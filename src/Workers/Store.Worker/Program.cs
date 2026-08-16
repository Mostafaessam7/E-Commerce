using System.Globalization;
using EventBus;
using Infrastructure;
using Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Notifications.Infrastructure;
using Ordering.Infrastructure;
using Ordering.Infrastructure.Persistence;
using Payments.Infrastructure;
using Payments.Infrastructure.Persistence;
using Persistence;
using Persistence.Outbox;
using Security;
using Serilog;
using Serilog.Events;
using Store.Worker;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, configuration) => configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
            formatProvider: CultureInfo.InvariantCulture)
        .WriteTo.File(
            "logs/store-worker-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
            formatProvider: CultureInfo.InvariantCulture));

    // Same cross-cutting building blocks Store.Web registers — module Application/Infrastructure
    // code (IDateTimeProvider, IDispatcher for ADR-014/018 cross-module calls, ICurrentUser for
    // AuditingInterceptor) doesn't know which composition root it's hosted in. ICurrentUser is
    // always "no one" here (HttpContextAccessor.HttpContext is null outside a web request) — fine,
    // audit fields for worker-driven writes are meant to read as system-initiated, not a user.
    builder.Services.AddSharedInfrastructure();
    builder.Services.AddSecurityCore();
    builder.Services.AddMessagingCore();
    builder.Services.AddInProcessEventBus();

    // Full module wiring (DbContext + repositories + command handlers), same as Store.Web — the
    // worker is a second composition root over the same modules, not a stripped-down DB poller. Only
    // modules that currently enqueue integration events are wired here; adding a third module's
    // outbox later means one AddXModule + one AddOutboxProcessor line, nothing structural.
    builder.Services.AddOrderingModule(builder.Configuration);
    builder.Services.AddPaymentsModule(builder.Configuration);
    // Consumer-only — Notifications never enqueues its own outbox events (nothing reacts to a
    // notification being sent), so no AddOutboxProcessor<NotificationsDbContext> call.
    builder.Services.AddNotificationsModule(builder.Configuration);

    builder.Services.AddOutboxProcessor<OrderingDbContext>();
    builder.Services.AddOutboxProcessor<PaymentsDbContext>();

    // No inbound HTTP here (plain Worker host) — LoggingHealthCheckPublisher runs these checks on
    // a timer and logs the result instead of an endpoint being polled.
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<OrderingDbContext>("ordering-db")
        .AddDbContextCheck<PaymentsDbContext>("payments-db");
    builder.Services.Configure<HealthCheckPublisherOptions>(options => options.Period = TimeSpan.FromMinutes(5));
    builder.Services.AddSingleton<IHealthCheckPublisher, LoggingHealthCheckPublisher>();

    var host = builder.Build();

    // Opt-in only (Docker Compose sets this) — see Store.Web/Program.cs's identical block and
    // Persistence.MigrationExtensions's doc comment. Safe to run alongside Store.Web's own
    // migration of these same two contexts (docker-compose.yml has this service depend on
    // store-web being up first, but this doesn't assume that ordering held).
    if (builder.Configuration.GetValue<bool>("ApplyMigrationsOnStartup"))
    {
        var migrationLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");
        await host.Services.MigrateWithRetryAsync<OrderingDbContext>(migrationLogger);
        await host.Services.MigrateWithRetryAsync<PaymentsDbContext>(migrationLogger);
    }

    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Store.Worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

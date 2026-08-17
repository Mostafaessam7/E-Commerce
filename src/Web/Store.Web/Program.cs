using System.Globalization;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure;
using Customers.Infrastructure.Persistence;
using Customers.Infrastructure;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure;
using Infrastructure;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure;
using Messaging;
using Microsoft.AspNetCore.Builder;
using Notifications.Infrastructure.Persistence;
using Notifications.Infrastructure;
using Observability;
using Ordering.Infrastructure.Persistence;
using Ordering.Infrastructure;
using Payments.Infrastructure.Persistence;
using Payments.Infrastructure;
using Persistence;
using Security;
using Serilog;
using Serilog.Events;
using Store.Web.Infrastructure.ExceptionHandling;
using Store.Web.Infrastructure.Observability;

// Two-stage Serilog init (the documented Serilog.AspNetCore pattern): a minimal bootstrap logger
// exists before the host is even built, so a failure during configuration/DI wiring itself still
// gets logged somewhere instead of disappearing — replaced by the fully-configured logger once
// `builder.Host.UseSerilog(...)` runs below.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
            formatProvider: CultureInfo.InvariantCulture)
        .WriteTo.File(
            "logs/store-web-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
            formatProvider: CultureInfo.InvariantCulture));

    // --- Cross-cutting building blocks (Phase 1 foundation) ---
    builder.Services.AddObservabilityCore();
    builder.Services.AddSecurityCore();
    builder.Services.AddSharedInfrastructure();

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();
    builder.Services.AddMessagingCore();
    builder.Services.AddHttpClient();

    // --- Module composition root ---
    // Every module owns its own `Add{Module}Module(IServiceCollection, IConfiguration)` extension
    // in that module's Infrastructure project (convention, not an interface — see
    // docs/module-composition.md for why this beats a reflection-discovered IModule abstraction).
    // Calls are added here as each module gets real services, starting Phase 4 (Catalog):
    //
    builder.Services.AddCatalogModule(builder.Configuration);
    builder.Services.AddInventoryModule(builder.Configuration);
    builder.Services.AddOrderingModule(builder.Configuration);
    builder.Services.AddPaymentsModule(builder.Configuration);
    builder.Services.AddCustomersModule(builder.Configuration);
    builder.Services.AddIdentityModule(builder.Configuration);
    // builder.Services.AddPromotionsModule(builder.Configuration);
    // builder.Services.AddShippingModule(builder.Configuration);
    // builder.Services.AddReviewsModule(builder.Configuration);
    // Store.Web doesn't register IEventBus (only Store.Worker processes the Outbox), so
    // Notifications' handlers never fire here — this wiring is just for the DbContext/health
    // check/future admin NotificationLog viewing, same reasoning as every other module.
    builder.Services.AddNotificationsModule(builder.Configuration);

    // One check per module DbContext against the single shared database (docs/database.md) —
    // "is the DB reachable at all" is a fair enough liveness signal at this scale; a per-table or
    // per-query check would be diagnosing something a log line already shows.
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<CatalogDbContext>("catalog-db")
        .AddDbContextCheck<InventoryDbContext>("inventory-db")
        .AddDbContextCheck<OrderingDbContext>("ordering-db")
        .AddDbContextCheck<PaymentsDbContext>("payments-db")
        .AddDbContextCheck<AppIdentityDbContext>("identity-db")
        .AddDbContextCheck<NotificationsDbContext>("notifications-db")
        .AddDbContextCheck<CustomersDbContext>("customers-db");

    builder.Services.AddControllersWithViews();

    var app = builder.Build();

    // Opt-in only (Docker Compose sets this; local `dotnet run` against LocalDB never does — see
    // docs/deployment.md) — `dotnet ef database update` per context is still the normal dev
    // workflow (docs/database.md).
    if (app.Configuration.GetValue<bool>("ApplyMigrationsOnStartup"))
    {
        var migrationLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");
        await app.Services.MigrateWithRetryAsync<CatalogDbContext>(migrationLogger);
        await app.Services.MigrateWithRetryAsync<InventoryDbContext>(migrationLogger);
        await app.Services.MigrateWithRetryAsync<OrderingDbContext>(migrationLogger);
        await app.Services.MigrateWithRetryAsync<PaymentsDbContext>(migrationLogger);
        await app.Services.MigrateWithRetryAsync<AppIdentityDbContext>(migrationLogger);
        await app.Services.MigrateWithRetryAsync<NotificationsDbContext>(migrationLogger);
        await app.Services.MigrateWithRetryAsync<CustomersDbContext>(migrationLogger);
    }

    // First in the pipeline so every log line for this request — including ones from middleware
    // that runs before routing/auth — carries the same correlation id.
    app.UseCorrelationId();
    app.UseSerilogRequestLogging();

    // Configure the HTTP request pipeline.
    app.UseExceptionHandler(new ExceptionHandlerOptions
    {
        // GlobalExceptionHandler handles JSON/API requests itself; anything it defers to (normal
        // page navigations) falls through to this Razor error view.
        ExceptionHandlingPath = "/Home/Error",
    });

    if (!app.Environment.IsDevelopment())
    {
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapStaticAssets();

    app.MapHealthChecks("/health");

    app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}")
        .WithStaticAssets();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
        .WithStaticAssets();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Store.Web terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

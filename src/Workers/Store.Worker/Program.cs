using EventBus;
using Infrastructure;
using Messaging;
using Ordering.Infrastructure;
using Ordering.Infrastructure.Persistence;
using Payments.Infrastructure;
using Payments.Infrastructure.Persistence;
using Persistence.Outbox;
using Security;

var builder = Host.CreateApplicationBuilder(args);

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

builder.Services.AddOutboxProcessor<OrderingDbContext>();
builder.Services.AddOutboxProcessor<PaymentsDbContext>();

var host = builder.Build();
host.Run();

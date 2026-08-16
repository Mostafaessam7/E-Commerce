# Observability

## Structured logging (Serilog)

Both composition roots (`Store.Web`, `Store.Worker`) use the documented Serilog.AspNetCore
"two-stage init" pattern: a minimal bootstrap `Log.Logger` (Console only) exists before the host
is built, so a failure during DI/configuration wiring itself still logs somewhere; it's replaced
by the fully-configured logger once `UseSerilog`/`AddSerilog` runs. Both `Program.cs` wrap
everything in `try/catch/finally` so a fatal startup exception is logged (`Log.Fatal`) instead of
disappearing, and `Log.CloseAndFlush()` always runs on shutdown.

Sinks (code-configured, not `appsettings.json`-driven — see ADR-022): Console (human-readable,
`{Timestamp} {Level}] {Message} {Properties}`) + a rolling daily file under `logs/` (gitignored,
14-day retention). `Microsoft.AspNetCore`/`Microsoft.EntityFrameworkCore` are overridden to
`Warning` — their `Information`-level noise (routing, EF command text) would drown out
application log lines otherwise; drop to `Debug`/remove the override locally when you need to see
raw SQL.

## Correlation id

`Observability.ICorrelationIdProvider`/`HttpContextCorrelationIdProvider` (Phase 1) reads
`TraceIdentifier` or an inbound `X-Correlation-Id` header. `Store.Web.Infrastructure.Observability.CorrelationIdMiddleware`
(Phase 12) is the first thing in the pipeline (`app.UseCorrelationId()`, before routing/auth/
exception handling) — it echoes the id back as a response header and wraps the rest of the
request in `ILogger.BeginScope(new Dictionary { [LogContextKeys.CorrelationId] = id })`, so
*every* log line for that request — including ones from `GlobalExceptionHandler`, EF Core's own
command logging, or a module's Application-layer code — carries the same id automatically,
without each call site fetching `ICorrelationIdProvider` itself. `GlobalExceptionHandler` only
resolves the provider directly to put the id in the `ProblemDetails` response payload; it no
longer needs its own `BeginScope` (redundant with the middleware's).

`app.UseSerilogRequestLogging()` adds one structured summary log line per request (method, path,
status, elapsed ms) on top of this.

## Health checks

- `Store.Web`: `builder.Services.AddHealthChecks().AddDbContextCheck<T>()` — one check per module
  `DbContext` (Catalog/Inventory/Ordering/Payments/Identity), all against the one shared database
  (docs/database.md) — mapped at `GET /health` (`app.MapHealthChecks("/health")`), anonymous,
  returns `Healthy`/`Unhealthy` plain text (ASP.NET Core's default writer — no custom JSON
  formatter added; not needed yet).
- `Store.Worker`: same `AddDbContextCheck<T>()` registrations (Ordering/Payments — the two
  modules it wires) but no inbound HTTP listener to expose an endpoint on (plain
  `Microsoft.NET.Sdk.Worker` host). `Microsoft.Extensions.Diagnostics.HealthChecks`' generic-host
  equivalent of polling an endpoint is `IHealthCheckPublisher` — `LoggingHealthCheckPublisher`
  (`Store.Worker/LoggingHealthCheckPublisher.cs`) runs the same checks on a timer
  (`HealthCheckPublisherOptions.Period`, 5 minutes) and logs the result. Swap for a real publisher
  (a monitoring system's push API) if one is ever wired up — nothing else changes.

## Not yet built

Distributed tracing (OpenTelemetry spans/exporters), metrics, and a real log aggregation backend
(Seq/ELK/Application Insights) — the file sink is a local-dev/single-box fallback, not a
production log destination. `LogContextKeys.OrderId`/`PaymentId` exist (Phase 1) but aren't
pushed at every relevant call site yet — add `BeginScope` at a handler when investigating a real
incident needs it, don't pre-instrument speculatively.

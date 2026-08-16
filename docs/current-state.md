Current Phase:
Phase 9 complete (Payments). Next up: whichever the user picks — Admin panel
(Phase 11), Identity's Account/Login UI (needed for real authenticated
customers, not just guest checkout), or Outbox processor (Phase 10).

Completed:
- Phase 1-6: Foundation, Persistence BB, Identity, Catalog, Ecomus storefront,
  Inventory. See git log / prior entries for detail — condensed here to keep
  this file small (Section: "Keep Documentation Small").
- Phase 7+8: Ordering module — Cart (guest/customer, merge-at-login) + Order
  (Place/Confirm/MarkAsPaid/StartProcessing/MarkAsShipped/MarkAsDelivered/
  Cancel, each a named domain method). PlaceOrderCommand re-validates
  price+availability against Catalog, reserves stock in Inventory with
  release-on-partial-failure compensation, flat 14% tax placeholder
  (ADR-016), enqueues OrderPlacedIntegrationEvent — one transaction.
- Phase 9: Payments module — `PaymentTransaction` (Initialize/MarkSucceeded/
  MarkFailed/Refund, each a guarded transition) + `RefundTransaction` (partial
  refunds). `IPaymentGateway` abstraction, `FakePaymentGateway` the only
  implementation but exercising real mechanics (ADR-017): HMAC-SHA256 signed
  webhooks, real signature verification (constant-time compare), idempotent
  processing via a `ProcessedWebhookEvent` ledger. `ProcessWebhookCommand`:
  verify signature → idempotency check → guarded domain transition → dispatch
  `Ordering.Contracts.MarkOrderAsPaidCommand` (ADR-018, reverse-direction
  ADR-014) → enqueue `PaymentSucceededIntegrationEvent` → one SaveChanges.
  Store.Web: "Pay now (simulated)" button on Confirmation page round-trips
  through the *real* `/api/webhooks/payments/fake` endpoint via
  `IWebhookSimulator` + `HttpClient`, not a shortcut.
- Three more real bugs found and fixed since Phase 7/8 (see docs/decisions.md
  ADR-011/012/013/015): a CS8122 expression-tree compile error in a test using
  an `is` pattern inside `.Should().ContainSingle(predicate)` (fixed: `.OfType<T>()`
  then a separate assertion), a missing `Messaging` project reference on
  `Ordering.Contracts` (needed for the reverse-direction `ICommand<T>`), and
  xUnit's default parallel test-class execution causing a spurious
  `DbUpdateConcurrencyException` across unrelated integration tests sharing
  one real DB — fixed via `xunit.runner.json` (ADR-019).
- All tests passing: 70 unit + 12 integration + 29 architecture.
- Commits: 71e7f96, 36008a1, c9f75b6, fd27d1f, bc563ff (Phase 1 through 7/8) —
  Phase 9 not yet committed as of this writing, see next actual commit hash in
  git log.

In Progress:
- (nothing — between phases)

Next:
- No Outbox processor yet (Phase 10) — `OrderPlacedIntegrationEvent` and
  `PaymentSucceededIntegrationEvent` rows just accumulate unprocessed.
- No Account/Login UI — checkout only works as a guest (AnonymousId cookie);
  `GetOrCreateCartCommand`/`MergeCartCommand` already support a CustomerId
  path, just nothing calls it yet.
- No Admin UI — Products/Categories/Brands/Stock can only be created via
  tests or direct DB access (Phase 11).
- No real payment provider — `FakePaymentGateway` only (ADR-017); swapping in
  a real one is one new `IPaymentGateway` implementation, not a rearchitecture.
- Categories/Brands still have no Application-layer commands (only Product,
  Cart/Order, and now Payments do).

Known Issues:
None outstanding.

Important Files:
- AGENTS.md — entry point; has "EF Core gotchas" + "Other gotchas" sections
  worth reading before touching persistence or cross-module code.
- docs/architecture.md, docs/modules.md — boundaries, don't re-derive from code.
- docs/database.md — schema-per-module + Guid-key convention.
- docs/events.md — domain vs integration events vs ADR-014/018's synchronous
  cross-module dispatch (three different mechanisms, don't conflate them).
- docs/security.md — webhook signature verification + idempotency (Phase 9).
- docs/testing.md — IntegrationTests must run sequentially (ADR-019).
- docs/decisions.md — ADR-001..019.

Database Changes:
Local dev DB `ECommerce` (LocalDB), 5 migrated contexts: CatalogDbContext
(schema `catalog`), AppIdentityDbContext (`identity`), InventoryDbContext
(`inventory`), OrderingDbContext (`ordering`), PaymentsDbContext (`payments`).
Empty (no seed data).

Decisions Made:
See docs/decisions.md. Newest: ADR-017 (fake payment gateway, real
mechanics), ADR-018 (reverse-direction ADR-014, Payments→Ordering), ADR-019
(IntegrationTests run sequentially, not in parallel).

Current Phase:
Phase 8 complete (Checkout). Next up: whichever the user picks — Payments
(Phase 9), Admin panel (Phase 11), or Identity's Account/Login UI (needed
before a real authenticated customer, not just guest checkout, is possible).

Completed:
- Phase 1-6: Foundation, Persistence BB, Identity, Catalog, Ecomus storefront,
  Inventory. See git log / prior entries for detail — condensed here to keep
  this file small (Section: "Keep Documentation Small").
- Phase 7+8: Ordering module — Cart (guest/customer, merge-at-login) + Order
  (Place/Confirm/MarkAsPaid/StartProcessing/MarkAsShipped/MarkAsDelivered/
  Cancel, each a named domain method, never a public status setter).
  PlaceOrderCommand: re-validates price+availability against Catalog,
  reserves stock in Inventory per line with release-on-partial-failure
  compensation, computes totals (flat 14% tax placeholder — ADR-016), creates
  the Order, enqueues OrderPlacedIntegrationEvent, clears the cart — all in
  one transaction. Store.Web: Cart page, Checkout form, Confirmation page,
  add-to-cart wired on Product Details.
- ADR-014: first real cross-module synchronous calls (Ordering -> Catalog for
  pricing, Ordering -> Inventory for reservation) via IDispatcher +
  Contracts-hosted commands/queries — moved ReserveStockCommand/
  ReleaseStockCommand to Inventory.Contracts, added
  GetProductVariantSnapshotQuery to Catalog.Contracts. ArchitectureTests
  updated: Application may reference any module's *.Contracts now (Contracts
  may reference Messaging too).
- Two more real bugs found and fixed (in addition to Phase 4-6's — see
  docs/decisions.md ADR-011/012/013): a C# namespace collision
  (`Ordering.Application.Cart` shadowed `Ordering.Domain.Cart` — ADR-015,
  renamed to `Carts`), and a missing `decimal` column type causing an EF
  warning (`ProductVariant.WeightKg`).
- All tests passing: 62 unit + 9 integration (2 new: full checkout round trip
  incl. cross-module reservation, and insufficient-stock compensation) + 29
  architecture. Verified live in-browser: Cart page renders correctly.
- Commits: 71e7f96, 36008a1, c9f75b6, fd27d1f (Phase 4-6) — Phase 7/8 not yet
  committed as of this writing, see next actual commit hash in git log.

In Progress:
- (nothing — between phases)

Next:
- Payment isn't wired — every placed order sits at PaymentStatus=Pending
  forever until Phase 9 (Payments module) exists to call `order.MarkAsPaid()`.
- No Account/Login UI — checkout only works as a guest (AnonymousId cookie);
  `GetOrCreateCartCommand`/`MergeCartCommand` already support a CustomerId
  path, just nothing calls it yet.
- No Admin UI — Products/Categories/Brands/Stock can only be created via
  tests or direct DB access (Phase 11).
- Categories/Brands still have no Application-layer commands (only Product
  and now Cart/Order do).

Known Issues:
None outstanding.

Important Files:
- AGENTS.md — entry point; has "EF Core gotchas" + "Other gotchas" sections
  worth reading before touching persistence or cross-module code.
- docs/architecture.md, docs/modules.md — boundaries, don't re-derive from code.
- docs/database.md — schema-per-module + Guid-key convention.
- docs/events.md — domain vs integration events vs ADR-014's synchronous
  cross-module dispatch (three different mechanisms, don't conflate them).
- docs/decisions.md — ADR-001..016.

Database Changes:
Local dev DB `ECommerce` (LocalDB), 4 migrated contexts: CatalogDbContext
(schema `catalog`, +FixWeightPrecision), AppIdentityDbContext (`identity`),
InventoryDbContext (`inventory`), OrderingDbContext (`ordering`). Empty (no
seed data).

Decisions Made:
See docs/decisions.md. Newest: ADR-014 (cross-module Contracts dispatch
pattern), ADR-015 (namespace-vs-entity-name collision), ADR-016 (flat tax
placeholder).

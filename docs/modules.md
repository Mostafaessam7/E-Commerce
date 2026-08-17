# Modules

Format per module: Responsibility / Owns / Does not own / Public contracts / Dependencies.
Most modules still depend only on BuildingBlocks — Ordering and Payments are the exceptions
(ADR-014's cross-module Contracts dispatch, see their sections below); add a Dependencies line
like theirs the moment another module gains one.

## Catalog
- Responsibility: products, categories, brands, attributes/variants, search/listing.
- Owns: `Product` (aggregate root — variants, images, SEO, tags, related/cross-sell/upsell ids,
  `BrandId`, `CategoryIds`; `SetBrand`/`SetCategories` let the admin Edit form change either after
  creation, Phase 21), `Category` (nested via ParentId; `Activate`/`Deactivate`), `Brand`
  (`Activate`/`Deactivate`), `ProductAttribute`/`AttributeValue`.
- Does not own: stock levels (Inventory — variants are referenced by Guid only), pricing
  promotions (Promotions).
- Public contracts: none yet (no cross-module consumer exists).
- Dependencies: BuildingBlocks only. DB schema: `catalog`.
- Application: `CreateProductCommand`, `GetProductBySlugQuery`, `SearchProductsQuery`
  (`Catalog.Application.Products`); `Catalog.Application.Brands`/`Categories` — `Create*Command`/
  `Activate*Command`/`Deactivate*Command`/`List*Query` for each, same admin command shape as
  Promotions' coupons and Shipping's methods (Phase 21).
- Admin: `Store.Web/Areas/Admin/Controllers/{BrandsController,CategoriesController}.cs` —
  list/create/activate/deactivate; `ProductsController`'s Create/Edit actions now dispatch
  `ListBrandsQuery`/`ListCategoriesQuery` to populate the product form's Brand select and Category
  checkboxes, closing the "admin form omits BrandId/CategoryIds" gap noted since Phase 11.

## Inventory
- Responsibility: stock quantity, reservations, prevents overselling.
- Owns: `StockItem` (aggregate root, keyed by `ProductVariantId` — a plain Guid, no FK/navigation
  into Catalog), `StockTransaction` (append-only history child entity).
- Does not own: product catalog data.
- Public contracts: none yet.
- Dependencies: BuildingBlocks only. DB schema: `inventory`.
- Application: `ReserveStockCommand`, `ReleaseStockCommand`, `GetStockQuery`
  (`Inventory.Application.Stock`). Concurrency conflicts surface as
  `SharedKernel.Exceptions.ConflictException` (HTTP 409), not a raw EF exception.

## Ordering
- Responsibility: cart → checkout → order lifecycle. Owns both `Cart` and `Order` — no separate
  "Cart" module exists in the fixed 10; they're tightly coupled (a Cart becomes an Order at
  checkout) and the master plan pairs them as one phase (7+8).
- Owns: `Cart`/`CartItem` (guest via AnonymousId or customer via CustomerId, mergeable at login),
  `Order`/`OrderItem`/`OrderStatusHistoryEntry` (Status/PaymentStatus/FulfillmentStatus, each only
  mutable through named domain methods — `MarkAsPaid()`, `Cancel()`, etc., never a public setter).
- Does not own: actual payment processing (Payments, not built), shipping rate calculation
  (Shipping, not built — checkout takes a shipping cost as input for now), tax rules (a flat
  placeholder rate lives in `PlaceOrderCommandHandler`, see docs/decisions.md).
- Public contracts: `OrderPlacedIntegrationEvent` (`Ordering.Contracts`), enqueued via the Outbox
  when `PlaceOrderCommand` succeeds.
- Dependencies: BuildingBlocks + **Catalog.Contracts and Inventory.Contracts** (ADR-014) — the
  first module doing real cross-module reads: `GetProductVariantSnapshotQuery` (re-validate price/
  availability) and `ReserveStockCommand`/`ReleaseStockCommand` (reserve at checkout, release on
  partial-failure compensation), all via the shared `IDispatcher`. DB schema: `ordering`.
- Application: `Ordering.Application.Carts` (Get/AddItem/RemoveItem/UpdateQuantity/ApplyCoupon/
  Merge/GetCart) and `Ordering.Application.Checkout` (`PlaceOrderCommand`, `GetOrderQuery`).

## Payments
- Responsibility: payment gateway abstraction, transactions, refunds, webhooks (Section 9).
- Owns: `PaymentTransaction` (aggregate root — Initialize/MarkSucceeded/MarkFailed/Refund, each a
  guarded transition), `RefundTransaction` (child, supports partial refunds).
- Does not own: Order state — reacts to it via a *synchronous* cross-module call (ADR-014,
  reverse direction: dispatches `Ordering.Contracts.MarkOrderAsPaidCommand` once a webhook
  confirms success), not an integration event (too slow/eventual for "the payment page needs to
  know right now").
- `IPaymentGateway` abstraction (Section 9's explicit requirement) — `FakePaymentGateway` is the
  only implementation (no real provider account exists), but it exercises the real mechanics: a
  signed webhook payload (HMAC-SHA256), real signature verification, idempotent processing (a
  `ProcessedWebhookEvent` ledger dedupes by provider event id). Swapping in a real provider means
  adding one new class, not touching Application/Domain or any other module.
- Public contracts: `PaymentSucceededIntegrationEvent` (`Payments.Contracts`), enqueued via the
  Outbox — no consumer yet.
- Dependencies: BuildingBlocks + **Ordering.Contracts** (for `MarkOrderAsPaidCommand`). DB schema:
  `payments`.
- Application: `Payments.Application.Payments` — `InitializePaymentCommand`,
  `ProcessWebhookCommand` (signature verify → idempotency check → guarded domain transition →
  dispatch `MarkOrderAsPaidCommand` → enqueue integration event, one transaction),
  `RefundPaymentCommand`, `GetPaymentQuery`, `ListPaymentsQuery` (admin-wide or narrowed to one
  order, Phase 21 — `IPaymentsQueries` is the read-side, `GetPaymentQuery` stayed on the write-side
  `IPaymentTransactionRepository` since it already existed and predates this read/write split).
- Admin: `Store.Web/Areas/Admin/Controllers/PaymentsController.cs` — lists every transaction and
  can trigger `RefundPaymentCommand` inline (`Permissions.Payments.View`/`Refund`, both defined
  since Phase 11 but unused until now).

## Customers
- Responsibility: customer profile + saved address book, distinct from Identity's auth concern.
- Owns: `Customer` (aggregate root — `Id` is deliberately the *same* Guid as the owning
  `ApplicationUser.Id`; Store.Web's `ProfileController` is the only place that equality is
  assumed, no DB-level FK between the two modules), `CustomerAddress` (child entity — a reusable
  saved address, distinct from `Ordering.Domain.ValueObjects.Address`, which is a permanent
  snapshot on a placed order that must never retroactively change).
- Does not own: authentication/credentials (Identity) — `Customer.Email` is cached for display
  only, Identity remains the source of truth.
- Public contracts: none yet (no cross-module consumer exists — Store.Web's `ProfileController`
  talks to it directly via `IDispatcher`, same as every other module's storefront-facing
  controller).
- Dependencies: BuildingBlocks only. DB schema: `customers`.
- Application (`Customers.Application.Profile`): `GetOrCreateCustomerCommand` (create-if-missing,
  same shape as Ordering's `GetOrCreateCartCommand`), `UpdateProfileCommand`, `AddAddressCommand`/
  `RemoveAddressCommand`/`SetDefaultAddressCommand`, `GetCustomerProfileQuery`. Exactly one address
  is ever marked default — enforced in the aggregate (first address added, or removing the
  current default, both auto-promote a new one).
- Not wired into checkout yet: `PlaceOrderCommand` still always passes `CustomerId: null` (guest
  checkout only) — pre-filling checkout from a saved default address, or attaching a real
  `CustomerId` to an order, is a follow-up, not done in the phase that built this module.

## Identity
- Responsibility: authentication, roles, permissions (ASP.NET Core Identity).
- Owns: `ApplicationUser`/`ApplicationRole` (`IdentityUser<Guid>`/`IdentityRole<Guid>` —
  framework-coupled, so they live in `Identity.Infrastructure`, not `Identity.Domain`), permission
  claims.
- Does not own: customer profile data (Customers, not built).
- Public contracts: none yet (no cross-module consumer exists — `IIdentityService` is consumed
  directly by `Store.Web/Controllers/AccountController.cs`, not through Contracts, since nothing
  outside the composition root needs it).
- Dependencies: BuildingBlocks only. DB schema: `identity`.
- Application: `Identity.Application.Abstractions.IIdentityService` — Register/Login/Logout/
  ConfirmEmail/GeneratePasswordResetToken/ResetPassword, all returning `Result`/`Result<T>`, kept
  free of `UserManager`/`SignInManager` (implemented by `IdentityService` in Infrastructure).
- Infrastructure extras: `PermissionRoleSeeder` (idempotently grants an "Admin" role every
  `Permissions.*` claim, never creates a user) and `AdminUserBootstrapper` (dev-only, opt-in via
  `Identity:DefaultAdmin:Email`/`Password` config — creates one pre-confirmed admin user, ADR-021)
  — both `IHostedService`s registered in `AddIdentityModule`. Full detail: docs/security.md.

## Promotions
- Responsibility: coupons, discount rules.
- Owns: `Coupon` (aggregate root — `Redeem`/`ReleaseRedemption`/`Activate`/`Deactivate`, each a
  guarded transition; the only mutation is `UsageCount`, incremented by `Redeem`).
- Does not own: order totals calculation — Ordering applies the returned discount amount, never
  computes one itself.
- Public contracts: `Promotions.Contracts.RedeemCouponCommand`/`ReleaseCouponCommand` (ADR-014) —
  dispatched from Ordering's checkout, same compensation shape as
  `Inventory.Contracts.ReserveStockCommand`/`ReleaseStockCommand`.
- Dependencies: BuildingBlocks only. DB schema: `promotions`.
- Application (`Promotions.Application.Coupons`): `RedeemCouponCommandHandler`/
  `ReleaseCouponCommandHandler` (implement the Contracts commands above), plus admin
  `CreateCouponCommand`/`ActivateCouponCommand`/`DeactivateCouponCommand`/`ListCouponsQuery`.
- Ordering's `Cart.ApplyCoupon`/`RemoveCoupon` only ever store a code *string* on the cart — no
  validation happens until checkout (`PlaceOrderCommandHandler` dispatches `RedeemCouponCommand`
  against the real subtotal, same "never trust the cart's stale snapshot" rule already applied to
  price/stock). If the order fails to place afterward (e.g. a later stock reservation failure),
  `ReleaseCouponCommand` undoes the usage-count increment so the coupon isn't silently burned by
  an order that never actually happened.
- Admin: `Store.Web/Areas/Admin/Controllers/CouponsController.cs` — list/create/activate/
  deactivate, gated by new `Permissions.Promotions.View`/`Manage`.

## Shipping
- Responsibility: shipping methods and their real cost. No zone/region rate matching (ADR-030) —
  every active method applies everywhere.
- Owns: `ShippingMethod` (aggregate root — `Name`/`Description`/`Cost` (Money)/`EstimatedDaysMin`/
  `Max`/`IsActive`; `Create`/`UpdateCost`/`Activate`/`Deactivate`).
- Does not own: order shipping snapshot — Ordering re-reads the authoritative cost at checkout
  time and stores it on the `Order` itself, same as it does for price/stock.
- Public contracts: `Shipping.Contracts.ListShippingMethodsQuery` (checkout's method picker —
  active-only unless `IncludeInactive: true` for the admin listing) and
  `GetShippingMethodQuery` (ADR-014 — dispatched from `Ordering.Application.Checkout
  .PlaceOrderCommandHandler` to price whichever method the customer picked; a client-submitted
  cost is never trusted, same rule already applied to Catalog price and Inventory stock).
- Dependencies: BuildingBlocks only. DB schema: `shipping`.
- Application (`Shipping.Application.Methods`): `ListShippingMethodsQueryHandler`/
  `GetShippingMethodQueryHandler` (implement the Contracts queries above — the latter fails with
  `ShippingMethod.NotFound`/`ShippingMethod.Inactive`), plus admin `CreateShippingMethodCommand`/
  `ActivateShippingMethodCommand`/`DeactivateShippingMethodCommand`.
- `PlaceOrderCommand`'s old `ShippingCost: decimal` parameter is gone — it takes
  `ShippingMethodId: Guid` and looks up the real cost itself (ADR-030).
- Admin: `Store.Web/Areas/Admin/Controllers/ShippingMethodsController.cs` — list/create/activate/
  deactivate, gated by new `Permissions.Shipping.View`/`Manage`.

## Reviews
- Responsibility: product reviews/ratings, with moderation. No "verified purchase" check against
  Ordering (ADR-031) — accepted from anyone, not just someone who actually bought the product.
- Owns: `Review` (aggregate root — `ProductId`/`ReviewerName`/`ReviewerEmail`/`Rating` (1-5)/
  `Title`/`Body`/`Status`; `Submit`/`Approve`/`Reject`, the latter two one-way transitions out of
  `Pending` only — `Review.NotPending` blocks re-moderating an already-decided review).
- Does not own: product data — `ProductId` is stored as a plain Guid, not cross-module validated
  against Catalog (a deliberate scope cut; the storefront only ever submits an id for a product
  it's currently rendering).
- Public contracts: none — no other module ever calls into Reviews (unlike Shipping/Promotions),
  so `Reviews.Contracts` stays empty, same as `Customers.Contracts` (ADR-028).
- Dependencies: BuildingBlocks only. DB schema: `reviews`.
- Application (`Reviews.Application.Reviews`): `SubmitReviewCommand` (storefront, no login
  required — same guest-friendly posture as checkout) always creates a `Pending` review;
  `GetProductReviewsQuery` (storefront's product page) only ever returns `Approved` ones plus the
  aggregate average rating. Admin: `ApproveReviewCommand`/`RejectReviewCommand`/`ListReviewsQuery`.
- Storefront: `Store.Web/Controllers/ProductController.cs` — the product details page renders
  approved reviews + a submission form; `Views/Product/Details.cshtml`.
- Admin: `Store.Web/Areas/Admin/Controllers/ReviewsController.cs` — pending/all listing,
  approve/reject, gated by new `Permissions.Reviews.View`/`Moderate`.

## Notifications
- Responsibility: email sending abstraction, notification log. First real consumer of the
  integration events other modules publish (docs/events.md) — everything before Phase 15 only
  published events into an empty room.
- Owns: `NotificationLog` (append-only send record, not an aggregate root — no business rules,
  just an audit trail).
- Does not own: business events that trigger notifications (reacts via integration events).
- Public contracts: `Notifications.Contracts.SendEmailCommand` (ADR-014/027) — dispatchable, for
  the rare case an email must be sent synchronously (e.g. Identity's account-confirmation link)
  rather than reactively; no DTOs describing Notifications' own state yet.
- Dependencies: BuildingBlocks + **Ordering.Contracts and Payments.Contracts** — Application
  references their integration event DTOs to react to them
  (`IIntegrationEventHandler<OrderPlacedIntegrationEvent>`,
  `IIntegrationEventHandler<PaymentSucceededIntegrationEvent>`), and dispatches
  `Ordering.Contracts.GetOrderContactInfoQuery` (ADR-014) when a payment-succeeded event doesn't
  itself carry an email. DB schema: `notifications`.
- `INotificationSender` abstraction (Section 9-style — same shape as `IPaymentGateway`) —
  `FakeEmailSender` is the only implementation (no real SMTP/SendGrid account exists), logs
  instead of sending, but every call still goes through the interface and every attempt still
  gets a real `NotificationLog` row.
- Wired into `Store.Worker` only (that's what runs `IEventBus`/the Outbox processor — see
  docs/events.md); Store.Web wires the module too but only for its DbContext/health check, since
  it never processes the Outbox and the handlers would never fire there.

---
## Store.Web's Admin area (Phase 11 — not a module)
Not one of the 10 modules — `Areas/Admin` inside `Store.Web`, the composition root, same as the
storefront controllers. Thin controllers per module (`ProductsController`, `OrdersController`,
`StockController`) dispatch existing/new commands through the same `IDispatcher` the storefront
uses; new Application-layer surface added specifically for it:
- `Catalog.Application.Products`: `UpdateProductCommand`, `AddProductVariantCommand`,
  `PublishProductCommand`, `ArchiveProductCommand`, `DeleteProductCommand`, `GetProductByIdQuery`
  (any status, unlike the storefront's Active-only `GetProductBySlugQuery`), plus
  `ProductSearchCriteria.IncludeAllStatuses` on the existing `SearchProductsQuery`.
- `Ordering.Application.Checkout`: `ConfirmOrderCommand`/`StartProcessingOrderCommand`/
  `ShipOrderCommand`/`DeliverOrderCommand`/`CancelOrderCommand` (thin wrappers over `Order`'s
  existing named transition methods), `IOrderQueries`/`SearchOrdersQuery` for the admin list.
- `Inventory.Application.Stock`: `AdjustStockCommand` (wraps `StockItem.AdjustTo`),
  `IStockQueries`/`SearchStockQuery` for the admin list.

Authorization: `[Authorize(Policy = Permissions.X)]` per action (`Permissions` catalog, Security
BB) — never role-name checks. `Identity.Infrastructure.Seeding.AdminUserBootstrapper` is a
dev-only, opt-in (config-gated) hosted service that creates one pre-confirmed admin user; see
ADR-021 and docs/security.md.

---
As of Phase 14: Catalog, Inventory, Ordering, Payments, and Identity have real Domain/Application/
Infrastructure code (sections above). Customers, Promotions, Shipping, Reviews, and Notifications
are still placeholders — their sections above describe intended ownership only, guiding whichever
phase builds them next, not current code.

# Modules

Format per module: Responsibility / Owns / Does not own / Public contracts / Dependencies.
Most modules still depend only on BuildingBlocks — Ordering, Payments, and Notifications are the
exceptions (ADR-014's cross-module Contracts dispatch, see their sections below); add a
Dependencies line like theirs the moment another module gains one.

## Catalog
- Responsibility: products, categories, brands, attributes/variants, search/listing.
- Owns: `Product` (aggregate root — variants, images, SEO, tags, related/cross-sell/upsell ids,
  `BrandId`, `CategoryIds`; `SetBrand`/`SetCategories` let the admin Edit form change either after
  creation, Phase 21), `Category` (nested via ParentId; `Activate`/`Deactivate`), `Brand`
  (`Activate`/`Deactivate`), `ProductAttribute`/`AttributeValue`.
- Does not own: stock levels (Inventory — variants are referenced by Guid only), pricing
  promotions (Promotions).
- Public contracts: `Catalog.Contracts.GetProductVariantSnapshotQuery`/`ProductVariantSnapshotDto`
  (ADR-014) — dispatched by Ordering at checkout to re-validate a variant's current price and
  purchasability; never the full `ProductVariant` entity, which would leak Catalog.Domain across
  the module boundary.
- Dependencies: BuildingBlocks only. DB schema: `catalog`.
- Application: `CreateProductCommand`, `GetProductBySlugQuery`, `SearchProductsQuery`
  (`Catalog.Application.Products`); `Catalog.Application.Brands`/`Categories` — `Create*Command`/
  `Activate*Command`/`Deactivate*Command`/`List*Query` for each, same admin command shape as
  Promotions' coupons and Shipping's methods (Phase 21).
- Admin: `Store.Web/Areas/Admin/Controllers/{BrandsController,CategoriesController}.cs` —
  list/create/activate/deactivate; `ProductsController`'s Create/Edit actions now dispatch
  `ListBrandsQuery`/`ListCategoriesQuery` to populate the product form's Brand select and Category
  checkboxes, closing the "admin form omits BrandId/CategoryIds" gap noted since Phase 11.
- Caching (Phase 22, ADR-033): `Catalog.Infrastructure.Caching.CachedProductQueries` decorates
  `IProductQueries` with a Redis-backed (`BuildingBlocks/Caching`) read-through cache for the
  storefront's `GetBySlugAsync`/`SearchAsync` — TTL-only (60s/30s), no write-side eviction.
  `GetVariantSnapshotAsync` (checkout's price/stock re-validation, ADR-014) and admin listings
  (`IncludeAllStatuses: true`) are never cached — the first because stale pricing is a
  correctness bug, the second because an admin needs to see their own just-published product
  immediately.
- Image upload (Phase 29, ADR-040): `AddProductImageCommand`/`RemoveProductImageCommand`
  (`Catalog.Application.Products.AdminCommands`) call `Product.AddImage`/`RemoveImage` — the domain
  method existed since Phase 4 but nothing dispatched it until now. The actual file write is a
  `Store.Web`-only concern behind `IProductImageStorage`/`LocalProductImageStorage`
  (`Store.Web/Infrastructure/Uploads`) — Catalog only ever receives a URL string, same as always.
  Files land in `wwwroot/uploads/products/{productId}/` (gitignored), served by a plain
  `app.UseStaticFiles()` alongside the build-time `MapStaticAssets()` pipeline, since the latter
  never sees anything written at runtime. Admin UI: `ProductsController.UploadImage`/`RemoveImage`
  + the Images panel on `Areas/Admin/Views/Products/Edit.cshtml`.

## Inventory
- Responsibility: stock quantity, reservations, prevents overselling.
- Owns: `StockItem` (aggregate root, keyed by `ProductVariantId` — a plain Guid, no FK/navigation
  into Catalog), `StockTransaction` (append-only history child entity).
- Does not own: product catalog data.
- Public contracts: `Inventory.Contracts.ReserveStockCommand`/`ReleaseStockCommand` (ADR-014) —
  dispatched by Ordering at checkout (reserve) and on partial-failure compensation (release); the
  compensation shape Promotions' `RedeemCouponCommand`/`ReleaseCouponCommand` (ADR-029) and
  Shipping later reused. `GetStockQuery` stays admin-only in `Inventory.Application.Stock`, not
  Contracts — nothing outside this module calls it.
- Dependencies: BuildingBlocks + `Catalog.Contracts` (ADR-014, Phase 32) — `SearchStockQueryHandler`
  dispatches `GetProductVariantSnapshotQuery` to enrich the admin Stock list with each row's real
  product name/SKU; `StockItem` itself still keys everything on a plain `ProductVariantId` Guid, no
  FK/navigation. DB schema: `inventory`.
- Application: `ReserveStockCommandHandler`/`ReleaseStockCommandHandler` (implement the Contracts
  commands above), `SearchStockQuery`/`GetStockQuery` (`Inventory.Application.Stock`). Concurrency
  conflicts surface as `SharedKernel.Exceptions.ConflictException` (HTTP 409), not a raw EF exception.

## Ordering
- Responsibility: cart → checkout → order lifecycle. Owns both `Cart` and `Order` — no separate
  "Cart" module exists in the fixed 10; they're tightly coupled (a Cart becomes an Order at
  checkout) and the master plan pairs them as one phase (7+8).
- Owns: `Cart`/`CartItem` (guest via AnonymousId or customer via CustomerId, mergeable at login),
  `Order`/`OrderItem`/`OrderStatusHistoryEntry` (Status/PaymentStatus/FulfillmentStatus, each only
  mutable through named domain methods — `MarkAsPaid()`, `Cancel()`, etc., never a public setter).
- Does not own: actual payment processing (Payments, Phase 9), shipping rate calculation
  (Shipping, Phase 19 — `PlaceOrderCommand` takes a `ShippingMethodId` and looks the real cost up
  via `GetShippingMethodQuery`, ADR-030), discount calculation (Promotions, Phase 18 —
  `RedeemCouponCommand`, ADR-029). Tax remains the one permanent exception: a flat 14% placeholder
  rate lives in `PlaceOrderCommandHandler` (`TaxRate` constant) — no Tax module exists in the fixed
  10, this isn't a phase that was skipped, it's out of this project's scope entirely.
- Public contracts: `OrderPlacedIntegrationEvent` (`Ordering.Contracts`), enqueued via the Outbox
  when `PlaceOrderCommand` succeeds. `GetOrderContactInfoQuery` (Phase 15, ADR-025) — dispatched by
  Notifications when a `PaymentSucceededIntegrationEvent` carries no email of its own.
- Dependencies: BuildingBlocks + four other modules' `*.Contracts` (ADR-014) — the first module
  doing real cross-module reads, now the module with the most of them: `Catalog.Contracts`
  (`GetProductVariantSnapshotQuery` — re-validate price/availability), `Inventory.Contracts`
  (`ReserveStockCommand`/`ReleaseStockCommand` — reserve at checkout, release on partial-failure
  compensation), `Promotions.Contracts` (`RedeemCouponCommand`/`ReleaseCouponCommand`, Phase 18),
  `Shipping.Contracts` (`GetShippingMethodQuery`/`ListShippingMethodsQuery`, Phase 19) — all via the
  shared `IDispatcher`. DB schema: `ordering`.
- Application: `Ordering.Application.Carts` (Get/AddItem/RemoveItem/UpdateQuantity/ApplyCoupon/
  RemoveCoupon/Merge/GetCart) and `Ordering.Application.Checkout` (`PlaceOrderCommand`,
  `GetOrderQuery`, `GetOrderContactInfoQuery`).
- Cart UI gap closed (Phase 31, ADR-042): `ApplyCouponCommand`/`RemoveCouponCommand` had existed
  since before Promotions was built (Phase 18) but were never registered in DI nor dispatched from
  anywhere — no UI ever let a customer actually set `Cart.CouponCode`, so `PlaceOrderCommand`'s
  real `RedeemCouponCommand` dispatch had nothing to ever redeem. `CartController` now dispatches
  both; `Views/Cart/Index.cshtml` has a real coupon input/remove UI. Also: `CartItem`/`CartItemDto`
  gained `ImageUrl`, sourced from `Catalog.Contracts.ProductVariantSnapshotDto`'s new
  `PrimaryImageUrl` field and snapshotted at add-to-cart time (same staleness rule as
  `UnitPrice`) — the cart page rendered no product images at all before this.

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
  Outbox — consumed by Notifications' `PaymentSucceededNotificationHandler` since Phase 15.
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
  and (Phase 28) `CheckoutController`/`AccountController` talk to it directly via `IDispatcher`,
  same as every other module's storefront-facing controller).
- Dependencies: BuildingBlocks only. DB schema: `customers`.
- Application (`Customers.Application.Profile`): `GetOrCreateCustomerCommand` (create-if-missing,
  same shape as Ordering's `GetOrCreateCartCommand`), `UpdateProfileCommand`, `AddAddressCommand`/
  `RemoveAddressCommand`/`SetDefaultAddressCommand`, `GetCustomerProfileQuery`. Exactly one address
  is ever marked default — enforced in the aggregate (first address added, or removing the
  current default, both auto-promote a new one).
- Wired into checkout since Phase 28 (ADR-039): `AccountController.Login` calls
  `GetOrCreateCustomerCommand` on every successful sign-in (idempotent) and dispatches Ordering's
  `MergeCartCommand` to fold the guest cart into the customer's own; `CartController`/
  `CheckoutController` resolve `CustomerId` from `ICurrentUser` instead of always `null`;
  `PlaceOrderCommand` gets a real `CustomerId`. `CheckoutController`'s `GET` also pre-fills the
  form from the customer's default address — informational only, `PlaceOrderCommand` still takes
  the address from the submitted form, exactly like a guest checkout.

## Identity
- Responsibility: authentication, roles, permissions (ASP.NET Core Identity).
- Owns: `ApplicationUser`/`ApplicationRole` (`IdentityUser<Guid>`/`IdentityRole<Guid>` —
  framework-coupled, so they live in `Identity.Infrastructure`, not `Identity.Domain`), permission
  claims.
- Does not own: customer profile data (Customers, Phase 17 — `Customer.Id` deliberately equals the
  owning `ApplicationUser.Id`, no DB-level FK between the two modules, see that section's
  ADR-028 note).
- Public contracts: none yet (no cross-module consumer exists — `IIdentityService` is consumed
  directly by `Store.Web/Controllers/AccountController.cs`, not through Contracts, since nothing
  outside the composition root needs it).
- Dependencies: BuildingBlocks only. DB schema: `identity`.
- Application: `Identity.Application.Abstractions.IIdentityService` — Register/Login/Logout/
  ConfirmEmail/GenerateEmailConfirmationToken/GeneratePasswordResetToken/ResetPassword, all
  returning `Result`/`Result<T>`, kept free of `UserManager`/`SignInManager` (implemented by
  `IdentityService` in Infrastructure).
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

- `Catalog.Application.Brands`/`Categories`, `Promotions.Application.Coupons`,
  `Shipping.Application.Methods`, `Reviews.Application.Reviews` (Approve/Reject side), and
  `Payments.Application.Payments.ListPaymentsQuery` — the same admin command shape (list/create/
  activate/deactivate, or moderate) repeated across `BrandsController`/`CategoriesController`/
  `CouponsController`/`ShippingMethodsController`/`ReviewsController`/`PaymentsController` (Phases
  18/19/20/21).

Authorization: `[Authorize(Policy = Permissions.X)]` per action (`Permissions` catalog, Security
BB) — never role-name checks. `Identity.Infrastructure.Seeding.AdminUserBootstrapper` is a
dev-only, opt-in (config-gated) hosted service that creates one pre-confirmed admin user; see
ADR-021 and docs/security.md.

UI (Phase 24, ADR-035): `_AdminLayout.cshtml` and all 18 admin view files are built on the real
`admin-ecomus` ThemeForest template (`wwwroot/admin-ecomus/`, a curated asset subset — same
curated-not-literal approach as the storefront's Phase 5) — real sidebar/header chrome, dark/light
toggle, `wg-table`/`wg-box`/`form-style-1`/`tf-button` component classes throughout. Replaced the
hand-styled placeholder Phase 11 shipped with.

Status badges fixed (Phase 32, ADR-043): every status pill across Orders/Payments/Products/Reviews
(`Store.Web.Infrastructure.Admin.StatusBadge.CssClass`) previously hardcoded the same
`block-available` (green) class regardless of the actual status string, so a Cancelled order and a
Delivered one were visually identical — the theme's real semantic classes
(`block-available`/`block-pending`/`block-not-available`/`block-tracking`) existed but only
Brands/Categories/Coupons/ShippingMethods' Active/Inactive toggles ever used more than one of
them. `StatusBadge` is the single place that maps a status string to the right one now. Stock page
(`Areas/Admin/Views/Stock/Index.cshtml`) also gained the product's actual name/SKU — it used to
show only the raw `ProductVariantId` Guid, which nobody can recognize a product by;
`SearchStockQueryHandler` now enriches each row via `Catalog.Contracts.GetProductVariantSnapshotQuery`
(ADR-014 — the first time Inventory reads from Catalog this way; previously only Ordering did).

## Storefront UI polish (Phase 30, ADR-041 — not a module)
- Vanta.js (`three.js` + the `VANTA.NET`/`VANTA.WAVES` effect bundles) self-hosted under
  `wwwroot/vendor/vanta/` — same "curated local assets, no runtime CDN dependency" discipline as
  `ecomus`/`admin-ecomus`. Home's hero (`#vanta-hero`, `VANTA.NET`, brand red-on-black) and the
  four Account pages' split visual panel (`#vanta-auth`, `VANTA.WAVES`, shared init in
  `Views/Shared/_VantaAuthScript.cshtml`) are the only two treatments — deliberately not applied
  to product/cart/checkout/admin screens, where an animated background would hurt readability and
  add unnecessary render cost for zero UX benefit. Both guarded behind a `prefers-reduced-motion`
  check and a real WebGL capability probe, falling back to a static gradient/image rather than a
  broken or thrashing page when either is absent.
- `Views/Account/{Login,Register,ForgotPassword,ResetPassword}.cshtml` rebuilt onto a shared
  `.auth-split` two-column layout (`wwwroot/css/site-custom.css`) — form on one side, the Vanta
  visual on the other, single column on mobile. Submit buttons switched from a one-off `btn
  btn-dark` to the same `tf-btn btn-fill radius-3` class every other storefront CTA uses.
- Real bug fixed, not just a redesign: `Views/Shared/_ValidationScriptsPartial.cshtml` referenced
  `~/lib/jquery-validation*` files that were never actually present in `wwwroot/lib` — client-side
  validation had been silently dead on every page that included it (Checkout since Phase 7/8,
  now also all four Account pages) the whole time, falling back to a full server round-trip for
  every validation error. Fetched the real `jquery-validation`/`jquery-validation-unobtrusive`
  packages into `wwwroot/lib/` so the existing `asp-validation-for` markup (already correct)
  finally gets a live client-side check instead of only a server-side one.

---
As of Phase 29: all ten modules (Catalog, Inventory, Ordering, Payments, Identity, Notifications,
Customers, Promotions, Shipping, Reviews) have real Domain/Application/Infrastructure code — none
are placeholders, and every gap flagged in the last full-project review (rate limiting, sitemap,
Customers-in-checkout, product image upload) has been closed. See docs/current-state.md for
exactly which phase built each one and what (if anything) remains open.

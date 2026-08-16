# Modules

Format per module: Responsibility / Owns / Does not own / Public contracts / Dependencies.
All modules currently depend only on BuildingBlocks (no cross-module Contracts
usage exists yet — add rows here the moment one does).

## Catalog
- Responsibility: products, categories, brands, attributes/variants, search/listing.
- Owns: `Product` (aggregate root — variants, images, SEO, tags, related/cross-sell/upsell ids),
  `Category` (nested via ParentId), `Brand`, `ProductAttribute`/`AttributeValue`.
- Does not own: stock levels (Inventory — variants are referenced by Guid only), pricing
  promotions (Promotions).
- Public contracts: none yet (no cross-module consumer exists).
- Dependencies: BuildingBlocks only. DB schema: `catalog`.
- Application: `CreateProductCommand`, `GetProductBySlugQuery`, `SearchProductsQuery`
  (`Catalog.Application.Products`).

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
- Responsibility: cart → checkout → order lifecycle, order aggregate.
- Owns: Order, OrderItem, OrderStatusHistory.
- Does not own: payment processing, shipping cost calculation logic.
- Public contracts: none yet (will publish `OrderPlacedIntegrationEvent` etc.).
- Dependencies: BuildingBlocks only.

## Payments
- Responsibility: payment gateway abstraction, transactions, refunds, webhooks.
- Owns: PaymentTransaction, RefundTransaction.
- Does not own: Order state (reacts to it via integration events).
- Public contracts: none yet.
- Dependencies: BuildingBlocks only.

## Customers
- Responsibility: customer profile, addresses (distinct from Identity's auth concern).
- Owns: Customer, Address.
- Does not own: authentication/credentials (Identity).
- Public contracts: none yet.
- Dependencies: BuildingBlocks only.

## Identity
- Responsibility: authentication, roles, permissions (ASP.NET Core Identity).
- Owns: ApplicationUser, roles, permission claims.
- Does not own: customer profile data.
- Public contracts: none yet.
- Dependencies: BuildingBlocks only.

## Promotions
- Responsibility: coupons, discount rules.
- Owns: Coupon, DiscountRule.
- Does not own: order totals calculation (Ordering applies the discount).
- Public contracts: none yet.
- Dependencies: BuildingBlocks only.

## Shipping
- Responsibility: shipping methods/zones, `IShippingProvider` abstraction.
- Owns: ShippingMethod, ShippingZone.
- Does not own: order shipping snapshot (Ordering stores it at order time).
- Public contracts: none yet.
- Dependencies: BuildingBlocks only.

## Reviews
- Responsibility: product reviews/ratings.
- Owns: Review.
- Does not own: product data.
- Public contracts: none yet.
- Dependencies: BuildingBlocks only.

## Notifications
- Responsibility: email/SMS sending abstractions, notification templates.
- Owns: NotificationLog.
- Does not own: business events that trigger notifications (reacts via integration events).
- Public contracts: none yet.
- Dependencies: BuildingBlocks only.

---
No module has domain/application code yet as of Phase 1 — this file describes
intended ownership to guide Phase 4+ implementation, not current code.

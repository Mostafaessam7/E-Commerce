# Modules

Format per module: Responsibility / Owns / Does not own / Public contracts / Dependencies.
All modules currently depend only on BuildingBlocks (no cross-module Contracts
usage exists yet — add rows here the moment one does).

## Catalog
- Responsibility: products, categories, brands, attributes/variants, search input.
- Owns: Product, Category, Brand, Attribute, ProductVariant aggregates.
- Does not own: stock levels (Inventory), pricing promotions (Promotions).
- Public contracts: none yet.
- Dependencies: BuildingBlocks only.

## Inventory
- Responsibility: stock quantity, reservations, prevents overselling.
- Owns: StockItem, StockReservation, StockTransaction.
- Does not own: product catalog data.
- Public contracts: none yet.
- Dependencies: BuildingBlocks only.

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

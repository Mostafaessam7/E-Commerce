using System.ComponentModel.DataAnnotations;
using Promotions.Domain;

namespace Store.Web.Areas.Admin.Models;

public sealed record DashboardViewModel(int TotalProducts, int TotalOrders, int PendingOrders, int TrackedStockItems);

public sealed class ProductFormModel
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Slug { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ShortDescription { get; set; }

    public string? Description { get; set; }

    public Guid? BrandId { get; set; }

    public List<Guid> CategoryIds { get; set; } = [];
}

public sealed class ProductEditFormModel
{
    public Guid Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ShortDescription { get; set; }

    public string? Description { get; set; }

    public Guid? BrandId { get; set; }

    public List<Guid> CategoryIds { get; set; } = [];
}

public sealed class AddVariantFormModel
{
    public Guid ProductId { get; set; }

    [Required, StringLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Required, Range(0.01, 1_000_000)]
    public decimal Price { get; set; }

    [Required, StringLength(3)]
    public string Currency { get; set; } = "EGP";

    public decimal? SalePrice { get; set; }
}

public sealed class ShipOrderFormModel
{
    public Guid OrderId { get; set; }

    public string? TrackingNumber { get; set; }
}

public sealed class CancelOrderFormModel
{
    public Guid OrderId { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class AdjustStockFormModel
{
    public Guid ProductVariantId { get; set; }

    [Required]
    public int NewQuantityOnHand { get; set; }

    [Required, StringLength(300)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class CreateCouponFormModel
{
    [Required, StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    public DiscountType DiscountType { get; set; } = DiscountType.Percentage;

    [Required, Range(0.01, 1_000_000)]
    public decimal Value { get; set; }

    [Required, StringLength(3)]
    public string Currency { get; set; } = "EGP";

    public DateTime? ExpiresAtUtc { get; set; }

    public int? UsageLimit { get; set; }

    public decimal? MinimumOrderAmount { get; set; }
}

public sealed class RefundPaymentFormModel
{
    [Required]
    public Guid PaymentTransactionId { get; set; }

    [Required, Range(0.01, 1_000_000)]
    public decimal Amount { get; set; }

    [StringLength(300)]
    public string? Reason { get; set; }
}

public sealed class CreateBrandFormModel
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Slug { get; set; } = string.Empty;
}

public sealed class CreateCategoryFormModel
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Slug { get; set; } = string.Empty;

    public Guid? ParentId { get; set; }
}

public sealed class CreateShippingMethodFormModel
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Description { get; set; }

    [Required, Range(0, 1_000_000)]
    public decimal Cost { get; set; }

    [Required, StringLength(3)]
    public string Currency { get; set; } = "EGP";

    public int? EstimatedDaysMin { get; set; }

    public int? EstimatedDaysMax { get; set; }
}

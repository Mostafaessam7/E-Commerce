namespace Store.Web.Infrastructure.Storefront;

/// <summary>
/// Same fix as <see cref="Admin.StatusBadge"/> (Phase 32, ADR-043), for the one storefront page
/// that shows an order status: <c>Views/Checkout/Confirmation.cshtml</c> hardcoded
/// <c>bg-warning</c>/<c>bg-secondary</c> for every Status/PaymentStatus regardless of the actual
/// value. Maps to Bootstrap's own semantic <c>bg-*</c> classes (already loaded on the storefront —
/// unlike the Admin area, this page has no `admin-ecomus` theme to draw from).
/// </summary>
public static class OrderStatusBadge
{
    public static string CssClass(string status) => status switch
    {
        "Delivered" or "Paid" or "Confirmed" or "Fulfilled" => "bg-success",
        "Pending" or "Processing" => "bg-warning text-dark",
        "Cancelled" or "Failed" => "bg-danger",
        "Shipped" or "Refunded" => "bg-info text-dark",
        _ => "bg-secondary",
    };
}

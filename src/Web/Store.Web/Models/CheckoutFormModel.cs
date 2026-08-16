using System.ComponentModel.DataAnnotations;

namespace Store.Web.Models;

/// <summary>Thin view model for the checkout form — mapped to
/// <c>Ordering.Application.Checkout.AddressInput</c> by the controller, not passed through
/// directly, so validation attributes stay a Web-layer concern (Section 35).</summary>
public sealed class CheckoutFormModel
{
    [Required, StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, Phone, StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string Line1 { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Line2 { get; set; }

    [Required, StringLength(150)]
    public string City { get; set; } = string.Empty;

    [StringLength(150)]
    public string? State { get; set; }

    [Required, StringLength(20)]
    public string PostalCode { get; set; } = string.Empty;

    [Required, StringLength(2, MinimumLength = 2)]
    public string Country { get; set; } = "EG";

    public bool BillingSameAsShipping { get; set; } = true;

    public string? BillingFullName { get; set; }

    public string? BillingPhone { get; set; }

    public string? BillingLine1 { get; set; }

    public string? BillingLine2 { get; set; }

    public string? BillingCity { get; set; }

    public string? BillingState { get; set; }

    public string? BillingPostalCode { get; set; }

    public string? BillingCountry { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}

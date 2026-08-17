using System.ComponentModel.DataAnnotations;

namespace Store.Web.Models;

public sealed class UpdateProfileFormModel
{
    [StringLength(200)]
    public string? FullName { get; set; }

    [Phone, StringLength(30)]
    public string? Phone { get; set; }
}

public sealed class AddAddressFormModel
{
    [Required, StringLength(100)]
    public string Label { get; set; } = string.Empty;

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

    public bool IsDefault { get; set; }
}

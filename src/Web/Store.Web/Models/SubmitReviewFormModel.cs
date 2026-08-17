using System.ComponentModel.DataAnnotations;

namespace Store.Web.Models;

public sealed class SubmitReviewFormModel
{
    [Required]
    public Guid ProductId { get; set; }

    [Required, StringLength(200)]
    public string ReviewerName { get; set; } = string.Empty;

    [EmailAddress, StringLength(256)]
    public string? ReviewerEmail { get; set; }

    [Required, Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(200)]
    public string? Title { get; set; }

    [Required, StringLength(4000)]
    public string Body { get; set; } = string.Empty;
}

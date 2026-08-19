namespace Store.Web.Infrastructure.Admin;

/// <summary>
/// Maps a status string (every admin list view already renders `@order.Status`,
/// `@payment.Status`, etc. — each DTO's enum via its own `ToString()`) to one of the
/// `admin-ecomus` theme's real semantic badge classes (`wwwroot/admin-ecomus/css/styles.css`:
/// `.block-available` green, `.block-pending` orange, `.block-not-available` red,
/// `.block-tracking` blue). Phase 32 (ADR-043): every admin status badge previously hardcoded
/// `block-available bg-1` regardless of the actual value, so a Cancelled order and a Delivered one
/// rendered as the same gray-green pill — this is the single place that stops recurring, instead
/// of re-deriving the same status → color mapping per view.
/// </summary>
public static class StatusBadge
{
    public static string CssClass(string status) => status switch
    {
        // Positive / terminal-success outcomes.
        "Active" or "Approved" or "Delivered" or "Succeeded" or "Paid" or "Confirmed" or "Fulfilled" => "block-available",

        // In-flight / needs-attention, not yet resolved either way.
        "Pending" or "Processing" or "Draft" => "block-pending",

        // Terminal-negative outcomes.
        "Cancelled" or "Rejected" or "Failed" or "Archived" or "Inactive" => "block-not-available",

        // Informational, resolved but not a plain "success" (money moved back, in transit).
        "Shipped" or "Refunded" or "PartiallyRefunded" => "block-tracking",

        // Anything not in this list yet (a future enum member) gets the neutral gray the whole
        // badge family already falls back to via `bg-1`, not an incorrect color.
        _ => "block-stock",
    };
}

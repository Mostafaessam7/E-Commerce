namespace Security;

/// <summary>
/// Custom claim types issued by the Identity module (Phase 3) on top of the standard
/// <see cref="System.Security.Claims.ClaimTypes"/>. A user's JWT/cookie carries one
/// <see cref="Permission"/> claim per permission they hold (flattened from their roles at
/// sign-in), so authorization checks are a cheap claim lookup instead of a database round trip.
/// </summary>
public static class CustomClaimTypes
{
    public const string Permission = "permission";
}

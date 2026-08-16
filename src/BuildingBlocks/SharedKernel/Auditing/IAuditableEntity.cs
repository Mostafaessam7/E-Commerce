namespace SharedKernel.Auditing;

/// <summary>
/// Implemented by entities that need created/modified tracking. Populated by the EF Core
/// <c>SaveChanges</c> interceptor added in Phase 2 (<c>AuditingInterceptor</c>) — never set these
/// by hand from Application or Web code.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; }

    string? CreatedBy { get; }

    DateTime? LastModifiedAtUtc { get; }

    string? LastModifiedBy { get; }
}

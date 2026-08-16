using Infrastructure;
using Security;

namespace IntegrationTests.Persistence;

internal sealed class FakeDateTimeProvider(DateTime utcNow) : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = utcNow;
}

internal sealed class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; set; }

    public Guid? UserId { get; set; }

    public string? Email { get; set; }

    public bool IsInRole(string role) => false;

    public bool HasPermission(string permission) => false;
}

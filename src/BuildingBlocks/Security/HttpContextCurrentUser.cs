using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Security;

public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var value = Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Email => Principal?.FindFirst(ClaimTypes.Email)?.Value;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    public bool HasPermission(string permission) =>
        Principal?.HasClaim(CustomClaimTypes.Permission, permission) ?? false;
}

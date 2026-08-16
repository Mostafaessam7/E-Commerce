using Identity.Infrastructure.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Derives from ASP.NET Core Identity's <see cref="IdentityDbContext{TUser,TRole,TKey}"/>, not
/// <c>Persistence.AppDbContextBase</c> (C# has no multiple inheritance) — so it doesn't get the
/// Outbox/soft-delete conventions the other modules' contexts get for free. Auditing still works
/// here because <c>AuditingInterceptor</c> is wired through <c>DbContextOptions</c>
/// (see <see cref="DependencyInjection.AddIdentityModule"/>), not through base-class inheritance.
/// </summary>
public sealed class AppIdentityDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<ApplicationRole>().ToTable("Roles");
    }
}

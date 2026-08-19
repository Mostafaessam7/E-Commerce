using System.Runtime.CompilerServices;

// Repository/query implementations are `internal` (accessed only through their Application-layer
// interfaces in production code — see docs/architecture.md); IntegrationTests constructs them
// directly against a real database to test the EF Core mapping itself, which needs this visibility.
[assembly: InternalsVisibleTo("IntegrationTests")]

// CachedProductQueries (also internal, same reasoning) is pure caching logic against a fake inner
// IProductQueries + an in-memory IDistributedCache — no real database involved, so its test
// belongs in UnitTests rather than IntegrationTests despite needing the same internal visibility.
[assembly: InternalsVisibleTo("UnitTests")]

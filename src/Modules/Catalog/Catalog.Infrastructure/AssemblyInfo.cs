using System.Runtime.CompilerServices;

// Repository/query implementations are `internal` (accessed only through their Application-layer
// interfaces in production code — see docs/architecture.md); IntegrationTests constructs them
// directly against a real database to test the EF Core mapping itself, which needs this visibility.
[assembly: InternalsVisibleTo("IntegrationTests")]

using SharedKernel.Results;

namespace Messaging;

/// <summary>
/// Handles one <see cref="IRequest{TResponse}"/>. Registered explicitly per module
/// (<c>services.AddScoped&lt;IRequestHandler&lt;CreateProductCommand, Guid&gt;, CreateProductCommandHandler&gt;()</c>)
/// — no assembly scanning, consistent with ADR-003's "explicit over reflection discovery".
/// Returns <see cref="Result{TValue}"/>, not a bare value or a thrown exception, so callers
/// (thin controllers) map failures the same way everywhere via
/// <c>Store.Web/Infrastructure/ExceptionHandling/ResultExtensions.cs</c>.
/// </summary>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken = default);
}

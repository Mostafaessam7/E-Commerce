using SharedKernel.Results;

namespace Messaging;

/// <summary>
/// What a thin controller/Razor page calls instead of resolving a handler itself:
/// <c>var result = await dispatcher.Send(new CreateProductCommand(...));</c>. One dispatcher per
/// application (registered once in Store.Web/Store.Worker), works across every module because
/// resolution happens by request type at call time, not by module.
/// </summary>
public interface IDispatcher
{
    Task<Result<TResponse>> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}

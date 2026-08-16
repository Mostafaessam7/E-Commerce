using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Results;

namespace Messaging;

/// <summary>
/// Resolves <c>IRequestHandler&lt;TRequest, TResponse&gt;</c> for the request's concrete runtime
/// type via DI and invokes it. The reflection cost is paid once per request type (cached in
/// <see cref="_handleMethods"/>), not per call.
/// </summary>
public sealed class Dispatcher : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> _handleMethods = new();

    private readonly IServiceProvider _serviceProvider;

    public Dispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<Result<TResponse>> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));

        var handler = _serviceProvider.GetService(handlerType)
            ?? throw new InvalidOperationException($"No handler registered for '{requestType.Name}'.");

        var handleMethod = _handleMethods.GetOrAdd(handlerType, t => t.GetMethod(nameof(IRequestHandler<IRequest<object>, object>.Handle))!);

        return (Task<Result<TResponse>>)handleMethod.Invoke(handler, [request, cancellationToken])!;
    }
}

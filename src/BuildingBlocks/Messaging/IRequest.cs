namespace Messaging;

/// <summary>
/// A CQRS command or query. <see cref="ICommand{TResponse}"/>/<see cref="IQuery{TResponse}"/> are
/// pure markers on top of this — same dispatch mechanism, kept as distinct names so a handler's
/// intent (mutates vs reads) is visible at the call site. Minimal in-house replacement for
/// MediatR (ADR-004: MediatR's current versions require a commercial license this project's size
/// doesn't need).
/// </summary>
public interface IRequest<TResponse>;

public interface ICommand<TResponse> : IRequest<TResponse>;

public interface IQuery<TResponse> : IRequest<TResponse>;

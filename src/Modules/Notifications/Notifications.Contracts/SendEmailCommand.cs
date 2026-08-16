using Messaging;

namespace Notifications.Contracts;

/// <summary>
/// The dispatchable (ADR-014) counterpart to Notifications' event-reactive handlers — for the
/// rare case something needs an email sent *right now*, synchronously, rather than "eventually,
/// in reaction to a fact that already happened" (Identity's account-confirmation/password-reset
/// links are exactly this: the link has to exist before the response even renders, there's no
/// prior integration event to react to). Any module's Application layer may dispatch this via the
/// shared <c>IDispatcher</c>, same as any other Contracts-hosted command.
/// </summary>
public sealed record SendEmailCommand(string ToAddress, string Subject, string Body) : ICommand<Unit>;

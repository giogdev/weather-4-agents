namespace Weather4Agents.Application.CQRS;

/// <summary>
/// Non-generic-over-command bridge that lets <see cref="Dispatcher"/> invoke a strongly typed
/// command handler from a call site that only sees <see cref="ICommand"/>. One closed instance is
/// built per concrete command type and cached, so dispatch stays free of <c>dynamic</c>/DLR calls.
/// </summary>
internal abstract class CommandHandlerWrapperBase
{
    public abstract Task HandleAsync(object command, IServiceProvider serviceProvider, CancellationToken ct);
}

internal sealed class CommandHandlerWrapper<TCommand> : CommandHandlerWrapperBase
    where TCommand : ICommand
{
    public override Task HandleAsync(object command, IServiceProvider serviceProvider, CancellationToken ct)
    {
        var handler = serviceProvider.GetService(typeof(ICommandHandler<TCommand>)) as ICommandHandler<TCommand>
            ?? throw new InvalidOperationException(
                $"No handler registered for command '{typeof(TCommand)}'.");

        return handler.HandleAsync((TCommand)command, ct);
    }
}

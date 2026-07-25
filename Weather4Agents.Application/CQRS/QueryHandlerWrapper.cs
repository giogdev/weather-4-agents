namespace Weather4Agents.Application.CQRS;

/// <summary>
/// Non-generic-over-query bridge that lets <see cref="Dispatcher"/> invoke a strongly typed
/// query handler while knowing only the result type at the call site. One closed instance is
/// built per concrete query type and cached, so dispatch stays free of <c>dynamic</c>/DLR calls.
/// </summary>
internal abstract class QueryHandlerWrapperBase<TResult>
{
    public abstract Task<TResult> HandleAsync(object query, IServiceProvider serviceProvider, CancellationToken ct);
}

internal sealed class QueryHandlerWrapper<TQuery, TResult> : QueryHandlerWrapperBase<TResult>
    where TQuery : IQuery<TResult>
{
    public override Task<TResult> HandleAsync(object query, IServiceProvider serviceProvider, CancellationToken ct)
    {
        var handler = serviceProvider.GetService(typeof(IQueryHandler<TQuery, TResult>)) as IQueryHandler<TQuery, TResult>
            ?? throw new InvalidOperationException(
                $"No handler registered for query '{typeof(TQuery)}' returning '{typeof(TResult)}'.");

        return handler.HandleAsync((TQuery)query, ct);
    }
}

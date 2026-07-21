using System.Collections.Concurrent;

namespace Weather4Agents.Application.CQRS;

/// <summary>
/// Resolves and invokes query/command handlers through compiled generic wrappers cached per
/// concrete request type. Dispatch carries no <c>dynamic</c>/DLR overhead, and an unregistered
/// handler surfaces a clear <see cref="InvalidOperationException"/>.
/// </summary>
public sealed class Dispatcher : IDispatcher
{
    // Keyed by the concrete query/command type. A query type fixes its result type via
    // IQuery<TResult>, so the request type alone is a safe cache key. Static so the reflection
    // work to build each closed wrapper happens once per process, not once per scope.
    private static readonly ConcurrentDictionary<Type, object> QueryWrappers = new();
    private static readonly ConcurrentDictionary<Type, CommandHandlerWrapperBase> CommandWrappers = new();

    private readonly IServiceProvider _serviceProvider;

    public Dispatcher(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public Task<TResult> SendAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var wrapper = (QueryHandlerWrapperBase<TResult>)QueryWrappers.GetOrAdd(
            query.GetType(),
            queryType => Activator.CreateInstance(
                typeof(QueryHandlerWrapper<,>).MakeGenericType(queryType, typeof(TResult)))!);

        return wrapper.HandleAsync(query, _serviceProvider, ct);
    }

    public Task SendAsync(ICommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var wrapper = CommandWrappers.GetOrAdd(
            command.GetType(),
            commandType => (CommandHandlerWrapperBase)Activator.CreateInstance(
                typeof(CommandHandlerWrapper<>).MakeGenericType(commandType))!);

        return wrapper.HandleAsync(command, _serviceProvider, ct);
    }
}

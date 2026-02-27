using Microsoft.Extensions.DependencyInjection;

namespace Weather4Agents.Application.CQRS;

public sealed class Dispatcher : IDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public Dispatcher(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public Task<TResult> SendAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default)
    {
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
        dynamic handler = _serviceProvider.GetRequiredService(handlerType);
        return handler.HandleAsync((dynamic)query, ct);
    }

    public Task SendAsync(ICommand command, CancellationToken ct = default)
    {
        var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
        dynamic handler = _serviceProvider.GetRequiredService(handlerType);
        return handler.HandleAsync((dynamic)command, ct);
    }
}

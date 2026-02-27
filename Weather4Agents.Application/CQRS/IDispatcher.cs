namespace Weather4Agents.Application.CQRS;

public interface IDispatcher
{
    Task<TResult> SendAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default);
    Task SendAsync(ICommand command, CancellationToken ct = default);
}

using Microsoft.Extensions.DependencyInjection;
using Weather4Agents.Application.CQRS;

namespace Weather4Agents.Test.CQRS;

/// <summary>
/// Behavioural contract of <see cref="Dispatcher"/> (ticket 13): dispatch resolves the registered
/// handler through the DI container and forwards the concrete request — with no <c>dynamic</c>
/// binding — while a missing registration surfaces a clear, typed resolution error.
/// </summary>
public class DispatcherTests
{
    private sealed record EchoQuery(string Value) : IQuery<string>;

    private sealed class EchoQueryHandler : IQueryHandler<EchoQuery, string>
    {
        public Task<string> HandleAsync(EchoQuery query, CancellationToken ct)
            => Task.FromResult($"handled:{query.Value}");
    }

    private sealed record RecordingCommand(List<string> Log) : ICommand;

    private sealed class RecordingCommandHandler : ICommandHandler<RecordingCommand>
    {
        public Task HandleAsync(RecordingCommand command, CancellationToken ct)
        {
            command.Log.Add("executed");
            return Task.CompletedTask;
        }
    }

    private static IDispatcher BuildDispatcher(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddScoped<IDispatcher, Dispatcher>();
        configure(services);
        return services.BuildServiceProvider().GetRequiredService<IDispatcher>();
    }

    [Fact]
    public async Task SendAsync_Query_ResolvesRegisteredHandlerAndReturnsResult()
    {
        var dispatcher = BuildDispatcher(s =>
            s.AddScoped<IQueryHandler<EchoQuery, string>, EchoQueryHandler>());

        var result = await dispatcher.SendAsync(new EchoQuery("ping"));

        Assert.Equal("handled:ping", result);
    }

    [Fact]
    public async Task SendAsync_Command_ResolvesRegisteredHandlerAndExecutesIt()
    {
        var dispatcher = BuildDispatcher(s =>
            s.AddScoped<ICommandHandler<RecordingCommand>, RecordingCommandHandler>());
        var log = new List<string>();

        await dispatcher.SendAsync(new RecordingCommand(log));

        Assert.Equal(["executed"], log);
    }

    [Fact]
    public async Task SendAsync_Query_WithoutRegisteredHandler_ThrowsTypedResolutionError()
    {
        var dispatcher = BuildDispatcher(_ => { });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.SendAsync(new EchoQuery("ping")));
    }

    [Fact]
    public async Task SendAsync_Command_WithoutRegisteredHandler_ThrowsTypedResolutionError()
    {
        var dispatcher = BuildDispatcher(_ => { });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.SendAsync(new RecordingCommand([])));
    }

    [Fact]
    public async Task SendAsync_Query_DispatchedRepeatedly_StaysCorrectAcrossCachedWrapper()
    {
        var dispatcher = BuildDispatcher(s =>
            s.AddScoped<IQueryHandler<EchoQuery, string>, EchoQueryHandler>());

        var first = await dispatcher.SendAsync(new EchoQuery("a"));
        var second = await dispatcher.SendAsync(new EchoQuery("b"));

        Assert.Equal("handled:a", first);
        Assert.Equal("handled:b", second);
    }
}

using Gdzie.Kupic.Realtime;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Unit.Realtime;

public class RealtimeBuilderTests
{
    private interface IFirstChannel;

    private interface ISecondChannel;

    private sealed class FirstChannel : IFirstChannel;

    private sealed class SecondChannel : ISecondChannel;

    [Test]
    public void AddChannel_RegistersEachChannelAsASingleton()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddRealtimeModule(realtime => realtime
            .AddChannel<IFirstChannel, FirstChannel>()
            .AddChannel<ISecondChannel, SecondChannel>());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IFirstChannel>().ShouldBeOfType<FirstChannel>();
        provider.GetRequiredService<ISecondChannel>().ShouldBeOfType<SecondChannel>();
        provider.GetRequiredService<IFirstChannel>().ShouldBeSameAs(provider.GetRequiredService<IFirstChannel>());
        services.Count(d => d.ServiceType == typeof(IFirstChannel)).ShouldBe(1);
    }

    [Test]
    public void AddRealtimeModule_ProvidesTheSender()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRealtimeModule(_ => { });
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IRealtimeSender>().ShouldNotBeNull();
    }
}
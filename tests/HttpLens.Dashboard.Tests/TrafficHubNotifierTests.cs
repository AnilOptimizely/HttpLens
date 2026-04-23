using HttpLens.Core.Models;
using HttpLens.Core.Storage;
using HttpLens.Dashboard.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace HttpLens.Dashboard.Tests;

public class TrafficHubNotifierTests
{
    [Fact]
    public async Task StartAsync_RecordAdded_BroadcastsRecordAddedEvent()
    {
        var store = new InMemoryTrafficStore(Options.Create(new HttpLens.Core.Configuration.HttpLensOptions()));
        var hubContext = new Mock<IHubContext<TrafficHub>>();
        var hubClients = new Mock<IHubClients>();
        var clientProxy = new Mock<IClientProxy>();
        var tcs = new TaskCompletionSource<HttpTrafficRecord>(TaskCreationOptions.RunContinuationsAsynchronously);

        clientProxy.Setup(x => x.SendCoreAsync(
                "RecordAdded",
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((_, args, _) =>
            {
                if (args.Length > 0 && args[0] is HttpTrafficRecord record)
                    tcs.TrySetResult(record);
            })
            .Returns(Task.CompletedTask);
        hubClients.Setup(x => x.All).Returns(clientProxy.Object);
        hubContext.Setup(x => x.Clients).Returns(hubClients.Object);

        var notifier = new TrafficHubNotifier(store, hubContext.Object, NullLogger<TrafficHubNotifier>.Instance);
        await notifier.StartAsync(CancellationToken.None);

        var recordToAdd = new HttpTrafficRecord { RequestMethod = "GET", RequestUri = "https://example.com" };
        store.Add(recordToAdd);
        var broadcasted = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Equal(recordToAdd.Id, broadcasted.Id);
        await notifier.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_AfterStop_RecordAddedDoesNotBroadcast()
    {
        var store = new InMemoryTrafficStore(Options.Create(new HttpLens.Core.Configuration.HttpLensOptions()));
        var hubContext = new Mock<IHubContext<TrafficHub>>();
        var hubClients = new Mock<IHubClients>();
        var clientProxy = new Mock<IClientProxy>();
        hubClients.Setup(x => x.All).Returns(clientProxy.Object);
        hubContext.Setup(x => x.Clients).Returns(hubClients.Object);

        var notifier = new TrafficHubNotifier(store, hubContext.Object, NullLogger<TrafficHubNotifier>.Instance);
        await notifier.StartAsync(CancellationToken.None);
        await notifier.StopAsync(CancellationToken.None);

        store.Add(new HttpTrafficRecord { RequestMethod = "GET", RequestUri = "https://example.com" });

        clientProxy.Verify(x => x.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

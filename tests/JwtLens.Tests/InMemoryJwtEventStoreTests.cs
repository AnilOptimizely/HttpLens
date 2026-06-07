using Xunit;
using JwtLens.Configuration;
using JwtLens.Models;
using JwtLens.Storage;
using Microsoft.Extensions.Options;
using Moq;

namespace JwtLens.Tests;

public sealed class InMemoryJwtEventStoreTests
{
    private static InMemoryJwtEventStore CreateStore(int maxEvents = 200)
    {
        var options = new JwtLensOptions { MaxStoredEvents = maxEvents };
        var monitor = new Mock<IOptionsMonitor<JwtLensOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(options);
        return new InMemoryJwtEventStore(monitor.Object);
    }

    [Fact]
    public void Add_IncreasesCount()
    {
        var store = CreateStore();
        store.Add(new CapturedJwt { Direction = TokenDirection.Inbound });

        Assert.Equal(1, store.Count);
        Assert.Equal(1, store.TotalCaptured);
    }

    [Fact]
    public void Add_ExceedsMaxSize_EvictsOldest()
    {
        var store = CreateStore(maxEvents: 3);

        for (int i = 0; i < 5; i++)
        {
            store.Add(new CapturedJwt { Direction = TokenDirection.Inbound, RequestUri = $"http://test/{i}" });
        }

        Assert.Equal(3, store.Count);
        Assert.Equal(5, store.TotalCaptured);

        var all = store.GetAll();
        Assert.Equal("http://test/2", all[0].RequestUri);
        Assert.Equal("http://test/4", all[2].RequestUri);
    }

    [Fact]
    public void GetById_ExistingId_ReturnsEvent()
    {
        var store = CreateStore();
        var captured = new CapturedJwt { Direction = TokenDirection.Inbound };
        store.Add(captured);

        var found = store.GetById(captured.Id);

        Assert.NotNull(found);
        Assert.Equal(captured.Id, found.Id);
    }

    [Fact]
    public void GetById_NonExistingId_ReturnsNull()
    {
        var store = CreateStore();

        var found = store.GetById(Guid.NewGuid());

        Assert.Null(found);
    }

    [Fact]
    public void Clear_RemovesAllEventsAndResetsCounter()
    {
        var store = CreateStore();
        store.Add(new CapturedJwt { Direction = TokenDirection.Inbound });
        store.Add(new CapturedJwt { Direction = TokenDirection.Outbound });

        store.Clear();

        Assert.Equal(0, store.Count);
        Assert.Equal(0, store.TotalCaptured);
    }

    [Fact]
    public void GetAll_ReturnsInInsertionOrder()
    {
        var store = CreateStore();
        var first = new CapturedJwt { Direction = TokenDirection.Inbound, RequestUri = "first" };
        var second = new CapturedJwt { Direction = TokenDirection.Outbound, RequestUri = "second" };

        store.Add(first);
        store.Add(second);

        var all = store.GetAll();
        Assert.Equal(2, all.Count);
        Assert.Equal("first", all[0].RequestUri);
        Assert.Equal("second", all[1].RequestUri);
    }

    [Fact]
    public void ThreadSafety_ConcurrentAdds_DoNotThrow()
    {
        var store = CreateStore(maxEvents: 50);

        Parallel.For(0, 100, i =>
        {
            store.Add(new CapturedJwt { Direction = TokenDirection.Inbound, RequestUri = $"http://test/{i}" });
        });

        Assert.True(store.Count <= 50);
        Assert.Equal(100, store.TotalCaptured);
    }
}

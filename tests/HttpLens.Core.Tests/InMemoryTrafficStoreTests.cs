using HttpLens.Core.Configuration;
using HttpLens.Core.Models;
using HttpLens.Core.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace HttpLens.Core.Tests;

public class InMemoryTrafficStoreTests
{
    private static InMemoryTrafficStore Create(int max = 500) =>
        new(Options.Create(new HttpLensOptions { MaxStoredRecords = max }));

    private static HttpTrafficRecord MakeRecord() => new() { RequestMethod = "GET" };

    [Fact]
    public void AddAndRetrieveById()
    {
        var store = Create();
        var record = MakeRecord();
        store.Add(record);

        var found = store.GetById(record.Id);
        Assert.NotNull(found);
        Assert.Equal(record.Id, found.Id);
    }

    [Fact]
    public void GetAllReturnsRecordsInInsertionOrder()
    {
        var store = Create();
        var r1 = MakeRecord();
        var r2 = MakeRecord();
        store.Add(r1);
        store.Add(r2);

        var all = store.GetAll();
        Assert.Equal(2, all.Count);
        Assert.Equal(r1.Id, all[0].Id);
        Assert.Equal(r2.Id, all[1].Id);
    }

    [Fact]
    public void RingBufferEvictsOldestWhenFull()
    {
        const int max = 3;
        var store = Create(max);
        var records = Enumerable.Range(0, 5).Select(_ => MakeRecord()).ToList();
        foreach (var r in records) store.Add(r);

        Assert.Equal(max, store.Count);
        var ids = store.GetAll().Select(r => r.Id).ToHashSet();
        // The last 3 should be present, first 2 evicted.
        Assert.DoesNotContain(records[0].Id, ids);
        Assert.DoesNotContain(records[1].Id, ids);
        Assert.Contains(records[4].Id, ids);
    }

    [Fact]
    public void ClearEmptiesStore()
    {
        var store = Create();
        store.Add(MakeRecord());
        store.Add(MakeRecord());
        store.Clear();

        Assert.Equal(0, store.Count);
        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void OnRecordAddedEventFires()
    {
        var store = Create();
        HttpTrafficRecord? received = null;
        store.OnRecordAdded += r => received = r;

        var record = MakeRecord();
        store.Add(record);

        Assert.NotNull(received);
        Assert.Equal(record.Id, received!.Id);
    }
}

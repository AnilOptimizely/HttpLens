using HttpLens.Core.Configuration;
using HttpLens.Core.Models;
using HttpLens.Core.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Threading;
using Xunit;

namespace HttpLens.Core.Tests;

public sealed class SqliteTrafficStoreTests
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "httplens-tests", Guid.NewGuid().ToString("N"));
    private static readonly string[] expected = ["c"];
    private static readonly string[] expectedArray = ["a", "b"];

    [Fact]
    public void AddAndGetById_RecordExists_ReturnsRecord()
    {
        var dbPath = CreateTempDbPath();
        var store = CreateStore(databasePath: dbPath);
        try
        {
            var record = CreateRecord();
            store.Add(record);
            var found = store.GetById(record.Id);

            Assert.NotNull(found);
            Assert.Equal(record.Id, found!.Id);
        }
        finally
        {
            TryDeleteFileBestEffort(dbPath);
        }
    }

    private static void TryDeleteFileBestEffort(string dbPath)
    {
        const int maxAttempts = 20;

        for (var i = 1; i <= maxAttempts; i++)
        {
            try
            {
                if (File.Exists(dbPath))
                    File.Delete(dbPath);

                var dir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);

                return;
            }
            catch (IOException)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(100 * i);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(100 * i);
            }
        }

        // Intentionally DO NOT throw. Cleanup is best effort.
    }

    [Fact]
    public void GetAll_RecordsAdded_ReturnsTimestampDescending()
    {
        var store = CreateStore();
        var older = CreateRecord();
        var newer = CreateRecord();
        older.Timestamp = DateTimeOffset.UtcNow.AddSeconds(-10);
        newer.Timestamp = DateTimeOffset.UtcNow;
        store.Add(older);
        store.Add(newer);

        var all = store.GetAll();

        Assert.Equal(2, all.Count);
        Assert.Equal(newer.Id, all[0].Id);
        Assert.Equal(older.Id, all[1].Id);
    }

    // Apply same try/finally pattern to each test...
    // For Constructor_FilePathConfigured..., use a unique dbPath instead of shared "schema-test.db"

    private static string CreateTempDbPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "httplens-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{Guid.NewGuid():N}.db");
    }

    [Fact]
    public void GetByRetryGroupId_GroupExists_ReturnsOnlyMatchingRecords()
    {
        var store = CreateStore();
        var groupId = Guid.NewGuid();
        var inGroup = CreateRecord();
        inGroup.RetryGroupId = groupId;
        var outsideGroup = CreateRecord();
        outsideGroup.RetryGroupId = Guid.NewGuid();
        store.Add(inGroup);
        store.Add(outsideGroup);

        var records = store.GetByRetryGroupId(groupId);

        Assert.Single(records);
        Assert.Equal(inGroup.Id, records[0].Id);
    }

    [Fact]
    public void Clear_RecordsExist_RemovesAllRecords()
    {
        var store = CreateStore();
        store.Add(CreateRecord());
        store.Add(CreateRecord());

        store.Clear();

        Assert.Equal(0, store.Count);
        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void Add_ExceedsMaxStoredRecords_EvictsOldestRecord()
    {
        var store = CreateStore(maxStoredRecords: 2);
        var first = CreateRecord();
        first.Timestamp = DateTimeOffset.UtcNow.AddSeconds(-2);
        var second = CreateRecord();
        second.Timestamp = DateTimeOffset.UtcNow.AddSeconds(-1);
        var third = CreateRecord();
        third.Timestamp = DateTimeOffset.UtcNow;
        store.Add(first);
        store.Add(second);
        store.Add(third);

        var all = store.GetAll();

        Assert.Equal(2, all.Count);
        Assert.DoesNotContain(all, r => r.Id == first.Id);
        Assert.Contains(all, r => r.Id == second.Id);
        Assert.Contains(all, r => r.Id == third.Id);
    }

    [Fact]
    public void Add_WithHeaders_SerializesAndDeserializesHeaders()
    {
        var store = CreateStore();
        var record = CreateRecord();
        record.RequestHeaders["X-Test"] = ["a", "b"];
        record.ResponseHeaders["Y-Test"] = ["c"];
        store.Add(record);

        var found = store.GetById(record.Id);

        Assert.NotNull(found);
        Assert.Equal(expectedArray, found!.RequestHeaders["X-Test"]);
        Assert.Equal(expected, found.ResponseHeaders["Y-Test"]);
    }

    [Fact]
    public void Add_Invoked_RaisesOnRecordAddedEvent()
    {
        var store = CreateStore();
        var record = CreateRecord();
        HttpTrafficRecord? received = null;
        store.OnRecordAdded += captured => received = captured;

        store.Add(record);

        Assert.NotNull(received);
        Assert.Equal(record.Id, received!.Id);
    }

    [Fact]
    public void Add_ConcurrentCalls_RemainsThreadSafe()
    {
        var store = CreateStore(maxStoredRecords: 2000);
        var records = Enumerable.Range(0, 250).Select(_ => CreateRecord()).ToList();

        Parallel.ForEach(records, record => store.Add(record));

        Assert.Equal(records.Count, store.Count);
    }

    [Fact]
    public void Constructor_FilePathConfigured_CreatesSchemaOnFirstUse()
    {
        var uniqueDir = Path.Combine(Path.GetTempPath(), "httplens-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(uniqueDir);

        var databasePath = Path.Combine(uniqueDir, $"schema-test-{Guid.NewGuid():N}.db");
        try
        {
            var store = CreateStore(databasePath: databasePath);

            _ = store.Count; // triggers initialization/schema creation

            Assert.True(File.Exists(databasePath));

            using var connection = new SqliteConnection($"Data Source={databasePath};Cache=Shared");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT version FROM schema_version LIMIT 1;";
            var version = Convert.ToInt32(command.ExecuteScalar());
            Assert.Equal(1, version);
        }
        finally
        {
            // best-effort cleanup
            TryDeleteFileBestEffort(databasePath); // your retry helper
        }
    }

    public void Dispose()
    {
        if (!Directory.Exists(_tempDirectory))
            return;

        const int maxAttempts = 10;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(100 * attempt);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                Thread.Sleep(100 * attempt);
            }
        }

        // Final attempt throws if still locked
        Directory.Delete(_tempDirectory, recursive: true);
    }

    private SqliteTrafficStore CreateStore(int maxStoredRecords = 500, string? databasePath = null)
    {
        var path = databasePath ?? Path.Combine(_tempDirectory, $"{Guid.NewGuid():N}.db");
        var options = Options.Create(new HttpLensOptions
        {
            MaxStoredRecords = maxStoredRecords,
            EnableSqlitePersistence = true,
            SqliteDatabasePath = path
        });
        return new SqliteTrafficStore(options);
    }

    private static HttpTrafficRecord CreateRecord() => new()
    {
        RequestMethod = "GET",
        RequestUri = "https://example.com",
        Timestamp = DateTimeOffset.UtcNow,
        Duration = TimeSpan.FromMilliseconds(123),
        HttpClientName = "test",
        RequestHeaders = [],
        ResponseHeaders = [],
        IsSuccess = true
    };
}

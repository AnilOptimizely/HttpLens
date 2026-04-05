using System.Collections.Concurrent;
using HttpLens.Core.Configuration;
using HttpLens.Core.Models;
using Microsoft.Extensions.Options;

namespace HttpLens.Core.Storage;

/// <summary>Thread-safe in-memory ring-buffer store backed by <see cref="ConcurrentQueue{T}"/>.</summary>
public sealed class InMemoryTrafficStore : ITrafficStore
{
    private readonly ConcurrentQueue<HttpTrafficRecord> _queue = new();
    private readonly int _maxRecords;
    private readonly object _evictionLock = new();

    /// <inheritdoc />
    public event Action<HttpTrafficRecord>? OnRecordAdded;

    /// <param name="options">HttpLens configuration; reads <see cref="HttpLensOptions.MaxStoredRecords"/>.</param>
    public InMemoryTrafficStore(IOptions<HttpLensOptions> options)
    {
        _maxRecords = options.Value.MaxStoredRecords;
    }

    /// <inheritdoc />
    public int Count => _queue.Count;

    /// <inheritdoc />
    public void Add(HttpTrafficRecord record)
    {
        _queue.Enqueue(record);

        // Ring-buffer eviction — keep under the cap.
        lock (_evictionLock)
        {
            while (_queue.Count > _maxRecords)
                _queue.TryDequeue(out _);
        }

        OnRecordAdded?.Invoke(record);
    }

    /// <inheritdoc />
    public IReadOnlyList<HttpTrafficRecord> GetAll() => _queue.ToArray();

    /// <inheritdoc />
    public HttpTrafficRecord? GetById(Guid id) =>
        _queue.FirstOrDefault(r => r.Id == id);

    /// <inheritdoc />
    public IReadOnlyList<HttpTrafficRecord> GetByRetryGroupId(Guid groupId) =>
        _queue.Where(r => r.RetryGroupId == groupId).ToArray();

    /// <inheritdoc />
    public void Clear()
    {
        while (_queue.TryDequeue(out _)) { }
    }
}

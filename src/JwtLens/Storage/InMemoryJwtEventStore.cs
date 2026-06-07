using System.Collections.Concurrent;
using JwtLens.Configuration;
using JwtLens.Models;
using Microsoft.Extensions.Options;

namespace JwtLens.Storage;

/// <summary>
/// Thread-safe in-memory ring buffer store for captured JWT events.
/// </summary>
public sealed class InMemoryJwtEventStore : IJwtEventStore
{
    private readonly ConcurrentQueue<CapturedJwt> _queue = new();
    private readonly IOptionsMonitor<JwtLensOptions> _optionsMonitor;
    private long _totalCaptured;

    /// <summary>
    /// Initializes a new instance of <see cref="InMemoryJwtEventStore"/>.
    /// </summary>
    /// <param name="optionsMonitor">Options monitor for dynamic configuration.</param>
    public InMemoryJwtEventStore(IOptionsMonitor<JwtLensOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    /// <inheritdoc />
    public int Count => _queue.Count;

    /// <inheritdoc />
    public long TotalCaptured => Interlocked.Read(ref _totalCaptured);

    /// <inheritdoc />
    public void Add(CapturedJwt captured)
    {
        _queue.Enqueue(captured);
        Interlocked.Increment(ref _totalCaptured);

        var maxSize = _optionsMonitor.CurrentValue.MaxStoredEvents;
        if (maxSize < 0) maxSize = 0;

        while (_queue.Count > maxSize && _queue.TryDequeue(out _))
        {
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<CapturedJwt> GetAll()
    {
        return _queue.ToArray();
    }

    /// <inheritdoc />
    public CapturedJwt? GetById(Guid id)
    {
        return _queue.FirstOrDefault(e => e.Id == id);
    }

    /// <inheritdoc />
    public void Clear()
    {
        while (_queue.TryDequeue(out _)) { }
        Interlocked.Exchange(ref _totalCaptured, 0);
    }
}

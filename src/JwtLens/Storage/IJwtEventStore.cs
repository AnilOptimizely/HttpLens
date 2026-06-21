using JwtLens.Models;

namespace JwtLens.Storage;

/// <summary>
/// Abstraction over the in-memory JWT event store.
/// </summary>
public interface IJwtEventStore
{
    /// <summary>Total number of events currently held.</summary>
    int Count { get; }

    /// <summary>Total number of events captured since the last <see cref="Clear"/> call, including evicted ones.</summary>
    long TotalCaptured { get; }

    /// <summary>Adds a captured JWT event to the store.</summary>
    void Add(CapturedJwt captured);

    /// <summary>Returns a snapshot of all stored events in insertion order.</summary>
    IReadOnlyList<CapturedJwt> GetAll();

    /// <summary>Looks up a single event by its ID.</summary>
    CapturedJwt? GetById(Guid id);

    /// <summary>Removes all stored events.</summary>
    void Clear();
}

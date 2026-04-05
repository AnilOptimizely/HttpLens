using HttpLens.Core.Models;

namespace HttpLens.Core.Storage;

/// <summary>Abstraction over the in-process traffic record store.</summary>
public interface ITrafficStore
{
    /// <summary>Fired after each record is successfully added. Used by SignalR hub in v0.5.</summary>
    event Action<HttpTrafficRecord>? OnRecordAdded;

    /// <summary>Total number of records currently held.</summary>
    int Count { get; }

    /// <summary>Append a record. Implementations must be thread-safe.</summary>
    void Add(HttpTrafficRecord record);

    /// <summary>Returns a snapshot of all records in insertion order.</summary>
    IReadOnlyList<HttpTrafficRecord> GetAll();

    /// <summary>Looks up a single record by its ID.</summary>
    HttpTrafficRecord? GetById(Guid id);

    /// <summary>Returns all records matching the given retry group ID.</summary>
    IReadOnlyList<HttpTrafficRecord> GetByRetryGroupId(Guid groupId);

    /// <summary>Removes all stored records.</summary>
    void Clear();
}

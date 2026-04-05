namespace HttpLens.Core.Models;

/// <summary>Groups all retry attempts for a single logical request.</summary>
public sealed class RetryGroup
{
    /// <summary>Shared identifier for all attempts in this group.</summary>
    public Guid GroupId { get; init; }

    /// <summary>Individual attempts in chronological order.</summary>
    public List<HttpTrafficRecord> Attempts { get; init; } = new();

    /// <summary>Total number of attempts.</summary>
    public int TotalAttempts => Attempts.Count;

    /// <summary>True if any attempt resulted in a success status.</summary>
    public bool EventuallySucceeded => Attempts.Any(a => a.IsSuccess);

    /// <summary>Sum of all attempt durations.</summary>
    public TimeSpan TotalDuration => Attempts.Aggregate(TimeSpan.Zero, (sum, a) => sum + a.Duration);
}

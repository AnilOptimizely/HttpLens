using System.Collections.Concurrent;
using JwtLens.Models;

namespace JwtLens.Analysis;

/// <summary>
/// Tracks consecutive JWT tokens per subject and computes claim diffs.
/// </summary>
public sealed class ClaimDiffTracker
{
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _lastPayloadBySubject = new();

    /// <summary>
    /// Computes the claim differences between the provided payload and the previously seen
    /// payload for the same subject. Updates the stored payload for future comparisons.
    /// </summary>
    /// <param name="subject">The subject identifier (sub claim).</param>
    /// <param name="currentPayload">The current token's payload claims.</param>
    /// <returns>A list of claim diffs, or an empty list if this is the first token for the subject.</returns>
    public List<ClaimDiff> TrackAndDiff(string? subject, Dictionary<string, string> currentPayload)
    {
        if (string.IsNullOrEmpty(subject))
            return [];

        var previous = _lastPayloadBySubject.GetValueOrDefault(subject);
        _lastPayloadBySubject[subject] = new Dictionary<string, string>(currentPayload);

        if (previous is null)
            return [];

        return ComputeDiff(previous, currentPayload);
    }

    /// <summary>
    /// Clears all tracked state.
    /// </summary>
    public void Clear()
    {
        _lastPayloadBySubject.Clear();
    }

    /// <summary>
    /// Computes claim diffs from a JwtEvent by extracting the subject and payload.
    /// Used by the legacy middleware capture pipeline.
    /// </summary>
    /// <param name="evt">The JWT event containing decoded payload claims.</param>
    /// <returns>A list of claim diffs, or an empty list if this is the first token for the subject.</returns>
    public List<ClaimDiff> ComputeDiffs(Models.JwtEvent evt)
    {
        if (!evt.DecodedSuccessfully || evt.Payload is null)
            return [];

        evt.Payload.TryGetValue("sub", out var subObj);
        var subject = subObj?.ToString();

        var stringPayload = new Dictionary<string, string>();
        foreach (var kvp in evt.Payload)
        {
            stringPayload[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
        }

        return TrackAndDiff(subject, stringPayload);
    }

    internal static List<ClaimDiff> ComputeDiff(
        Dictionary<string, string> previous,
        Dictionary<string, string> current)
    {
        var diffs = new List<ClaimDiff>();

        foreach (var (key, oldValue) in previous)
        {
            if (current.TryGetValue(key, out var newValue))
            {
                if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
                {
                    diffs.Add(new ClaimDiff
                    {
                        ClaimName = key,
                        PreviousValue = oldValue,
                        CurrentValue = newValue,
                        DiffType = ClaimDiffType.Modified
                    });
                }
            }
            else
            {
                diffs.Add(new ClaimDiff
                {
                    ClaimName = key,
                    PreviousValue = oldValue,
                    CurrentValue = null,
                    DiffType = ClaimDiffType.Removed
                });
            }
        }

        foreach (var (key, newValue) in current)
        {
            if (!previous.ContainsKey(key))
            {
                diffs.Add(new ClaimDiff
                {
                    ClaimName = key,
                    PreviousValue = null,
                    CurrentValue = newValue,
                    DiffType = ClaimDiffType.Added
                });
            }
        }

        return diffs;
    }
}

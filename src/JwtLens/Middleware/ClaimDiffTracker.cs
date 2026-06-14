using JwtLens.Models;

namespace JwtLens.Middleware;

/// <summary>
/// Tracks claim differences between successive tokens for the same subject.
/// </summary>
public sealed class ClaimDiffTracker
{
    private readonly object _lock = new();
    private readonly Dictionary<string, Dictionary<string, object?>> _lastClaimsBySubject = new();

    public List<ClaimDiff> ComputeDiffs(JwtEvent evt)
    {
        var payload = evt.Payload;
        if (!payload.TryGetValue("sub", out var subObj) || subObj == null)
        {
            return new List<ClaimDiff>();
        }

        var subject = subObj.ToString()!;

        lock (_lock)
        {
            if (!_lastClaimsBySubject.TryGetValue(subject, out var previousClaims))
            {
                // First token for this subject — store and return no diffs
                _lastClaimsBySubject[subject] = new Dictionary<string, object?>(payload);
                return new List<ClaimDiff>();
            }

            var diffs = new List<ClaimDiff>();

            // Check for modified or removed claims
            foreach (var kvp in previousClaims)
            {
                if (payload.TryGetValue(kvp.Key, out var currentValue))
                {
                    if (!ValuesEqual(kvp.Value, currentValue))
                    {
                        diffs.Add(new ClaimDiff
                        {
                            ClaimName = kvp.Key,
                            DiffType = ClaimDiffType.Modified,
                            PreviousValue = kvp.Value?.ToString(),
                            CurrentValue = currentValue?.ToString()
                        });
                    }
                }
                else
                {
                    diffs.Add(new ClaimDiff
                    {
                        ClaimName = kvp.Key,
                        DiffType = ClaimDiffType.Removed,
                        PreviousValue = kvp.Value?.ToString(),
                        CurrentValue = null
                    });
                }
            }

            // Check for added claims
            foreach (var kvp in payload)
            {
                if (!previousClaims.ContainsKey(kvp.Key))
                {
                    diffs.Add(new ClaimDiff
                    {
                        ClaimName = kvp.Key,
                        DiffType = ClaimDiffType.Added,
                        PreviousValue = null,
                        CurrentValue = kvp.Value?.ToString()
                    });
                }
            }

            // Update stored claims
            _lastClaimsBySubject[subject] = new Dictionary<string, object?>(payload);

            return diffs;
        }
    }

    /// <summary>Clears all tracked claim state.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _lastClaimsBySubject.Clear();
        }
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal);
    }
}

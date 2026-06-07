using Xunit;
using JwtLens.Analysis;
using JwtLens.Models;

namespace JwtLens.Tests;

public sealed class ClaimDiffTrackerTests
{
    [Fact]
    public void TrackAndDiff_FirstToken_ReturnsEmptyDiffs()
    {
        var tracker = new ClaimDiffTracker();
        var payload = new Dictionary<string, string>
        {
            ["sub"] = "user1",
            ["role"] = "admin"
        };

        var diffs = tracker.TrackAndDiff("user1", payload);

        Assert.Empty(diffs);
    }

    [Fact]
    public void TrackAndDiff_SamePayload_ReturnsEmptyDiffs()
    {
        var tracker = new ClaimDiffTracker();
        var payload = new Dictionary<string, string>
        {
            ["sub"] = "user1",
            ["role"] = "admin"
        };

        tracker.TrackAndDiff("user1", payload);
        var diffs = tracker.TrackAndDiff("user1", new Dictionary<string, string>(payload));

        Assert.Empty(diffs);
    }

    [Fact]
    public void TrackAndDiff_ModifiedClaim_ReturnsModifiedDiff()
    {
        var tracker = new ClaimDiffTracker();
        var payload1 = new Dictionary<string, string> { ["sub"] = "user1", ["role"] = "admin" };
        var payload2 = new Dictionary<string, string> { ["sub"] = "user1", ["role"] = "user" };

        tracker.TrackAndDiff("user1", payload1);
        var diffs = tracker.TrackAndDiff("user1", payload2);

        Assert.Single(diffs);
        Assert.Equal("role", diffs[0].ClaimName);
        Assert.Equal(ClaimDiffType.Modified, diffs[0].DiffType);
        Assert.Equal("admin", diffs[0].PreviousValue);
        Assert.Equal("user", diffs[0].NewValue);
    }

    [Fact]
    public void TrackAndDiff_AddedClaim_ReturnsAddedDiff()
    {
        var tracker = new ClaimDiffTracker();
        var payload1 = new Dictionary<string, string> { ["sub"] = "user1" };
        var payload2 = new Dictionary<string, string> { ["sub"] = "user1", ["role"] = "admin" };

        tracker.TrackAndDiff("user1", payload1);
        var diffs = tracker.TrackAndDiff("user1", payload2);

        Assert.Single(diffs);
        Assert.Equal("role", diffs[0].ClaimName);
        Assert.Equal(ClaimDiffType.Added, diffs[0].DiffType);
        Assert.Null(diffs[0].PreviousValue);
        Assert.Equal("admin", diffs[0].NewValue);
    }

    [Fact]
    public void TrackAndDiff_RemovedClaim_ReturnsRemovedDiff()
    {
        var tracker = new ClaimDiffTracker();
        var payload1 = new Dictionary<string, string> { ["sub"] = "user1", ["role"] = "admin" };
        var payload2 = new Dictionary<string, string> { ["sub"] = "user1" };

        tracker.TrackAndDiff("user1", payload1);
        var diffs = tracker.TrackAndDiff("user1", payload2);

        Assert.Single(diffs);
        Assert.Equal("role", diffs[0].ClaimName);
        Assert.Equal(ClaimDiffType.Removed, diffs[0].DiffType);
        Assert.Equal("admin", diffs[0].PreviousValue);
        Assert.Null(diffs[0].NewValue);
    }

    [Fact]
    public void TrackAndDiff_NullSubject_ReturnsEmptyDiffs()
    {
        var tracker = new ClaimDiffTracker();
        var payload = new Dictionary<string, string> { ["role"] = "admin" };

        tracker.TrackAndDiff(null, payload);
        var diffs = tracker.TrackAndDiff(null, payload);

        Assert.Empty(diffs);
    }

    [Fact]
    public void TrackAndDiff_DifferentSubjects_TrackedIndependently()
    {
        var tracker = new ClaimDiffTracker();
        var payload1 = new Dictionary<string, string> { ["sub"] = "user1", ["role"] = "admin" };
        var payload2 = new Dictionary<string, string> { ["sub"] = "user2", ["role"] = "user" };

        tracker.TrackAndDiff("user1", payload1);
        var diffs = tracker.TrackAndDiff("user2", payload2);

        Assert.Empty(diffs); // First time for user2
    }

    [Fact]
    public void Clear_ResetsAllState()
    {
        var tracker = new ClaimDiffTracker();
        var payload = new Dictionary<string, string> { ["sub"] = "user1", ["role"] = "admin" };

        tracker.TrackAndDiff("user1", payload);
        tracker.Clear();

        var diffs = tracker.TrackAndDiff("user1", new Dictionary<string, string> { ["sub"] = "user1", ["role"] = "user" });
        Assert.Empty(diffs); // Should be treated as first token after clear
    }
}

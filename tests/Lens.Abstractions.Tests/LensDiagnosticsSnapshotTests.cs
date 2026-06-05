using Lens.Abstractions;
using Xunit;

namespace Lens.Abstractions.Tests;

public class LensDiagnosticsSnapshotTests
{
    [Fact]
    public void Constructor_SetsPackageId()
    {
        var snapshot = new LensDiagnosticsSnapshot("JwtLens");

        Assert.Equal("JwtLens", snapshot.PackageId);
    }

    [Fact]
    public void Constructor_ThrowsForNullPackageId()
    {
        Assert.Throws<ArgumentNullException>(() => new LensDiagnosticsSnapshot(null!));
    }

    [Fact]
    public void Timestamp_DefaultsToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var snapshot = new LensDiagnosticsSnapshot("TestLens");
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(snapshot.Timestamp, before, after);
    }

    [Fact]
    public void Data_DefaultsToEmptyDictionary()
    {
        var snapshot = new LensDiagnosticsSnapshot("TestLens");

        Assert.NotNull(snapshot.Data);
        Assert.Empty(snapshot.Data);
    }

    [Fact]
    public void EventCount_DefaultsToZero()
    {
        var snapshot = new LensDiagnosticsSnapshot("TestLens");

        Assert.Equal(0, snapshot.EventCount);
    }
}

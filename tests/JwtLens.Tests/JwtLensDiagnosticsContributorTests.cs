using Xunit;
using JwtLens.Storage;
using Lens.Abstractions;
using JwtLens.Models;
using Moq;

namespace JwtLens.Tests;

public sealed class JwtLensDiagnosticsContributorTests
{
    [Fact]
    public void Metadata_ReturnsExpectedValues()
    {
        var store = new Mock<IJwtEventStore>();
        store.Setup(s => s.GetAll()).Returns(Array.Empty<CapturedJwt>());
        var contributor = new JwtLensDiagnosticsContributor(store.Object);

        Assert.Equal("JwtLens", contributor.Metadata.PackageId);
        Assert.Equal("JWT Lens", contributor.Metadata.DisplayName);
        Assert.NotNull(contributor.Metadata.Description);
    }

    [Fact]
    public void GetLatestSnapshot_NoEvents_ReturnsNull()
    {
        var store = new Mock<IJwtEventStore>();
        store.Setup(s => s.GetAll()).Returns(Array.Empty<CapturedJwt>());
        var contributor = new JwtLensDiagnosticsContributor(store.Object);

        Assert.Null(contributor.GetLatestSnapshot());
    }

    [Fact]
    public void GetLatestSnapshot_WithEvents_ReturnsValidSnapshot()
    {
        var events = new List<CapturedJwt>
        {
            new()
            {
                Direction = TokenDirection.Inbound,
                Algorithm = "RS256",
                Subject = "user1",
                IsExpired = true,
                AlgorithmWarnings = []
            },
            new()
            {
                Direction = TokenDirection.Outbound,
                Algorithm = "HS256",
                Subject = "user2",
                IsExpiringSoon = true,
                AlgorithmWarnings = [new JwtAlgorithmWarning("HS256", WarningSeverity.Warning, "weak")]
            }
        };

        var store = new Mock<IJwtEventStore>();
        store.Setup(s => s.GetAll()).Returns(events);
        store.Setup(s => s.TotalCaptured).Returns(10);

        var contributor = new JwtLensDiagnosticsContributor(store.Object);
        var snapshot = contributor.GetLatestSnapshot();

        Assert.NotNull(snapshot);
        Assert.Equal("JwtLens", snapshot.PackageId);
        Assert.Equal(10, snapshot.EventCount);
        Assert.Equal("1", snapshot.Data["ExpiredTokens"]);
        Assert.Equal("1", snapshot.Data["ExpiringSoonTokens"]);
        Assert.Equal("1", snapshot.Data["TokensWithAlgorithmWarnings"]);
        Assert.Equal("HS256", snapshot.Data["LatestTokenAlgorithm"]);
    }
}

using Lens.Abstractions;
using Xunit;

namespace Lens.Abstractions.Tests;

public class LensPackageMetadataTests
{
    [Fact]
    public void Constructor_SetsRequiredProperties()
    {
        var metadata = new LensPackageMetadata("JwtLens", "JWT Lens");

        Assert.Equal("JwtLens", metadata.PackageId);
        Assert.Equal("JWT Lens", metadata.DisplayName);
    }

    [Fact]
    public void Constructor_ThrowsForNullPackageId()
    {
        Assert.Throws<ArgumentNullException>(() => new LensPackageMetadata(null!, "display"));
    }

    [Fact]
    public void Constructor_ThrowsForNullDisplayName()
    {
        Assert.Throws<ArgumentNullException>(() => new LensPackageMetadata("id", null!));
    }

    [Fact]
    public void OptionalProperties_DefaultToNull()
    {
        var metadata = new LensPackageMetadata("TestLens", "Test Lens");

        Assert.Null(metadata.Description);
        Assert.Null(metadata.Version);
    }

    [Fact]
    public void OptionalProperties_CanBeSet()
    {
        var metadata = new LensPackageMetadata("TestLens", "Test Lens")
        {
            Description = "A test lens",
            Version = "0.1.0"
        };

        Assert.Equal("A test lens", metadata.Description);
        Assert.Equal("0.1.0", metadata.Version);
    }
}

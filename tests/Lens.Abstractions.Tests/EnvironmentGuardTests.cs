using Lens.Abstractions;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace Lens.Abstractions.Tests;

public class EnvironmentGuardTests
{
    [Fact]
    public void IsAllowed_ReturnsTrue_ForDevelopmentByDefault()
    {
        var env = CreateEnvironment("Development");

        Assert.True(EnvironmentGuard.IsAllowed(env));
    }

    [Fact]
    public void IsAllowed_ReturnsFalse_ForProductionByDefault()
    {
        var env = CreateEnvironment("Production");

        Assert.False(EnvironmentGuard.IsAllowed(env));
    }

    [Fact]
    public void IsAllowed_ReturnsFalse_ForStagingByDefault()
    {
        var env = CreateEnvironment("Staging");

        Assert.False(EnvironmentGuard.IsAllowed(env));
    }

    [Fact]
    public void IsAllowed_ReturnsTrue_WhenEnvironmentInCustomList()
    {
        var env = CreateEnvironment("Staging");

        Assert.True(EnvironmentGuard.IsAllowed(env, ["Development", "Staging"]));
    }

    [Fact]
    public void IsAllowed_IsCaseInsensitive()
    {
        var env = CreateEnvironment("development");

        Assert.True(EnvironmentGuard.IsAllowed(env));
    }

    [Fact]
    public void IsAllowed_ThrowsArgumentNullException_ForNullEnvironment()
    {
        Assert.Throws<ArgumentNullException>(() => EnvironmentGuard.IsAllowed(null!));
    }

    [Fact]
    public void IsAllowed_UsesDefaultAllowedEnvironments_WhenEmptyListProvided()
    {
        var env = CreateEnvironment("Development");

        // Empty list falls back to default
        Assert.True(EnvironmentGuard.IsAllowed(env, []));
    }

    private static IHostEnvironment CreateEnvironment(string name)
    {
        var mock = new Mock<IHostEnvironment>();
        mock.Setup(e => e.EnvironmentName).Returns(name);
        return mock.Object;
    }
}

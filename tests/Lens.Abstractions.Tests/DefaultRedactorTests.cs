using Lens.Abstractions;
using Xunit;

namespace Lens.Abstractions.Tests;

public class DefaultRedactorTests
{
    [Fact]
    public void Redact_ReturnsPlaceholder_ForNonNullValue()
    {
        var redactor = new DefaultRedactor(["Authorization"]);

        var result = redactor.Redact("******");

        Assert.Equal(DefaultRedactor.RedactedPlaceholder, result);
    }

    [Fact]
    public void Redact_ReturnsEmptyString_ForNull()
    {
        var redactor = new DefaultRedactor();

        var result = redactor.Redact(null);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void IsSensitive_ReturnsTrue_ForConfiguredKey()
    {
        var redactor = new DefaultRedactor(["Authorization", "Cookie"]);

        Assert.True(redactor.IsSensitive("Authorization"));
        Assert.True(redactor.IsSensitive("authorization")); // case-insensitive
        Assert.True(redactor.IsSensitive("Cookie"));
    }

    [Fact]
    public void IsSensitive_ReturnsFalse_ForUnconfiguredKey()
    {
        var redactor = new DefaultRedactor(["Authorization"]);

        Assert.False(redactor.IsSensitive("Content-Type"));
    }

    [Fact]
    public void IsSensitive_ReturnsFalse_WhenNoKeysConfigured()
    {
        var redactor = new DefaultRedactor();

        Assert.False(redactor.IsSensitive("anything"));
    }
}

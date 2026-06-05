using Xunit;
using JwtLens.Analysis;

namespace JwtLens.Tests;

public sealed class JwtDecoderTests
{
    [Fact]
    public void Decode_ValidToken_ReturnsSuccess()
    {
        var token = TestJwtHelper.CreateToken();

        var result = JwtDecoder.Decode(token);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Header);
        Assert.NotEmpty(result.Payload);
        Assert.Equal("RS256", result.Algorithm);
        Assert.True(result.HasSignature);
    }

    [Fact]
    public void Decode_TokenWithoutSignature_ReturnsHasSignatureFalse()
    {
        var header = new Dictionary<string, object> { ["alg"] = "none", ["typ"] = "JWT" };
        var token = TestJwtHelper.CreateToken(header: header, includeSignature: false);

        var result = JwtDecoder.Decode(token);

        Assert.True(result.Success);
        Assert.False(result.HasSignature);
        Assert.Equal("none", result.Algorithm);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Decode_NullOrEmpty_ReturnsFailed(string? token)
    {
        var result = JwtDecoder.Decode(token);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Decode_InvalidStructure_ReturnsFailed()
    {
        var result = JwtDecoder.Decode("not.a.valid.jwt.token");

        Assert.False(result.Success);
        Assert.Contains("segment", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decode_InvalidBase64_ReturnsFailed()
    {
        var result = JwtDecoder.Decode("!!!invalid!!!.!!!base64!!!.sig");

        Assert.False(result.Success);
    }

    [Fact]
    public void Decode_ParsesAllClaimTypes()
    {
        var payload = new Dictionary<string, object>
        {
            ["sub"] = "user123",
            ["admin"] = true,
            ["level"] = 42,
            ["name"] = "Test"
        };

        var token = TestJwtHelper.CreateToken(payload: payload);
        var result = JwtDecoder.Decode(token);

        Assert.True(result.Success);
        Assert.Equal("user123", result.Payload["sub"]);
        Assert.Equal("true", result.Payload["admin"]);
        Assert.Equal("42", result.Payload["level"]);
    }

    [Fact]
    public void ExtractBearerToken_ValidBearerHeader_ReturnsToken()
    {
        var testJwt = TestJwtHelper.CreateToken();
        var headerValue = "Bearer " + testJwt;
        var token = JwtDecoder.ExtractBearerToken(headerValue);

        Assert.NotNull(token);
        Assert.Equal(testJwt, token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Basic dXNlcjpwYXNz")]
    [InlineData("Bearer ")]
    [InlineData("Bearer")]
    public void ExtractBearerToken_InvalidInput_ReturnsNull(string? input)
    {
        var token = JwtDecoder.ExtractBearerToken(input);

        Assert.Null(token);
    }

    [Fact]
    public void ExtractBearerToken_CaseInsensitive()
    {
        var token = JwtDecoder.ExtractBearerToken("bearer sometoken");

        Assert.Equal("sometoken", token);
    }
}

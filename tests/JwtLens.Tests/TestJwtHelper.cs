using System.Text;
using System.Text.Json;

namespace JwtLens.Tests;

/// <summary>
/// Helper for generating test JWT tokens without external dependencies.
/// </summary>
internal static class TestJwtHelper
{
    public static string CreateToken(
        Dictionary<string, object>? header = null,
        Dictionary<string, object>? payload = null,
        bool includeSignature = true)
    {
        header ??= new Dictionary<string, object>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT"
        };

        payload ??= new Dictionary<string, object>
        {
            ["sub"] = "user123",
            ["name"] = "Test User",
            ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
        };

        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);

        var headerEncoded = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadEncoded = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        if (!includeSignature)
            return $"{headerEncoded}.{payloadEncoded}.";

        var signature = Base64UrlEncode(Encoding.UTF8.GetBytes("fake-signature"));
        return $"{headerEncoded}.{payloadEncoded}.{signature}";
    }

    public static string CreateExpiredToken(TimeSpan expiredAgo)
    {
        var payload = new Dictionary<string, object>
        {
            ["sub"] = "user123",
            ["name"] = "Test User",
            ["iat"] = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds(),
            ["exp"] = DateTimeOffset.UtcNow.Subtract(expiredAgo).ToUnixTimeSeconds()
        };

        return CreateToken(payload: payload);
    }

    public static string CreateExpiringSoonToken(TimeSpan expiresIn)
    {
        var payload = new Dictionary<string, object>
        {
            ["sub"] = "user123",
            ["name"] = "Test User",
            ["iat"] = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds(),
            ["exp"] = DateTimeOffset.UtcNow.Add(expiresIn).ToUnixTimeSeconds()
        };

        return CreateToken(payload: payload);
    }

    public static string CreateTokenWithAlgorithm(string algorithm)
    {
        var header = new Dictionary<string, object>
        {
            ["alg"] = algorithm,
            ["typ"] = "JWT"
        };

        return CreateToken(header: header);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}

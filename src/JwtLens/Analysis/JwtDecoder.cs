using System.Text;
using System.Text.Json;
using JwtLens.Models;

namespace JwtLens.Analysis;

/// <summary>
/// Decodes JWT tokens by splitting on '.' and Base64Url-decoding the header and payload.
/// Does not validate signatures.
/// </summary>
public static class JwtDecoder
{
    /// <summary>
    /// Decodes a JWT token string into its header and payload components.
    /// </summary>
    /// <param name="token">The raw JWT string.</param>
    /// <returns>A <see cref="JwtDecodeResult"/> containing the decoded data or error information.</returns>
    public static JwtDecodeResult Decode(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return JwtDecodeResult.Failed("Token is null or empty.");

        var parts = token.Split('.');
        if (parts.Length < 2 || parts.Length > 3)
            return JwtDecodeResult.Failed($"Invalid JWT structure: expected 2 or 3 segments, got {parts.Length}.");

        var header = DecodeSegment(parts[0]);
        if (header is null)
            return JwtDecodeResult.Failed("Failed to decode JWT header segment.");

        var payload = DecodeSegment(parts[1]);
        if (payload is null)
            return JwtDecodeResult.Failed("Failed to decode JWT payload segment.");

        var hasSignature = parts.Length == 3 && !string.IsNullOrEmpty(parts[2]);

        header.TryGetValue("alg", out var algorithm);

        return new JwtDecodeResult
        {
            Success = true,
            Header = header,
            Payload = payload,
            HasSignature = hasSignature,
            Algorithm = algorithm
        };
    }

    /// <summary>
    /// Extracts a bearer token from an Authorization header value.
    /// </summary>
    /// <param name="authorizationHeaderValue">The full Authorization header value.</param>
    /// <returns>The token string, or null if not a valid Bearer header.</returns>
    public static string? ExtractBearerToken(string? authorizationHeaderValue)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeaderValue))
            return null;

        const string bearerPrefix = "Bearer ";
        if (!authorizationHeaderValue.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var token = authorizationHeaderValue[bearerPrefix.Length..].Trim();
        return string.IsNullOrEmpty(token) ? null : token;
    }

    private static Dictionary<string, string>? DecodeSegment(string segment)
    {
        try
        {
            var bytes = Base64UrlDecode(segment);
            var json = Encoding.UTF8.GetString(bytes);

            using var doc = JsonDocument.Parse(json);
            var result = new Dictionary<string, string>();

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => string.Empty,
                    _ => property.Value.GetRawText()
                };
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var base64 = input.Replace('-', '+').Replace('_', '/');

        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return Convert.FromBase64String(base64);
    }
}

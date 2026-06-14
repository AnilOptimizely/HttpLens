using System.Text;
using System.Text.Json;
using JwtLens.Models;

namespace JwtLens.Middleware;

/// <summary>
/// Decodes JWT tokens and produces JwtEvent instances.
/// </summary>
internal static class JwtDecoder
{
    private static readonly HashSet<string> WeakAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        "HS256", "HS384", "HS512"
    };

    private static readonly HashSet<string> NoneAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        "none"
    };

    private static readonly HashSet<string> SensitiveClaims = new(StringComparer.OrdinalIgnoreCase)
    {
        "email", "phone_number", "address", "birthdate",
        "phone", "credit_card", "ssn", "social_security_number"
    };

    private static readonly TimeSpan ExpiringSoonThreshold = TimeSpan.FromMinutes(5);

    public static JwtEvent Decode(string token, string direction)
    {
        var segments = token.Split('.');

        if (segments.Length < 2)
        {
            return new JwtEvent
            {
                DecodedSuccessfully = false,
                Direction = direction,
                DecodeError = "Invalid JWT structure: expected at least 2 segments",
                HasSignature = false
            };
        }

        bool hasSignature = segments.Length >= 3 && !string.IsNullOrEmpty(segments[2]);

        // Decode header
        Dictionary<string, object?>? headerClaims;
        try
        {
            headerClaims = DecodeSegment(segments[0]);
        }
        catch (Exception ex)
        {
            return new JwtEvent
            {
                DecodedSuccessfully = false,
                Direction = direction,
                DecodeError = $"Failed to decode header: {ex.Message}",
                HasSignature = hasSignature
            };
        }

        // Decode payload
        Dictionary<string, object?> payloadClaims;
        try
        {
            payloadClaims = DecodeSegment(segments[1]);
        }
        catch (Exception ex)
        {
            return new JwtEvent
            {
                DecodedSuccessfully = false,
                Direction = direction,
                DecodeError = $"Failed to decode payload: {ex.Message}",
                HasSignature = hasSignature
            };
        }

        var algorithm = headerClaims.TryGetValue("alg", out var alg) ? alg?.ToString() : null;
        var warnings = GetAlgorithmWarnings(algorithm);

        // Expiry analysis
        bool isExpired = false;
        bool isExpiringSoon = false;
        DateTimeOffset? expiresAt = null;

        if (payloadClaims.TryGetValue("exp", out var expValue) && expValue != null)
        {
            if (long.TryParse(expValue.ToString(), out var expUnix))
            {
                expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix);
                var now = DateTimeOffset.UtcNow;
                isExpired = expiresAt < now;
                isExpiringSoon = !isExpired && (expiresAt - now) < ExpiringSoonThreshold;
            }
        }

        // Redact sensitive claims
        var redactedPayload = RedactClaims(payloadClaims);

        return new JwtEvent
        {
            DecodedSuccessfully = true,
            Algorithm = algorithm,
            Direction = direction,
            AlgorithmWarnings = warnings,
            IsExpired = isExpired,
            IsExpiringSoon = isExpiringSoon,
            ExpiresAt = expiresAt,
            HasSignature = hasSignature,
            Payload = redactedPayload
        };
    }

    private static Dictionary<string, object?> DecodeSegment(string segment)
    {
        var json = Base64UrlDecode(segment);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
            ?? throw new InvalidOperationException("Decoded segment was null");

        var result = new Dictionary<string, object?>();
        foreach (var kvp in dict)
        {
            result[kvp.Key] = ConvertJsonElement(kvp.Value);
        }
        return result;
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private static string Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        var bytes = Convert.FromBase64String(padded);
        return Encoding.UTF8.GetString(bytes);
    }

    private static List<AlgorithmWarning> GetAlgorithmWarnings(string? algorithm)
    {
        var warnings = new List<AlgorithmWarning>();
        if (string.IsNullOrEmpty(algorithm)) return warnings;

        if (NoneAlgorithms.Contains(algorithm))
        {
            warnings.Add(new AlgorithmWarning
            {
                Severity = "Critical",
                Message = $"Algorithm '{algorithm}' provides no signature verification"
            });
        }
        else if (WeakAlgorithms.Contains(algorithm))
        {
            warnings.Add(new AlgorithmWarning
            {
                Severity = "Warning",
                Message = $"Algorithm '{algorithm}' uses symmetric signing which may be inappropriate for distributed systems"
            });
        }

        return warnings;
    }

    private static Dictionary<string, object?> RedactClaims(Dictionary<string, object?> claims)
    {
        var result = new Dictionary<string, object?>();
        foreach (var kvp in claims)
        {
            if (SensitiveClaims.Contains(kvp.Key))
            {
                result[kvp.Key] = "[REDACTED]";
            }
            else
            {
                result[kvp.Key] = kvp.Value;
            }
        }
        return result;
    }
}

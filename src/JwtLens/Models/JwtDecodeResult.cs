namespace JwtLens.Models;

/// <summary>
/// Represents the result of decoding a JWT token.
/// </summary>
public sealed class JwtDecodeResult
{
    /// <summary>Whether decoding was successful.</summary>
    public bool Success { get; init; }

    /// <summary>Error message if decoding failed.</summary>
    public string? Error { get; init; }

    /// <summary>Decoded header claims.</summary>
    public Dictionary<string, string> Header { get; init; } = new();

    /// <summary>Decoded payload claims.</summary>
    public Dictionary<string, string> Payload { get; init; } = new();

    /// <summary>Whether the token has a non-empty signature segment.</summary>
    public bool HasSignature { get; init; }

    /// <summary>The algorithm from the header (alg claim).</summary>
    public string? Algorithm { get; init; }

    /// <summary>
    /// Creates a failed decode result with the given error message.
    /// </summary>
    public static JwtDecodeResult Failed(string error) => new()
    {
        Success = false,
        Error = error
    };
}

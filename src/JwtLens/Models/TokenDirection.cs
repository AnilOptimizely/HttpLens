namespace JwtLens.Models;

/// <summary>
/// Represents the direction from which a JWT was captured.
/// </summary>
public enum TokenDirection
{
    /// <summary>Token was found in an inbound request.</summary>
    Inbound,

    /// <summary>Token was found in an outbound HttpClient request.</summary>
    Outbound
}

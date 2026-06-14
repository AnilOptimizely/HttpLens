using System.Text.Json.Serialization;

namespace JwtLens.Models;

/// <summary>
/// Represents the type of change detected in a claim between successive tokens.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClaimDiffType
{
    /// <summary>A claim was added that did not exist previously.</summary>
    Added = 0,

    /// <summary>A claim was removed that existed previously.</summary>
    Removed = 1,

    /// <summary>A claim value was modified from a previous value.</summary>
    Modified = 2
}

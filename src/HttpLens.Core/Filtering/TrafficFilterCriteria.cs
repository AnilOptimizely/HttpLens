namespace HttpLens.Core.Filtering;

/// <summary>
/// Immutable criteria for server-side filtering of traffic records.
/// All filters are optional — <c>null</c> or empty values mean "no filter".
/// </summary>
/// <param name="Method">Exact match on HTTP method (case-insensitive). Example: "GET".</param>
/// <param name="Status">Prefix match on the stringified response status code. Example: "4" matches 400, 404, 429.</param>
/// <param name="Host">Substring match on the request URI. Example: "github.com".</param>
/// <param name="Search">Case-insensitive substring match on the request URI. Example: "api".</param>
public sealed record TrafficFilterCriteria(
    string? Method = null,
    string? Status = null,
    string? Host = null,
    string? Search = null)
{
    /// <summary>Returns <c>true</c> when no filter criteria are specified.</summary>
    public bool IsEmpty =>
        string.IsNullOrEmpty(Method) &&
        string.IsNullOrEmpty(Status) &&
        string.IsNullOrEmpty(Host) &&
        string.IsNullOrEmpty(Search);
}

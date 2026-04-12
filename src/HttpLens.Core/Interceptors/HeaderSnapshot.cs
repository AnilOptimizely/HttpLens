using System.Net.Http.Headers;

namespace HttpLens.Core.Interceptors;

/// <summary>Merges one or more <see cref="HttpHeaders"/> collections into a single dictionary.</summary>
internal static class HeaderSnapshot
{
    /// <summary>
    /// Captures all headers from the supplied (possibly null) header collections
    /// and merges them into one case-insensitive dictionary.
    /// </summary>
    public static Dictionary<string, string[]> Capture(params HttpHeaders?[] headerCollections)
    {
        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var headers in headerCollections)
        {
            if (headers is null) continue;

            foreach (var (name, values) in headers)
            {
                result[name] = [.. values];
            }
        }

        return result;
    }
}

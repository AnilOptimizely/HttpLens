using System.Text.RegularExpressions;

namespace HttpLens.Core.Interceptors;

/// <summary>
/// Determines whether a request URL should be captured based on include/exclude glob patterns.
/// Exclude patterns take precedence over include patterns.
/// </summary>
public static class UrlPatternMatcher
{
    /// <summary>
    /// Returns <c>true</c> if the given <paramref name="requestUri"/> should be captured
    /// based on the provided exclude and include glob patterns.
    /// </summary>
    /// <param name="requestUri">The full request URL to evaluate.</param>
    /// <param name="excludePatterns">
    /// Glob patterns. If the URL matches ANY pattern, it is NOT captured.
    /// Takes precedence over <paramref name="includePatterns"/>.
    /// </param>
    /// <param name="includePatterns">
    /// Glob patterns. When non-empty, ONLY URLs matching at least one pattern are captured.
    /// </param>
    /// <returns><c>true</c> if the request should be captured; <c>false</c> otherwise.</returns>
    public static bool ShouldCapture(
        string requestUri,
        IReadOnlyList<string> excludePatterns,
        IReadOnlyList<string> includePatterns)
    {
        // Exclude takes precedence — if ANY exclude pattern matches, skip capture.
        for (var i = 0; i < excludePatterns.Count; i++)
        {
            if (GlobMatches(requestUri, excludePatterns[i]))
                return false;
        }

        // If no include patterns are configured, capture everything (that wasn't excluded).
        if (includePatterns.Count == 0)
            return true;

        // At least one include pattern must match.
        for (var i = 0; i < includePatterns.Count; i++)
        {
            if (GlobMatches(requestUri, includePatterns[i]))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Tests whether <paramref name="input"/> matches the glob <paramref name="pattern"/>.
    /// The <c>*</c> wildcard matches any sequence of characters. Matching is case-insensitive.
    /// </summary>
    internal static bool GlobMatches(string input, string pattern)
    {
        // Convert glob pattern to regex: escape everything except *, then replace * with .*
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
    }
}

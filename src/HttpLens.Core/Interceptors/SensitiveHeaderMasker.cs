namespace HttpLens.Core.Interceptors;

/// <summary>Masks values of sensitive headers before storing traffic records.</summary>
public static class SensitiveHeaderMasker
{
    private const string MaskChars = "••••••••";

    /// <summary>
    /// Returns a new dictionary where values of sensitive headers are masked.
    /// </summary>
    /// <param name="headers">The original headers dictionary.</param>
    /// <param name="sensitiveHeaderNames">Case-insensitive set of header names to mask.</param>
    public static Dictionary<string, string[]> Mask(
        Dictionary<string, string[]> headers,
        HashSet<string> sensitiveHeaderNames)
    {
        var result = new Dictionary<string, string[]>(headers.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (name, values) in headers)
        {
            if (sensitiveHeaderNames.Contains(name))
            {
                result[name] = values.Select(MaskValue).ToArray();
            }
            else
            {
                result[name] = values;
            }
        }

        return result;
    }

    private static string MaskValue(string value)
    {
        if (value.Length <= 8)
            return MaskChars;

        return value[..4] + MaskChars + value[^4..];
    }
}

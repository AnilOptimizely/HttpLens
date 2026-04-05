using HttpLens.Core.Models;

namespace HttpLens.Core.Export;

/// <summary>Exports an <see cref="HttpTrafficRecord"/> as a cURL command.</summary>
public static class CurlExporter
{
    /// <summary>Generates a valid cURL command that reproduces the captured request.</summary>
    public static string Export(HttpTrafficRecord record)
    {
        var parts = new List<string>
        {
            $"curl -X {record.RequestMethod} '{EscapeSingleQuotes(record.RequestUri)}'"
        };

        foreach (var (name, values) in record.RequestHeaders)
        {
            var value = string.Join(", ", values);
            parts.Add($"-H '{EscapeSingleQuotes(name)}: {EscapeSingleQuotes(value)}'");
        }

        if (!string.IsNullOrEmpty(record.RequestBody))
        {
            parts.Add($"-d '{EscapeSingleQuotes(record.RequestBody)}'");
        }

        return string.Join(" \\\n  ", parts);
    }

    private static string EscapeSingleQuotes(string value)
        => value.Replace("'", "'\\''");
}

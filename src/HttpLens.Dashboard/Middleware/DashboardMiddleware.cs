using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace HttpLens.Dashboard.Middleware;

/// <summary>Serves the embedded static dashboard files.</summary>
internal static class DashboardMiddleware
{
    private static readonly Assembly _assembly = typeof(DashboardMiddleware).Assembly;

    private static readonly Dictionary<string, string> _mimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".html", "text/html; charset=utf-8" },
        { ".js",   "application/javascript" },
        { ".css",  "text/css" },
        { ".map",  "application/json" },
    };

    /// <summary>Tries to serve an embedded resource. Returns false when the resource isn't found.</summary>
    public static async Task<bool> TryServeResourceAsync(string resourcePath, HttpContext context)
    {
        var stream = _assembly.GetManifestResourceStream(resourcePath);
        if (stream is null) return false;

        var ext = Path.GetExtension(resourcePath);
        context.Response.ContentType = _mimeTypes.TryGetValue(ext, out var mime)
            ? mime
            : "application/octet-stream";

        await using (stream)
            await stream.CopyToAsync(context.Response.Body);

        return true;
    }

    /// <summary>Lists all manifest resource names that are part of the embedded wwwroot.</summary>
    public static IEnumerable<string> GetResourceNames() =>
        _assembly.GetManifestResourceNames()
                 .Where(n => n.StartsWith("HttpLens.Dashboard.wwwroot.", StringComparison.Ordinal));

    /// <summary>Returns the resource name for <c>index.html</c>.</summary>
    public static string IndexHtmlResourceName =>
        "HttpLens.Dashboard.wwwroot.index.html";
}

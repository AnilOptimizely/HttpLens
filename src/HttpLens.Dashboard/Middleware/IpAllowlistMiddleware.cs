using System.Net;
using System.Net.Sockets;
using HttpLens.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace HttpLens.Dashboard.Middleware;

/// <summary>
/// Middleware that restricts access to HttpLens routes by client IP address.
/// Supports exact IPv4/IPv6 matching and CIDR range notation.
/// When <see cref="HttpLensOptions.AllowedIpRanges"/> is empty, all IPs are allowed.
/// IPv4-mapped IPv6 addresses (e.g., <c>::ffff:127.0.0.1</c>) are normalised to their
/// IPv4 representation before comparison.
/// </summary>
internal sealed class IpAllowlistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _dashboardPath;

    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="dashboardPath">The base URL path of the HttpLens dashboard.</param>
    public IpAllowlistMiddleware(RequestDelegate next, string dashboardPath)
    {
        _next = next;
        _dashboardPath = dashboardPath;
    }

    /// <summary>Processes the request, enforcing IP allowlist for HttpLens routes.</summary>
    public async Task InvokeAsync(HttpContext context, IOptionsMonitor<HttpLensOptions> options)
    {
        var allowedRanges = options.CurrentValue.AllowedIpRanges;

        // Empty allowlist means all IPs are permitted.
        if (allowedRanges.Count == 0)
        {
            await _next(context);
            return;
        }

        // Only protect HttpLens routes.
        if (!context.Request.Path.StartsWithSegments(_dashboardPath))
        {
            await _next(context);
            return;
        }

        var remoteIp = context.Connection.RemoteIpAddress;

        if (remoteIp is null || !IsIpAllowed(remoteIp, allowedRanges))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":"Access denied"}""");
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Determines whether <paramref name="remoteIp"/> is permitted by the
    /// <paramref name="allowedRanges"/> list.  Each entry may be a plain IP
    /// address or a CIDR range (e.g., <c>10.0.0.0/8</c>).
    /// </summary>
    internal static bool IsIpAllowed(IPAddress remoteIp, IReadOnlyList<string> allowedRanges)
    {
        // Normalise IPv4-mapped IPv6 (::ffff:a.b.c.d) → IPv4.
        var ip = remoteIp.IsIPv4MappedToIPv6 ? remoteIp.MapToIPv4() : remoteIp;

        foreach (var entry in allowedRanges)
        {
            if (entry.Contains('/'))
            {
                if (IsInCidrRange(ip, entry))
                    return true;
            }
            else
            {
                if (IPAddress.TryParse(entry, out var parsed))
                {
                    var parsedNorm = parsed.IsIPv4MappedToIPv6 ? parsed.MapToIPv4() : parsed;
                    if (ip.Equals(parsedNorm))
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="ip"/> falls within the
    /// CIDR range specified by <paramref name="cidr"/> (e.g., <c>10.0.0.0/8</c>).
    /// </summary>
    private static bool IsInCidrRange(IPAddress ip, string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2) return false;

        if (!IPAddress.TryParse(parts[0], out var network)) return false;
        if (!int.TryParse(parts[1], out var prefixLength)) return false;

        // Normalise network address if IPv4-mapped.
        if (network.IsIPv4MappedToIPv6)
            network = network.MapToIPv4();

        // Address families must match.
        if (ip.AddressFamily != network.AddressFamily) return false;

        var ipBytes = ip.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();

        if (ipBytes.Length != networkBytes.Length) return false;

        var totalBits = ipBytes.Length * 8;
        if (prefixLength < 0 || prefixLength > totalBits) return false;

        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        // Check full bytes.
        for (var i = 0; i < fullBytes; i++)
        {
            if (ipBytes[i] != networkBytes[i])
                return false;
        }

        // Check partial byte if any.
        if (remainingBits > 0 && fullBytes < ipBytes.Length)
        {
            var mask = (byte)(0xFF << (8 - remainingBits));
            if ((ipBytes[fullBytes] & mask) != (networkBytes[fullBytes] & mask))
                return false;
        }

        return true;
    }
}

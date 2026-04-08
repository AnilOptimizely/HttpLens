using HttpLens.Core.Export;
using HttpLens.Core.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HttpLens.Dashboard.Api;

/// <summary>Minimal API endpoints that expose stored traffic records as JSON.</summary>
public static class TrafficApiEndpoints
{
    /// <summary>
    /// Maps the traffic JSON endpoints under <c>{basePath}/api</c>.
    /// All endpoints are excluded from OpenAPI/Swagger descriptions.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="basePath">The base URL path of the HttpLens dashboard.</param>
    /// <param name="authorizationPolicy">
    /// Optional ASP.NET Core authorization policy name to apply to all API endpoints.
    /// When <see langword="null"/>, no authorization is required.
    /// </param>
    public static void MapHttpLensApi(
        this IEndpointRouteBuilder endpoints,
        string basePath,
        string? authorizationPolicy = null)
    {
        var apiGroup = endpoints.MapGroup($"{basePath}/api");

        // Apply ASP.NET Core authorization policy to entire API group when configured.
        if (!string.IsNullOrEmpty(authorizationPolicy))
            apiGroup.RequireAuthorization(authorizationPolicy);

        // GET /api/traffic?skip=0&take=100
        apiGroup.MapGet("/traffic", (ITrafficStore store, int skip = 0, int take = 100) =>
        {
            var all = store.GetAll()
                           .OrderByDescending(r => r.Timestamp)
                           .ToList();
            var page = all.Skip(skip).Take(take).ToList();
            return Results.Ok(new { total = all.Count, records = page });
        }).ExcludeFromDescription();

        // GET /api/traffic/{id:guid}
        apiGroup.MapGet("/traffic/{id:guid}", (ITrafficStore store, Guid id) =>
        {
            var record = store.GetById(id);
            return record is null ? Results.NotFound() : Results.Ok(record);
        }).ExcludeFromDescription();

        // DELETE /api/traffic
        apiGroup.MapDelete("/traffic", (ITrafficStore store) =>
        {
            store.Clear();
            return Results.NoContent();
        }).ExcludeFromDescription();

        // GET /api/traffic/retrygroup/{groupId:guid}
        apiGroup.MapGet("/traffic/retrygroup/{groupId:guid}", (ITrafficStore store, Guid groupId) =>
        {
            var records = store.GetByRetryGroupId(groupId);
            return Results.Ok(records);
        }).ExcludeFromDescription();

        // GET /api/traffic/{id:guid}/export/curl
        apiGroup.MapGet("/traffic/{id:guid}/export/curl", (ITrafficStore store, Guid id) =>
        {
            var record = store.GetById(id);
            if (record is null) return Results.NotFound();
            return Results.Text(CurlExporter.Export(record), "text/plain");
        }).ExcludeFromDescription();

        // GET /api/traffic/{id:guid}/export/csharp
        apiGroup.MapGet("/traffic/{id:guid}/export/csharp", (ITrafficStore store, Guid id) =>
        {
            var record = store.GetById(id);
            if (record is null) return Results.NotFound();
            return Results.Text(CSharpExporter.Export(record), "text/plain");
        }).ExcludeFromDescription();

        // GET /api/traffic/export/har?ids=guid1,guid2,...
        apiGroup.MapGet("/traffic/export/har", (ITrafficStore store, string? ids) =>
        {
            IReadOnlyList<HttpLens.Core.Models.HttpTrafficRecord> records;
            if (string.IsNullOrEmpty(ids))
            {
                records = store.GetAll();
            }
            else
            {
                var guidList = ids.Split(',')
                    .Select(s => Guid.TryParse(s.Trim(), out var g) ? g : (Guid?)null)
                    .Where(g => g.HasValue)
                    .Select(g => g!.Value)
                    .ToList();
                records = guidList
                    .Select(store.GetById)
                    .Where(r => r is not null)
                    .ToList()!;
            }
            return Results.Text(HarExporter.Export(records), "application/json");
        }).ExcludeFromDescription();
    }
}

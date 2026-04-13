# HttpLens Security Guide

HttpLens follows a **"secure by default for production, zero-config for development"** philosophy. All security layers are opt-in — existing users who don't configure security see zero behavior change.

## Security Architecture

```
Request → AllowedEnvironments (registration-time)
        → IsEnabled Guard (404)
        → IP Allowlist (403)
        → API Key Auth (401)
        → ASP.NET Core Authorization Policy (401/403)
        → Endpoint Handler
```

Each layer short-circuits — if `IsEnabled = false`, the IP check never runs; if the IP check fails, the API key check never runs; and so on.

## Security Layers

### Master Switch (`IsEnabled`)

Controls whether HttpLens is active at all. When `false`, the delegating handler becomes a pass-through (no traffic is captured) and all dashboard/API routes return 404.

`IsEnabled` is backed by `IOptionsMonitor<HttpLensOptions>`, so it reloads at runtime when `reloadOnChange: true` is set on the configuration source. Changing the value in `appsettings.json` takes effect immediately — no restart required.

**`appsettings.Development.json`:**

```json
{
  "HttpLens": { "IsEnabled": true }
}
```

**`appsettings.Production.json`:**

```json
{
  "HttpLens": { "IsEnabled": false }
}
```

**`Program.cs`:**

```csharp
builder.Services.AddHttpLens(options =>
    builder.Configuration.GetSection("HttpLens").Bind(options));
```

---

### Environment Guard (`AllowedEnvironments`)

`AllowedEnvironments` is evaluated **once at registration time** using the `AddHttpLens(IHostEnvironment, ...)` overload. When the current environment is not in the list, `AddHttpLens` returns immediately without registering any services — zero overhead, zero routes.

```csharp
builder.Services.AddHttpLens(builder.Environment, options =>
{
    options.AllowedEnvironments.AddRange(["Development", "Staging"]);
});
```

Because `ITrafficStore` is not registered when the environment is excluded, guard `MapHttpLensDashboard()` with a null check:

```csharp
if (app.Services.GetService<ITrafficStore>() != null)
    app.MapHttpLensDashboard();
```

> **Note:** `AllowedEnvironments` is a registration-time check only. Changing it at runtime (e.g. via `appsettings.json` reload) has no effect — the app must be restarted to re-evaluate it.

---

### API Key (`ApiKey`)

When `ApiKey` is set to a non-empty string, every request to the dashboard and API must include the correct key either as:

- The `X-HttpLens-Key` request header (checked first), or
- The `?key=` query parameter (fallback).

Requests that omit or provide the wrong key receive a `401 Unauthorized` response with a JSON error body.

**`Program.cs`:**

```csharp
builder.Services.AddHttpLens(options =>
{
    options.ApiKey = "my-secret-key";
});
```

**Access the dashboard:**

```
/_httplens?key=my-secret-key
```

The embedded SPA automatically reads `?key=` from the page URL on load, stores it in `sessionStorage`, and injects it as an `X-HttpLens-Key` header on every subsequent API call. If a `401` response is received, the UI surfaces a clear message guiding the user to provide the correct key.

> **Note:** `ApiKey` uses `string.IsNullOrEmpty` — setting it to an empty string is treated the same as `null` (no authentication required).

`ApiKey` supports runtime reloading via `IOptionsMonitor<T>`.

---

### IP Allowlist (`AllowedIpRanges`)

Restricts dashboard access to a list of IP addresses or CIDR ranges. When empty (the default), all IP addresses are allowed.

Supported formats:

| Format | Example |
|---|---|
| Exact IPv4 | `127.0.0.1` |
| Exact IPv6 | `::1` |
| IPv4 CIDR | `10.0.0.0/8`, `192.168.1.0/24` |
| IPv6 CIDR | `::1/128` |

IPv4-mapped IPv6 addresses (e.g. `::ffff:127.0.0.1`) are automatically normalised to their IPv4 equivalents before matching, so adding `127.0.0.1` to the allowlist covers both `127.0.0.1` and `::ffff:127.0.0.1`.

```csharp
builder.Services.AddHttpLens(options =>
{
    options.AllowedIpRanges.AddRange(["127.0.0.1", "::1", "10.0.0.0/8", "192.168.1.0/24"]);
});
```

Requests from IPs not in the allowlist receive `403 Forbidden`.

`AllowedIpRanges` supports runtime reloading via `IOptionsMonitor<T>`.

---

### Authorization Policy (`AuthorizationPolicy`)

Applies a named ASP.NET Core authorization policy to all HttpLens routes. The policy must be registered in the host application via `AddAuthorization()` before `MapHttpLensDashboard()` is called.

```csharp
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(/* configure JWT */);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("HttpLensAccess", policy =>
        policy.RequireRole("Admin"));
});

builder.Services.AddHttpLens(options =>
{
    options.AuthorizationPolicy = "HttpLensAccess";
});
```

When the policy is not satisfied, ASP.NET Core returns `401 Unauthorized` or `403 Forbidden` depending on whether the user is authenticated.

> **Note:** `AuthorizationPolicy` is read at `MapHttpLensDashboard()` call time and is not reloaded at runtime.

---

## Layered Security Combinations

### Development only (environment guard)

Automatically skips all registration in Production — zero overhead:

```csharp
builder.Services.AddHttpLens(builder.Environment, options =>
{
    options.AllowedEnvironments.AddRange(["Development"]);
});

if (app.Services.GetService<ITrafficStore>() != null)
    app.MapHttpLensDashboard();
```

### API key + IP allowlist (shared environments)

Protect a staging dashboard accessible only from the office network:

```csharp
builder.Services.AddHttpLens(options =>
{
    options.ApiKey = "staging-secret";
    options.AllowedIpRanges.AddRange(["10.0.0.0/8", "192.168.0.0/16"]);
});
```

### Full stack (all layers combined)

```csharp
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(/* configure JWT */);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("HttpLensAccess", policy =>
        policy.RequireRole("Admin"));
});

builder.Services.AddHttpLens(builder.Environment, options =>
{
    options.AllowedEnvironments.AddRange(["Development", "Staging"]);
    options.ApiKey = "my-secret";
    options.AllowedIpRanges.AddRange(["127.0.0.1", "10.0.0.0/8"]);
    options.AuthorizationPolicy = "HttpLensAccess";
});

if (app.Services.GetService<ITrafficStore>() != null)
    app.MapHttpLensDashboard();
```

### `appsettings.json` binding pattern

Store all options in configuration so they can be changed without redeploying:

```json
{
  "HttpLens": {
    "IsEnabled": true,
    "ApiKey": "my-secret-key",
    "AllowedEnvironments": ["Development", "Staging"],
    "AllowedIpRanges": ["127.0.0.1", "::1", "10.0.0.0/8"]
  }
}
```

```csharp
// Read AllowedEnvironments before AddHttpLens so the env check sees the correct value
var allowedEnvs = builder.Configuration
    .GetSection("HttpLens:AllowedEnvironments")
    .Get<List<string>>() ?? [];

builder.Services.AddHttpLens(builder.Environment, opts =>
{
    builder.Configuration.GetSection("HttpLens").Bind(opts);
    opts.AllowedEnvironments = allowedEnvs;
});
```

---

## Middleware Execution Order

Security checks are applied automatically inside `MapHttpLensDashboard()`. No `UseMiddleware` calls are needed in your `Program.cs`:

| Order | Middleware | Trigger condition | Response |
|---|---|---|---|
| 1 | `EnabledGuardMiddleware` | `IsEnabled = false` | 404 Not Found |
| 2 | `IpAllowlistMiddleware` | Client IP not in `AllowedIpRanges` | 403 Forbidden |
| 3 | `ApiKeyAuthMiddleware` | Key missing or wrong | 401 Unauthorized (JSON body) |
| 4 | Authorization policy | Policy not satisfied | 401 / 403 (ASP.NET Core) |
| 5 | Endpoint handler | — | 200 OK |

Earlier layers short-circuit — if `IsEnabled = false`, the IP check never runs.

---

## Runtime Configuration

Options backed by `IOptionsMonitor<T>` reload automatically when the underlying `IConfiguration` changes (requires `reloadOnChange: true` on the config source):

| Option | Reload support |
|---|---|
| `IsEnabled` | ✅ Runtime reload |
| `ApiKey` | ✅ Runtime reload |
| `AllowedIpRanges` | ✅ Runtime reload |
| `AllowedEnvironments` | ❌ Registration-time only |
| `AuthorizationPolicy` | ❌ `MapHttpLensDashboard()` time only |

To enable runtime reload for JSON configuration:

```csharp
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
```

---

## Frontend API Key Handling

`traffic-api.service.ts` (the embedded SPA's API service) handles API key propagation transparently:

1. **On load** — reads `?key=` from the current page URL.
2. **Stores** the key in `sessionStorage` under `httplens_api_key`.
3. **Injects** the key as an `X-HttpLens-Key` header on every `fetch` call to the API.
4. **On 401** — surfaces a clear error message in the UI, guiding the user to reload the page with `?key=<your-key>`.

This means users only need to pass the key once in the URL; subsequent navigation within the SPA works without repeating it.

---

## Testing Security

Use `TestServer` and `host.GetTestClient()` for integration tests:

```csharp
private static IHostBuilder CreateHost(Action<HttpLensOptions>? configure = null)
{
    return new HostBuilder()
        .ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddHttpLens(configure);
            });
            web.Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapHttpLensDashboard());
            });
        });
}

[Fact]
public async Task ApiKey_MissingKey_Returns401()
{
    using var host = await CreateHost(o => o.ApiKey = "secret").StartAsync();
    var client = host.GetTestClient();
    var response = await client.GetAsync("/_httplens");
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    await host.StopAsync();
}
```

For IP allowlist tests, set `RemoteIpAddress` on the test server connection:

```csharp
var testServer = host.GetTestServer();
testServer.BaseAddress = new Uri("http://localhost");
var context = await testServer.SendAsync(ctx =>
{
    ctx.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.100");
    ctx.Request.Method = "GET";
    ctx.Request.Path = "/_httplens";
});
Assert.Equal(403, context.Response.StatusCode);
```

The test suite ships with 135 automated tests covering all security layers, layered combinations, middleware execution order, and edge cases.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| Dashboard returns 404 | `IsEnabled = false` or `AllowedEnvironments` excludes current env | Check `IsEnabled` in options/config; verify `AllowedEnvironments` includes the current `ASPNETCORE_ENVIRONMENT` value |
| Dashboard returns 401 | `ApiKey` is configured but not provided | Add `?key=<your-key>` to the URL, or set the `X-HttpLens-Key` request header |
| Dashboard returns 403 | `AllowedIpRanges` is configured and client IP is not in the list | Add the client IP (or its CIDR range) to `AllowedIpRanges`; note that localhost may appear as `::1` over IPv6 |
| App crashes at startup with policy error | `AuthorizationPolicy` names a policy that is not registered | Ensure `AddAuthorization(o => o.AddPolicy(...))` is called before `MapHttpLensDashboard()` |
| Options not reloading at runtime | `reloadOnChange` is not enabled on the config source | Add `reloadOnChange: true` to `AddJsonFile(...)` |

# JwtLens

> Inspect JWT tokens flowing through your ASP.NET Core app — decode, analyze, and diagnose without touching your auth logic.

JwtLens is part of the [Lens family for .NET](https://github.com/AnilOptimizely/HttpLens): focused, local-first diagnostics packages.

## Features

- **Inbound capture** — intercepts JWT bearer tokens from incoming HTTP requests via middleware
- **Outbound capture** — intercepts JWTs on outgoing `HttpClient` calls via delegating handler
- **Decode without validation** — pure Base64Url + System.Text.Json decoding, zero external JWT dependencies
- **Algorithm analysis** — flags `alg: "none"` (critical) and configurable weak algorithms
- **Expiry analysis** — detects expired tokens and tokens within a configurable warning threshold
- **Claim diffs** — tracks changes between consecutive tokens per subject
- **Redaction** — sensitive claims redacted by default via shared `IRedactor`
- **Dashboard integration** — implements `ILensDiagnosticsContributor` for HttpLens dashboard

## Installation

```shell
dotnet add package JwtLens
```

## Quick Start

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register JwtLens services
builder.Services.AddJwtLens();

var app = builder.Build();

// Add inbound JWT capture middleware
app.UseJwtLens();

app.Run();
```

## Configuration

```csharp
builder.Services.AddJwtLens(options =>
{
    options.WarnIfExpiresWithin = TimeSpan.FromMinutes(5);
    options.FlagWeakAlgorithms = true;
    options.TrackClaimDiffs = true;
    options.MaxStoredEvents = 200;
});
```

## Environment Safety

JwtLens supports environment-aware registration:

```csharp
builder.Services.AddJwtLens(builder.Environment, options =>
{
    options.AllowedEnvironments.AddRange(["Development", "Staging"]);
});
```

## License

MIT

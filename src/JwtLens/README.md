# JwtLens

JwtLens captures inbound and outbound JWT bearer tokens in ASP.NET Core apps and surfaces local diagnostics such as claims, expiry, and algorithm warnings.

## Install

```shell
dotnet add package JwtLens
```

## Minimal setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddJwtLens();

var app = builder.Build();

app.UseJwtLens();

app.Run();
```

See the [Lens family architecture](https://github.com/AnilOptimizely/HttpLens/blob/main/docs/lens-family-architecture.md) for the shared package conventions.

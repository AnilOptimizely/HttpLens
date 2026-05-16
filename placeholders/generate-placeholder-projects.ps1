$packages = @{
  "JwtLens"       = "JWT inspection and diagnostics for .NET applications."
  "GcLens"        = "Garbage collection and allocation diagnostics for .NET applications."
  "AsyncLens"     = "Async flow and Task diagnostics for .NET applications."
  "SqlLens"       = "SQL query diagnostics and database visibility for .NET applications."
  "LogLens"       = "Local-first structured log diagnostics for .NET applications."
  "ConfigLens"    = "Configuration provenance and diagnostics for .NET applications."
  "CacheLens"     = "Cache hit, miss, eviction, and stampede diagnostics for .NET applications."
  "DiLens"        = "Dependency injection graph and lifetime diagnostics for .NET applications."
  "AiLens"        = "AI, LLM, token, prompt, and model-call diagnostics for .NET applications."
  "MailLens"      = "Email capture and diagnostics for .NET applications."
  "CookieLens"    = "Cookie security and Set-Cookie diagnostics for ASP.NET Core applications."
  "BlazorLens"    = "Blazor render and component diagnostics for .NET applications."
  "SignalRLens"   = "SignalR connection and message diagnostics for .NET applications."
  "MigrationLens" = "Database migration and schema drift diagnostics for .NET applications."
}

foreach ($package in $packages.Keys) {
  $dir = "packages/$package"
  New-Item -ItemType Directory -Force -Path $dir | Out-Null

  $description = $packages[$package]

  @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId>$package</PackageId>
    <Title>$package</Title>
    <Description>[Placeholder] $description Part of the Lens family for .NET.</Description>
  </PropertyGroup>
</Project>
"@ | Set-Content "$dir/$package.csproj"

  @"
namespace $package;

/// <summary>
/// Placeholder type for the $package package.
/// </summary>
public sealed class Placeholder
{
}
"@ | Set-Content "$dir/Placeholder.cs"
}
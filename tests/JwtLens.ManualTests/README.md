# JwtLens v0.1 — Manual Testing

## Overview

This directory contains PowerShell scripts for end-to-end manual testing of JwtLens v0.1. The scripts exercise all major features by making HTTP requests to the `SampleJwtLensApi` sample project.

## Prerequisites

- .NET 9 SDK
- PowerShell 7+ (pwsh)
- The `SampleJwtLensApi` sample project running locally

## Quick Start

### 1. Build the solution

```powershell
dotnet build HttpLens.slnx -c Release
```

### 2. Start the sample API

```powershell
cd samples/SampleJwtLensApi
dotnet run --environment Development
# App starts on http://localhost:5000
```

### 3. Run the main test script (in a new terminal)

```powershell
cd tests/JwtLens.ManualTests
./test-jwtlens.ps1 -BaseUrl "http://localhost:5000"
```

## Test Scripts

| Script | Purpose | Categories |
|--------|---------|------------|
| `test-jwtlens.ps1` | Main test runner — all default-options tests | 1 (Inbound), 2 (Outbound), 3 (Redaction), 4 (Claim Diff), 5 (Ring Buffer), 8 (Diagnostics) |
| `test-jwtlens-environment.ps1` | Environment guard tests (requires restart per scenario) | 6 (Environment Guard) |
| `test-jwtlens-options.ps1` | Options toggle tests (requires restart per scenario) | 7 (Options) |
| `helpers/jwt-helpers.ps1` | Shared functions for JWT creation and assertions | Used by all scripts |

## Environment Guard Tests (Category 6)

These tests require restarting the app with different environment variables:

```powershell
# Test with Development (default — JwtLens active):
cd samples/SampleJwtLensApi
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
# Then in another terminal:
./test-jwtlens-environment.ps1 -BaseUrl "http://localhost:5000"

# Test with Production (JwtLens disabled — requires AllowedEnvironments=["Development"]):
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet run
# Then:
./test-jwtlens-environment.ps1 -BaseUrl "http://localhost:5000" -ExpectDisabled
```

## Options Toggle Tests (Category 7)

These tests check specific options. Modify `appsettings.json` and restart the app for each scenario:

```powershell
# Test with IsEnabled=false:
# Edit appsettings.json: "IsEnabled": false
dotnet run
./test-jwtlens-options.ps1 -BaseUrl "http://localhost:5000" -TestScenario "disabled"

# Test with CaptureInboundTokens=false:
# Edit appsettings.json: "CaptureInboundTokens": false
dotnet run
./test-jwtlens-options.ps1 -BaseUrl "http://localhost:5000" -TestScenario "no-inbound"

# Other scenarios: "no-outbound", "no-weak-alg-flag", "custom-expiry-threshold", "custom-weak-algs"
```

## Test Categories

1. **Inbound JWT Capture** — Middleware captures tokens from Authorization headers
2. **Outbound JWT Capture** — DelegatingHandler captures tokens from HttpClient calls
3. **Claim Redaction** — Sensitive claims are redacted in stored events
4. **Claim Diff Tracking** — Changes between consecutive tokens for the same subject are tracked
5. **Ring Buffer Storage** — Event store respects MaxStoredEvents and FIFO eviction
6. **Environment Guard** — JwtLens respects AllowedEnvironments configuration
7. **Options Toggle** — Individual options control specific behaviors
8. **Diagnostics** — ILensDiagnosticsContributor provides metadata and snapshots

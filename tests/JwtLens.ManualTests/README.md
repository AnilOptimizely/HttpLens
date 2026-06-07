# JwtLens v0.1 — Manual Testing

## Overview

This directory contains scripts for end-to-end manual testing of JwtLens v0.1. The scripts exercise all major features by making HTTP requests to the `SampleJwtLensApi` sample project.

## Prerequisites

- .NET 9 SDK
- PowerShell 7+ (pwsh) — for `.ps1` scripts
- bash + curl — for `.sh` scripts
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

### 3. Run the test scripts (in a new terminal)

**Main test runner (PowerShell — comprehensive):**
```powershell
cd tests/JwtLens.ManualTests
./test-jwtlens.ps1 -BaseUrl "http://localhost:5000"
```

**Quick endpoint smoke test (Bash/curl):**
```bash
cd tests/JwtLens.ManualTests
chmod +x test-endpoints.sh
./test-endpoints.sh
# Or with a custom base URL:
./test-endpoints.sh http://localhost:5050
```

**Quick endpoint smoke test (PowerShell):**
```powershell
cd tests/JwtLens.ManualTests
./test-endpoints.ps1
# Or with a custom base URL:
./test-endpoints.ps1 -BaseUrl "http://localhost:5050"
```

## Test Scripts

| Script | Purpose | Categories |
|--------|---------|------------|
| `test-jwtlens.ps1` | Main test runner — all default-options tests | 1 (Inbound), 2 (Outbound), 3 (Redaction), 4 (Claim Diff), 5 (Ring Buffer), 8 (Diagnostics) |
| `test-jwtlens-environment.ps1` | Environment guard tests (requires restart per scenario) | 6 (Environment Guard) |
| `test-jwtlens-options.ps1` | Options toggle tests (requires restart per scenario) | 7 (Options) |
| `test-endpoints.sh` | Quick endpoint smoke test (bash/curl) | All endpoints |
| `test-endpoints.ps1` | Quick endpoint smoke test (PowerShell) | All endpoints |
| `helpers/jwt-helpers.ps1` | Shared functions for JWT creation and assertions | Used by all scripts |

## Endpoint Reference

| # | Method | Endpoint | Purpose |
|---|--------|----------|---------|
| 1 | GET | `/api/test` | Simple endpoint for inbound token testing |
| 2 | GET | `/api/jwt/events` | Returns all stored CapturedJwt events |
| 3 | GET | `/api/jwt/events/count` | Returns `{ count, totalCaptured }` |
| 4 | DELETE | `/api/jwt/events` | Clears the event store |
| 5 | GET | `/api/jwt/diagnostics` | Returns diagnostics metadata and snapshot |
| 6 | GET | `/api/jwt/options` | Returns current JwtLensOptions |
| 7 | GET | `/api/outbound-test?token={jwt}` | Triggers outbound HttpClient call with JWT |

## Manual curl Commands

If you prefer to test individual endpoints manually:

```bash
BASE=http://localhost:5000

# 1. Simple test endpoint
curl -s $BASE/api/test | jq .

# 2. Inbound JWT capture (send a token)
JWT="******"
curl -s -H "Authorization: ******" $BASE/api/test | jq .

# 3. Get all captured events
curl -s $BASE/api/jwt/events | jq .

# 4. Get event count
curl -s $BASE/api/jwt/events/count | jq .

# 5. Clear events
curl -s -X DELETE $BASE/api/jwt/events | jq .

# 6. Diagnostics
curl -s $BASE/api/jwt/diagnostics | jq .

# 7. Current options
curl -s $BASE/api/jwt/options | jq .

# 8. Outbound test (triggers HttpClient call with the given JWT)
curl -s "$BASE/api/outbound-test?token=$JWT" | jq .
```

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

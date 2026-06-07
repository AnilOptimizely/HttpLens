# JwtLens — Endpoint Test Scripts

## Overview

This directory contains scripts to quickly test all SampleJwtLensApi endpoints. Choose the script that matches your environment:

| Script | Platform | Requirements |
|--------|----------|-------------|
| `test-endpoints.sh` | Linux / macOS / WSL | `bash`, `curl` |
| `test-endpoints.ps1` | Windows / Cross-platform | PowerShell 7+ (`pwsh`) |

## Prerequisites

1. **.NET 9 SDK** installed
2. **SampleJwtLensApi** running locally

## Quick Start

### 1. Start the Sample API

```bash
cd samples/SampleJwtLensApi
dotnet run --environment Development
# App starts on http://localhost:5000
```

### 2. Run the test script (in a separate terminal)

**Bash (curl):**
```bash
cd tests/JwtLens.ManualTests
chmod +x test-endpoints.sh
./test-endpoints.sh
# Or with a custom base URL:
./test-endpoints.sh http://localhost:5050
```

**PowerShell:**
```powershell
cd tests/JwtLens.ManualTests
./test-endpoints.ps1
# Or with a custom base URL:
./test-endpoints.ps1 -BaseUrl "http://localhost:5050"
```

## What Gets Tested

| # | Method | Endpoint | Purpose |
|---|--------|----------|---------|
| 1 | GET | `/api/test` | Simple endpoint (no auth) |
| 2 | GET | `/api/test` | Inbound token capture (with ****** |
| 3 | GET | `/api/jwt/events` | Returns all stored CapturedJwt events |
| 4 | GET | `/api/jwt/events/count` | Returns `{ count, totalCaptured }` |
| 5 | DELETE | `/api/jwt/events` | Clears the event store |
| 6 | GET | `/api/jwt/diagnostics` | Returns diagnostics metadata and snapshot |
| 7 | GET | `/api/jwt/options` | Returns current JwtLensOptions |
| 8 | GET | `/api/outbound-test?token={jwt}` | Triggers outbound HttpClient call with JWT |

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

## Expected Output

A successful run looks like:

```
🔍 JwtLens SampleJwtLensApi — Endpoint Tests
   Target: http://localhost:5000
✓ Server is reachable

━━━ Basic Connectivity ━━━
  ✅ PASS: GET /api/test (no auth) (HTTP 200)

━━━ Inbound JWT Capture ━━━
  ✅ PASS: GET /api/test (with ****** (HTTP 200)

━━━ Event Store Endpoints ━━━
  ✅ PASS: GET /api/jwt/events (HTTP 200)
  ✅ PASS: GET /api/jwt/events/count (HTTP 200)
  ✅ PASS: DELETE /api/jwt/events (HTTP 200)

━━━ Diagnostics & Options ━━━
  ✅ PASS: GET /api/jwt/diagnostics (HTTP 200)
  ✅ PASS: GET /api/jwt/options (HTTP 200)

━━━ Outbound JWT Capture ━━━
  ✅ PASS: GET /api/outbound-test?token=JWT (HTTP 200)

════════════════════════════════════════
Results: 8 passed, 0 failed
════════════════════════════════════════
```

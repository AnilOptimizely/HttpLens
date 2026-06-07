# JwtLens v0.1 — Manual Testing Checklist

> **Goal**: Verify every JwtLens feature end-to-end before publishing the NuGet package.
> **Sample project**: `samples/SampleJwtLensApi` — a minimal ASP.NET Core app pre-wired with JwtLens.
> **Test scripts**: `tests/JwtLens.ManualTests/` — PowerShell scripts that automate many of these steps.

---

## Prerequisites

| Requirement | Verify with |
|-------------|-------------|
| .NET 9 SDK | `dotnet --version` → `9.x.x` |
| PowerShell 7+ | `pwsh --version` → `7.x.x` |
| Git | `git --version` |
| A terminal that supports two sessions (or two terminal windows) | — |

---

## Initial Setup (Do This Once)

### Step 1 — Clone and checkout the branch

```powershell
git clone https://github.com/AnilOptimizely/HttpLens.git
cd HttpLens
git checkout copilot/jwtlens-v01-implementation
```

### Step 2 — Build the entire solution

```powershell
dotnet build HttpLens.slnx -c Release
```

**✅ Pass**: Build succeeds with no errors.

### Step 3 — Run the automated unit tests

```powershell
dotnet test HttpLens.slnx -c Release --no-build
```

**✅ Pass**: All 61 tests pass (net9.0 + net10.0).

### Step 4 — Start the SampleJwtLensApi (Terminal 1)

```powershell
cd samples/SampleJwtLensApi
dotnet run --environment Development
```

**✅ Pass**: Console shows `Now listening on: http://localhost:5000` (or similar port). Leave this running.

### Step 5 — Verify the server is reachable (Terminal 2)

```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/test"
```

**✅ Pass**: Returns `{ "message": "OK" }`.

### Step 6 — Verify the default configuration

```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/options"
```

**✅ Pass**: Returns JSON with:

| Field | Expected Value |
|-------|----------------|
| `isEnabled` | `true` |
| `warnIfExpiresWithin` | `"00:05:00"` |
| `trackClaimDiffs` | `true` |
| `flagWeakAlgorithms` | `true` |
| `maxStoredEvents` | `200` |
| `captureOutboundTokens` | `true` |
| `captureInboundTokens` | `true` |
| `sensitiveClaimNames` | `["email", "phone_number", "address", "birthdate"]` |
| `weakAlgorithms` | `["none", "HS256"]` |

---

## SampleJwtLensApi Endpoint Reference

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `GET` | `/api/test` | Simple endpoint for inbound token testing |
| `GET` | `/api/jwt/events` | Returns all stored CapturedJwt events |
| `GET` | `/api/jwt/events/count` | Returns `{ count, totalCaptured }` |
| `DELETE` | `/api/jwt/events` | Clears the event store |
| `GET` | `/api/jwt/diagnostics` | Returns diagnostics metadata and snapshot |
| `GET` | `/api/jwt/options` | Returns current JwtLensOptions |
| `GET` | `/api/outbound-test?token={jwt}` | Triggers outbound HttpClient call with the given JWT |

---

## Helper: Creating Test JWTs in PowerShell

JwtLens doesn't validate signatures — it only decodes and analyzes. You can create unsigned test tokens directly:

```powershell
# Load the shared helpers (from repo root)
. ./tests/JwtLens.ManualTests/helpers/jwt-helpers.ps1

# Create a standard 3-part JWT
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1"; iss = "test"; exp = (Get-UnixTimestamp -OffsetMinutes 60) }

# Create an alg:none JWT (no signature)
$token = New-TestJwtNoSignature -Header @{ alg = "none" } -Payload @{ sub = "user1" }

# Create a 2-segment JWT (no signature part)
$token = New-TestJwtTwoSegments -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1" }

# Get Unix timestamp with offset
$futureExp = Get-UnixTimestamp -OffsetMinutes 60   # 1 hour from now
$pastExp = Get-UnixTimestamp -OffsetMinutes -10     # 10 minutes ago
```

---

## Automated Shortcut

Before going through each step manually, you can run the automated scripts to get a quick pass/fail overview. Then investigate any failures manually.

```powershell
cd tests/JwtLens.ManualTests

# Main tests (Categories 1, 2, 3, 4, 5, 8)
./test-jwtlens.ps1 -BaseUrl "http://localhost:5000"

# Environment guard tests (Category 6) — requires app restart; see Category 6 below
./test-jwtlens-environment.ps1 -BaseUrl "http://localhost:5000"

# Options toggle tests (Category 7) — requires app restart per scenario; see Category 7 below
./test-jwtlens-options.ps1 -BaseUrl "http://localhost:5000" -TestScenario "disabled"
```

---

## Category 1: Inbound JWT Capture (Middleware)

> **What we're testing**: The `JwtLensMiddleware` intercepts `Authorization: ****** headers on inbound requests, decodes the JWT, and stores a `CapturedJwt` event.

### Before each test

```powershell
# Clear the event store
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
```

---

### Test 1.1 — Valid RS256 token is captured and decoded

```powershell
. ./tests/JwtLens.ManualTests/helpers/jwt-helpers.ps1
$exp = Get-UnixTimestamp -OffsetMinutes 60
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1"; iss = "test"; aud = "api"; exp = $exp }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$events = Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events"
$event = $events[-1]
$event | ConvertTo-Json -Depth 5
```

**✅ Pass criteria**:
- [ ] `$event.decodedSuccessfully` is `true`
- [ ] `$event.algorithm` is `"RS256"`
- [ ] `$event.direction` is `"Inbound"`
- [ ] `$event.algorithmWarnings` is empty (RS256 is not weak)
- [ ] `$event.isExpired` is `false`
- [ ] `$event.isExpiringSoon` is `false`

---

### Test 1.2 — HS256 token triggers weak algorithm warning

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$token = New-TestJwt -Header @{ alg = "HS256"; typ = "JWT" } -Payload @{ sub = "user1"; iss = "test" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event | ConvertTo-Json -Depth 5
```

**✅ Pass criteria**:
- [ ] `$event.decodedSuccessfully` is `true`
- [ ] `$event.algorithm` is `"HS256"`
- [ ] `$event.algorithmWarnings` has at least 1 entry
- [ ] `$event.algorithmWarnings[0].severity` is `"Warning"`

---

### Test 1.3 — alg:none triggers critical algorithm warning

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$token = New-TestJwtNoSignature -Header @{ alg = "none" } -Payload @{ sub = "user1" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event | ConvertTo-Json -Depth 5
```

**✅ Pass criteria**:
- [ ] `$event.decodedSuccessfully` is `true`
- [ ] `$event.algorithmWarnings` has at least 1 entry
- [ ] `$event.algorithmWarnings[0].severity` is `"Critical"`

---

### Test 1.4 — Expired token is flagged

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$exp = Get-UnixTimestamp -OffsetMinutes -10
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1"; exp = $exp }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event | ConvertTo-Json -Depth 5
```

**✅ Pass criteria**:
- [ ] `$event.isExpired` is `true`

---

### Test 1.5 — Token expiring soon (within 5-minute threshold)

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$exp = Get-UnixTimestamp -OffsetMinutes 3
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1"; exp = $exp }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event | ConvertTo-Json -Depth 5
```

**✅ Pass criteria**:
- [ ] `$event.isExpiringSoon` is `true`
- [ ] `$event.isExpired` is `false`

---

### Test 1.6 — Token with no expiry claim

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1"; iss = "test" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event | ConvertTo-Json -Depth 5
```

**✅ Pass criteria**:
- [ ] `$event.isExpired` is `false`
- [ ] `$event.isExpiringSoon` is `false`
- [ ] `$event.expiresAt` is `null`

---

### Test 1.7 — Malformed token (1 segment)

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event | ConvertTo-Json -Depth 5
```

**✅ Pass criteria**:
- [ ] `$event.decodedSuccessfully` is `false`
- [ ] `$event.decodeError` contains `"Invalid JWT structure"`

---

### Test 1.8 — No Authorization header → no capture

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
Invoke-RestMethod -Uri "http://localhost:5000/api/test"
$count = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events/count").count
```

**✅ Pass criteria**:
- [ ] `$count` is `0`

---

### Test 1.9 — Basic auth (non-Bearer) → no capture

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "Basic dXNlcjpwYXNz" }
$count = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events/count").count
```

**✅ Pass criteria**:
- [ ] `$count` is `0`

---

### Test 1.10 — Empty ****** → no capture

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "Bearer " }
$count = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events/count").count
```

**✅ Pass criteria**:
- [ ] `$count` is `0`

---

### Test 1.11 — Token with 2 segments (no signature part)

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$token = New-TestJwtTwoSegments -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event | ConvertTo-Json -Depth 5
```

**✅ Pass criteria**:
- [ ] `$event.decodedSuccessfully` is `true`
- [ ] `$event.hasSignature` is `false`

---

### Test 1.12 — Invalid Base64 in header

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$token = "!!!invalid-base64!!!.eyJzdWIiOiJ1c2VyMSJ9.signature"
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event | ConvertTo-Json -Depth 5
```

**✅ Pass criteria**:
- [ ] `$event.decodedSuccessfully` is `false`
- [ ] `$event.decodeError` contains `"Failed to decode"`

---

## Category 2: Outbound JWT Capture (DelegatingHandler)

> **What we're testing**: The `JwtLensDelegatingHandler` intercepts JWTs on outbound `HttpClient` calls made through the named `"OutboundTest"` client.

### Test 2.1 — Outbound call with ****** is captured

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "outbound-user"; iss = "test" }
Invoke-RestMethod -Uri "http://localhost:5000/api/outbound-test?token=$token"
Start-Sleep -Seconds 1
$events = Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events"
$outbound = @($events | Where-Object { $_.direction -eq "Outbound" })
$outbound | ConvertTo-Json -Depth 5
```

**✅ Pass criteria**:
- [ ] At least 1 outbound event exists
- [ ] `$outbound[0].direction` is `"Outbound"`
- [ ] `$outbound[0].decodedSuccessfully` is `true`

> **Note**: The outbound call goes to `httpbin.org/get`. If your network blocks this, the call may fail, but the delegating handler still captures the token *before* sending. Check the event regardless of the HTTP response.

---

### Test 2.2 — Outbound call without token → no capture

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
Invoke-RestMethod -Uri "http://localhost:5000/api/outbound-test"
Start-Sleep -Seconds 1
$count = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events/count").count
```

**✅ Pass criteria**:
- [ ] `$count` is `0`

---

## Category 3: Claim Redaction

> **What we're testing**: Sensitive claims (`email`, `phone_number`, `address`, `birthdate`) are replaced with `"[REDACTED]"` in stored events.

### Test 3.1 — Email claim is redacted

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1"; email = "user@example.com" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event.redactedClaims | ConvertTo-Json
```

**✅ Pass criteria**:
- [ ] `$event.redactedClaims.email` is `"[REDACTED]"` (not `"user@example.com"`)

---

### Test 3.2 — Phone number claim is redacted

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1"; phone_number = "+1234567890" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event.redactedClaims | ConvertTo-Json
```

**✅ Pass criteria**:
- [ ] `$event.redactedClaims.phone_number` is `"[REDACTED]"`

---

### Test 3.3 — Address claim is redacted

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1"; address = "123 Main St" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event.redactedClaims | ConvertTo-Json
```

**✅ Pass criteria**:
- [ ] `$event.redactedClaims.address` is `"[REDACTED]"`

---

### Test 3.4 — Birthdate claim is redacted

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1"; birthdate = "1990-01-15" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event.redactedClaims | ConvertTo-Json
```

**✅ Pass criteria**:
- [ ] `$event.redactedClaims.birthdate` is `"[REDACTED]"`

---

### Test 3.5 — Non-sensitive claims are NOT redacted

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1"; iss = "test"; custom_claim = "visible-value" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event.redactedClaims | ConvertTo-Json
```

**✅ Pass criteria**:
- [ ] `$event.redactedClaims.sub` is `"user1"` (not redacted)
- [ ] `$event.redactedClaims.iss` is `"test"` (not redacted)
- [ ] `$event.redactedClaims.custom_claim` is `"visible-value"` (not redacted)

---

### Test 3.6 — Case-insensitive redaction (EMAIL → redacted)

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1"; EMAIL = "user@example.com" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event.redactedClaims | ConvertTo-Json
```

**✅ Pass criteria**:
- [ ] The `EMAIL` (or `email`) claim value is `"[REDACTED]"`

---

## Category 4: Claim Diff Tracking

> **What we're testing**: When `TrackClaimDiffs` is `true`, JwtLens tracks changes between consecutive tokens for the same `sub` (subject).

### Test 4.1 — First token for a subject → no diffs

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "diffuser1"; role = "admin"; level = "5" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event | ConvertTo-Json -Depth 5
```

**✅ Pass criteria**:
- [ ] `$event.claimDiffs` is empty or `null` (no previous token to compare)

---

### Test 4.2 — Second token with modified claim → diff detected

```powershell
# Don't clear — we need the first token from 4.1 still in the store
$token2 = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "diffuser1"; role = "viewer"; level = "5" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event.claimDiffs | ConvertTo-Json -Depth 5
```

**✅ Pass criteria**:
- [ ] `$event.claimDiffs` contains at least 1 entry
- [ ] One diff shows `role` changed from `"admin"` to `"viewer"`

---

### Test 4.3 — Third token with removed claim → diff detected

```powershell
$token3 = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "diffuser1"; role = "viewer" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event.claimDiffs | ConvertTo-Json -Depth 5
```

**✅ Pass criteria**:
- [ ] `$event.claimDiffs` shows `level` was removed

---

### Test 4.4 — Fourth token with added claim → diff detected

```powershell
$token4 = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "diffuser1"; role = "viewer"; department = "engineering" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event.claimDiffs | ConvertTo-Json -Depth 5
```

**✅ Pass criteria**:
- [ ] `$event.claimDiffs` shows `department` was added

---

### Test 4.5 — Different subject → independent tracking, no diffs

```powershell
$token5 = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "otheruser"; role = "admin" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event | ConvertTo-Json -Depth 5
```

**✅ Pass criteria**:
- [ ] `$event.claimDiffs` is empty (first token for `"otheruser"`)

---

### Test 4.6 — Token with no `sub` claim → no diff tracking

```powershell
$token6 = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ iss = "test"; role = "admin" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event | ConvertTo-Json -Depth 5
```

**✅ Pass criteria**:
- [ ] `$event.claimDiffs` is empty or `null`

---

## Category 5: Ring Buffer Storage

> **What we're testing**: The `InMemoryJwtEventStore` uses a FIFO ring buffer capped at `MaxStoredEvents` (default: 200). Events beyond the limit evict the oldest. `totalCaptured` always increments.

### Test 5.1 — Events stored within capacity

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
# Send 3 tokens
for ($i = 1; $i -le 3; $i++) {
    $token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "bufferuser$i" }
    Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
}
$result = Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events/count"
$result | ConvertTo-Json
```

**✅ Pass criteria**:
- [ ] `$result.count` is `3`
- [ ] `$result.totalCaptured` is `3`

---

### Test 5.2 — Clear and verify

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$result = Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events/count"
$result | ConvertTo-Json
```

**✅ Pass criteria**:
- [ ] `$result.count` is `0`

---

### Test 5.3 — Clear and re-add

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "after-clear" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$result = Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events/count"
$result | ConvertTo-Json
```

**✅ Pass criteria**:
- [ ] `$result.count` is `1`
- [ ] `$result.totalCaptured` is greater than `0`

---

## Category 6: Environment Guard

> **What we're testing**: When `AllowedEnvironments` is configured, JwtLens only activates in those environments. In disallowed environments, it becomes a pass-through (no events captured, no errors).

> ⚠️ **These tests require restarting the SampleJwtLensApi** with different environment settings.

### Test 6.1 — Development environment (default — JwtLens active)

**Setup** (Terminal 1 — stop the running app first with `Ctrl+C`):

```powershell
cd samples/SampleJwtLensApi
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
```

**Test** (Terminal 2):

```powershell
. ./tests/JwtLens.ManualTests/helpers/jwt-helpers.ps1
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "envtest" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$count = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events/count").count
```

**✅ Pass criteria**:
- [ ] `$count` is `1` (JwtLens is active in Development)

---

### Test 6.2 — Production environment with AllowedEnvironments guard

**Setup** (Terminal 1 — stop the app):

First, edit `samples/SampleJwtLensApi/appsettings.json` to add an allowed environments restriction:

```json
{
  "JwtLens": {
    "IsEnabled": true,
    "AllowedEnvironments": ["Development"]
  }
}
```

Then restart:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet run
```

**Test** (Terminal 2):

```powershell
. ./tests/JwtLens.ManualTests/helpers/jwt-helpers.ps1

# 6.2a — Events endpoint still works (returns empty)
$events = Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events"

# 6.2b — Sending a JWT does NOT create an event
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "envtest" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$count = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events/count").count

# 6.2c — Requests still work (no errors)
$response = Invoke-RestMethod -Uri "http://localhost:5000/api/test"
$response | ConvertTo-Json
```

**✅ Pass criteria**:
- [ ] `$count` is `0` (JwtLens is NOT active in Production when AllowedEnvironments = ["Development"])
- [ ] The `/api/test` endpoint still returns `{ "message": "OK" }` (pass-through, no errors)

---

### Test 6.3 — Empty AllowedEnvironments means all environments allowed

**Setup**: Edit `appsettings.json` — remove `AllowedEnvironments` or set to `[]`:

```json
{
  "JwtLens": {
    "IsEnabled": true,
    "AllowedEnvironments": []
  }
}
```

Restart with Production:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet run
```

**Test**:

```powershell
. ./tests/JwtLens.ManualTests/helpers/jwt-helpers.ps1
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "envtest" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$count = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events/count").count
```

**✅ Pass criteria**:
- [ ] `$count` is `1` (empty list = all environments allowed)

**Cleanup**: Restore `appsettings.json` to defaults (remove `AllowedEnvironments`). Restart with `Development`.

---

### You can also use the automated script:

```powershell
# When running in Development (JwtLens active):
./tests/JwtLens.ManualTests/test-jwtlens-environment.ps1 -BaseUrl "http://localhost:5000"

# When running in Production with AllowedEnvironments=["Development"] (JwtLens disabled):
./tests/JwtLens.ManualTests/test-jwtlens-environment.ps1 -BaseUrl "http://localhost:5000" -ExpectDisabled
```

---

## Category 7: Options Toggle

> **What we're testing**: Individual options in `JwtLensOptions` control specific behaviors. Each scenario requires editing `appsettings.json` and restarting the app.

> ⚠️ **Restore `appsettings.json` to defaults after each test and restart the app.**

### Default `appsettings.json` for reference:

```json
{
  "Logging": { "LogLevel": { "Default": "Information" } },
  "JwtLens": {
    "IsEnabled": true,
    "WarnIfExpiresWithin": "00:05:00",
    "TrackClaimDiffs": true,
    "FlagWeakAlgorithms": true,
    "MaxStoredEvents": 200,
    "CaptureOutboundTokens": true,
    "CaptureInboundTokens": true
  }
}
```

---

### Test 7.1 — IsEnabled=false → no events captured

**Setup**: Edit `appsettings.json`:

```json
"JwtLens": { "IsEnabled": false }
```

Restart the app (`dotnet run --environment Development`).

**Test**:

```powershell
. ./tests/JwtLens.ManualTests/helpers/jwt-helpers.ps1
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$count = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events/count").count
```

**✅ Pass criteria**:
- [ ] `$count` is `0`
- [ ] Requests still complete without errors

**Automated**: `./test-jwtlens-options.ps1 -BaseUrl "http://localhost:5000" -TestScenario "disabled"`

---

### Test 7.2 — CaptureInboundTokens=false → no inbound capture

**Setup**: Edit `appsettings.json`:

```json
"JwtLens": { "IsEnabled": true, "CaptureInboundTokens": false, "CaptureOutboundTokens": true }
```

Restart the app.

**Test**:

```powershell
. ./tests/JwtLens.ManualTests/helpers/jwt-helpers.ps1
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$count = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events/count").count
```

**✅ Pass criteria**:
- [ ] `$count` is `0` (inbound capture disabled)

**Automated**: `./test-jwtlens-options.ps1 -BaseUrl "http://localhost:5000" -TestScenario "no-inbound"`

---

### Test 7.3 — CaptureOutboundTokens=false → no outbound capture

**Setup**: Edit `appsettings.json`:

```json
"JwtLens": { "IsEnabled": true, "CaptureInboundTokens": true, "CaptureOutboundTokens": false }
```

Restart the app.

**Test**:

```powershell
. ./tests/JwtLens.ManualTests/helpers/jwt-helpers.ps1
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "outbound-user" }
Invoke-RestMethod -Uri "http://localhost:5000/api/outbound-test?token=$token"
Start-Sleep -Seconds 1
$events = Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events"
$outbound = @($events | Where-Object { $_.direction -eq "Outbound" })
```

**✅ Pass criteria**:
- [ ] `$outbound.Count` is `0` (no outbound events captured)

**Automated**: `./test-jwtlens-options.ps1 -BaseUrl "http://localhost:5000" -TestScenario "no-outbound"`

---

### Test 7.4 — FlagWeakAlgorithms=false → no algorithm warnings

**Setup**: Edit `appsettings.json`:

```json
"JwtLens": { "IsEnabled": true, "FlagWeakAlgorithms": false }
```

Restart the app.

**Test**:

```powershell
. ./tests/JwtLens.ManualTests/helpers/jwt-helpers.ps1
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$token = New-TestJwt -Header @{ alg = "HS256"; typ = "JWT" } -Payload @{ sub = "user1" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event | ConvertTo-Json -Depth 5
```

**✅ Pass criteria**:
- [ ] `$event.algorithmWarnings` is empty (HS256 is NOT flagged when `FlagWeakAlgorithms` is false)

**Automated**: `./test-jwtlens-options.ps1 -BaseUrl "http://localhost:5000" -TestScenario "no-weak-alg-flag"`

---

### Test 7.5 — Custom WarnIfExpiresWithin=10 minutes

**Setup**: Edit `appsettings.json`:

```json
"JwtLens": { "IsEnabled": true, "WarnIfExpiresWithin": "00:10:00" }
```

Restart the app.

**Test**:

```powershell
. ./tests/JwtLens.ManualTests/helpers/jwt-helpers.ps1
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
# Token expires in 8 minutes — outside 5min default but inside 10min custom
$exp = Get-UnixTimestamp -OffsetMinutes 8
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1"; exp = $exp }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event | ConvertTo-Json -Depth 5
```

**✅ Pass criteria**:
- [ ] `$event.isExpiringSoon` is `true` (8 min < 10 min threshold)
- [ ] `$event.isExpired` is `false`

**Automated**: `./test-jwtlens-options.ps1 -BaseUrl "http://localhost:5000" -TestScenario "custom-expiry-threshold"`

---

### Test 7.6 — Custom WeakAlgorithms includes RS256

**Setup**: Edit `appsettings.json`:

```json
"JwtLens": { "IsEnabled": true, "FlagWeakAlgorithms": true, "WeakAlgorithms": ["none", "HS256", "RS256"] }
```

Restart the app.

**Test**:

```powershell
. ./tests/JwtLens.ManualTests/helpers/jwt-helpers.ps1
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$event = (Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/events")[-1]
$event | ConvertTo-Json -Depth 5
```

**✅ Pass criteria**:
- [ ] `$event.algorithmWarnings` has at least 1 entry (RS256 is now considered weak)

**Automated**: `./test-jwtlens-options.ps1 -BaseUrl "http://localhost:5000" -TestScenario "custom-weak-algs"`

**Cleanup**: Restore `appsettings.json` to defaults and restart.

---

## Category 8: Diagnostics

> **What we're testing**: The `JwtLensDiagnosticsContributor` implements `ILensDiagnosticsContributor` and exposes metadata (package ID, display name, version) and runtime snapshots.

### Test 8.1 — Metadata is available

```powershell
$diag = Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/diagnostics"
$diag.metadata | ConvertTo-Json
```

**✅ Pass criteria**:
- [ ] `$diag.metadata.packageId` is `"JwtLens"` (or similar identifier)
- [ ] `$diag.metadata.displayName` is not empty
- [ ] `$diag.metadata.version` is not empty

---

### Test 8.2 — Snapshot before any events

```powershell
Invoke-RestMethod -Method Delete -Uri "http://localhost:5000/api/jwt/events"
$diag = Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/diagnostics"
$diag.snapshot | ConvertTo-Json -Depth 3
```

**✅ Pass criteria**:
- [ ] Snapshot is returned (not null)
- [ ] Event count in snapshot data is `0`

---

### Test 8.3 — Snapshot after capturing events

```powershell
. ./tests/JwtLens.ManualTests/helpers/jwt-helpers.ps1
$token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "diaguser" }
Invoke-RestMethod -Uri "http://localhost:5000/api/test" -Headers @{ "Authorization" = "******" }
$diag = Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/diagnostics"
$diag.snapshot | ConvertTo-Json -Depth 3
```

**✅ Pass criteria**:
- [ ] Snapshot reflects the captured event(s)
- [ ] Event count in snapshot is greater than `0`

---

### Test 8.4 — Snapshot data keys

```powershell
$diag = Invoke-RestMethod -Uri "http://localhost:5000/api/jwt/diagnostics"
$diag.snapshot.data | ConvertTo-Json -Depth 3
```

**✅ Pass criteria**:
- [ ] Snapshot `data` contains meaningful keys (e.g., event counts, storage info)

---

## Final Checklist Summary

| Category | Tests | Status |
|----------|-------|--------|
| 1. Inbound JWT Capture | 1.1–1.12 (12 tests) | ☐ |
| 2. Outbound JWT Capture | 2.1–2.2 (2 tests) | ☐ |
| 3. Claim Redaction | 3.1–3.6 (6 tests) | ☐ |
| 4. Claim Diff Tracking | 4.1–4.6 (6 tests) | ☐ |
| 5. Ring Buffer Storage | 5.1–5.3 (3 tests) | ☐ |
| 6. Environment Guard | 6.1–6.3 (3 scenarios) | ☐ |
| 7. Options Toggle | 7.1–7.6 (6 scenarios) | ☐ |
| 8. Diagnostics | 8.1–8.4 (4 tests) | ☐ |
| **Total** | **42 tests** | |

---

## Cleanup After Testing

1. **Stop the SampleJwtLensApi** (`Ctrl+C` in Terminal 1)
2. **Restore `appsettings.json`** to its default state (undo any Category 6/7 edits)
3. **Unset environment variables**:
   ```powershell
   Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
   ```

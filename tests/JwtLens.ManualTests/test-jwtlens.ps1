#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Main test runner for JwtLens v0.1 manual tests.
    Covers Categories 1 (Inbound), 2 (Outbound), 3 (Redaction), 4 (Claim Diff), 5 (Ring Buffer), 8 (Diagnostics).

.PARAMETER BaseUrl
    Base URL of the running SampleJwtLensApi instance. Default: http://localhost:5000

.PARAMETER VerboseOutput
    Show additional debug output.

.EXAMPLE
    ./test-jwtlens.ps1 -BaseUrl "http://localhost:5000"
#>
param(
    [string]$BaseUrl = "http://localhost:5000",
    [switch]$VerboseOutput
)

$ErrorActionPreference = "Stop"
$script:PassCount = 0
$script:FailCount = 0

# Load helpers
. "$PSScriptRoot/helpers/jwt-helpers.ps1"

function Test-Case {
    param(
        [string]$Name,
        [scriptblock]$Test
    )

    try {
        $result = & $Test
        if ($result -eq $true) {
            Write-Host "  PASS: $Name" -ForegroundColor Green
            $script:PassCount++
        }
        else {
            Write-Host "  FAIL: $Name (returned $result)" -ForegroundColor Red
            $script:FailCount++
        }
    }
    catch {
        Write-Host "  FAIL: $Name - $($_.Exception.Message)" -ForegroundColor Red
        if ($VerboseOutput) {
            Write-Host "    $($_.ScriptStackTrace)" -ForegroundColor DarkGray
        }
        $script:FailCount++
    }
}

# ============================================================
# Pre-flight: verify server is running
# ============================================================
Write-Host ""
Write-Host "JwtLens v0.1 - Manual Test Runner" -ForegroundColor Cyan
Write-Host "Target: $BaseUrl"
Write-Host ""

try {
    Invoke-RestMethod -Uri "$BaseUrl/api/test" -ErrorAction Stop | Out-Null
    Write-Host "Server is reachable" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "Cannot reach server at $BaseUrl - is SampleJwtLensApi running?" -ForegroundColor Red
    exit 1
}

# Clear store before testing
Clear-JwtEvents -BaseUrl $BaseUrl

# ============================================================
# Category 1: Inbound JWT Capture (Middleware)
# ============================================================
Write-Host "--- Category 1: Inbound JWT Capture ---" -ForegroundColor Yellow

Test-Case -Name "1.1 Valid RS256 token" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $exp = Get-UnixTimestamp -OffsetMinutes 60
    $token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1"; iss = "test"; aud = "api"; exp = $exp }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    Assert-True -Actual $event.decodedSuccessfully -Field "DecodedSuccessfully"
    Assert-Equal -Expected "RS256" -Actual $event.algorithm -Field "Algorithm"
    Assert-Equal -Expected "Inbound" -Actual $event.direction -Field "Direction"
    $event.algorithmWarnings.Count -eq 0
}

Test-Case -Name "1.2 Valid HS256 (weak algorithm)" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $token = New-TestJwt -Header @{ alg = "HS256"; typ = "JWT" } -Payload @{ sub = "user1"; iss = "test" }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    Assert-True -Actual $event.decodedSuccessfully -Field "DecodedSuccessfully"
    Assert-Equal -Expected "HS256" -Actual $event.algorithm -Field "Algorithm"
    $event.algorithmWarnings.Count -gt 0 -and $event.algorithmWarnings[0].severity -eq "Warning"
}

Test-Case -Name "1.3 alg:none (critical)" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $token = New-TestJwtNoSignature -Header @{ alg = "none" } -Payload @{ sub = "user1" }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    Assert-True -Actual $event.decodedSuccessfully -Field "DecodedSuccessfully"
    $event.algorithmWarnings.Count -gt 0 -and $event.algorithmWarnings[0].severity -eq "Critical"
}

Test-Case -Name "1.4 Expired token" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $exp = Get-UnixTimestamp -OffsetMinutes -10
    $token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1"; exp = $exp }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    Assert-True -Actual $event.isExpired -Field "IsExpired"
}

Test-Case -Name "1.5 Expiring soon (within 5min threshold)" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $exp = Get-UnixTimestamp -OffsetMinutes 3
    $token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1"; exp = $exp }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    Assert-True -Actual $event.isExpiringSoon -Field "IsExpiringSoon"
    Assert-False -Actual $event.isExpired -Field "IsExpired"
}

Test-Case -Name "1.6 No expiry claim" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1"; iss = "test" }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    Assert-False -Actual $event.isExpired -Field "IsExpired"
    Assert-False -Actual $event.isExpiringSoon -Field "IsExpiringSoon"
    Assert-Null -Actual $event.expiresAt -Field "ExpiresAt"
}

Test-Case -Name "1.7 Malformed token - 1 segment" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $token = "malformedtoken"
    $headers = @{ "Authorization" = "******" }
    Invoke-RestMethod -Uri "$BaseUrl/api/test" -Headers $headers -ErrorAction Stop | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    Assert-False -Actual $event.decodedSuccessfully -Field "DecodedSuccessfully"
    Assert-Contains -Expected "Invalid JWT structure" -Actual $event.decodeError -Field "DecodeError"
}

Test-Case -Name "1.8 No Authorization header (no capture)" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    Invoke-RestMethod -Uri "$BaseUrl/api/test" -ErrorAction Stop | Out-Null
    $count = (Get-JwtEventCount -BaseUrl $BaseUrl).count
    Assert-Equal -Expected 0 -Actual $count -Field "EventCount"
}

Test-Case -Name "1.9 Basic auth - non-Bearer, no capture" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $headers = @{ "Authorization" = "Basic dXNlcjpwYXNz" }
    Invoke-RestMethod -Uri "$BaseUrl/api/test" -Headers $headers -ErrorAction Stop | Out-Null
    $count = (Get-JwtEventCount -BaseUrl $BaseUrl).count
    Assert-Equal -Expected 0 -Actual $count -Field "EventCount"
}

Test-Case -Name "1.10 Empty ****** capture)" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $headers = @{ "Authorization" = "Bearer " }
    Invoke-RestMethod -Uri "$BaseUrl/api/test" -Headers $headers -ErrorAction Stop | Out-Null
    $count = (Get-JwtEventCount -BaseUrl $BaseUrl).count
    Assert-Equal -Expected 0 -Actual $count -Field "EventCount"
}

Test-Case -Name "1.11 Token with 2 segments (no signature)" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $token = New-TestJwtTwoSegments -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "user1" }
    $headers = @{ "Authorization" = "******" }
    Invoke-RestMethod -Uri "$BaseUrl/api/test" -Headers $headers -ErrorAction Stop | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    Assert-True -Actual $event.decodedSuccessfully -Field "DecodedSuccessfully"
    Assert-False -Actual $event.hasSignature -Field "HasSignature"
}

Test-Case -Name "1.12 Token with invalid Base64 in header" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $token = "!!!invalid-base64!!!.eyJzdWIiOiJ1c2VyMSJ9.signature"
    $headers = @{ "Authorization" = "******" }
    Invoke-RestMethod -Uri "$BaseUrl/api/test" -Headers $headers -ErrorAction Stop | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    Assert-False -Actual $event.decodedSuccessfully -Field "DecodedSuccessfully"
    Assert-Contains -Expected "Failed to decode" -Actual $event.decodeError -Field "DecodeError"
}

# ============================================================
# Category 2: Outbound JWT Capture (DelegatingHandler)
# ============================================================
Write-Host ""
Write-Host "--- Category 2: Outbound JWT Capture ---" -ForegroundColor Yellow

Test-Case -Name "2.1 Outbound with ******" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "outbound-user"; iss = "test" }
    Invoke-RestMethod -Uri "$BaseUrl/api/outbound-test?token=$token" -ErrorAction Stop | Out-Null
    Start-Sleep -Milliseconds 500
    $events = Get-JwtEvents -BaseUrl $BaseUrl
    $outboundEvents = @($events | Where-Object { $_.direction -eq "Outbound" })
    $outboundEvents.Count -gt 0
}

Test-Case -Name "2.2 Outbound without token (no capture)" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    Invoke-RestMethod -Uri "$BaseUrl/api/outbound-test" -ErrorAction Stop | Out-Null
    Start-Sleep -Milliseconds 500
    $events = Get-JwtEvents -BaseUrl $BaseUrl
    $outboundEvents = @($events | Where-Object { $_.direction -eq "Outbound" })
    $outboundEvents.Count -eq 0
}

# ============================================================
# Category 3: Claim Redaction
# ============================================================
Write-Host ""
Write-Host "--- Category 3: Claim Redaction ---" -ForegroundColor Yellow

Test-Case -Name "3.1 Redacts email claim" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $token = New-TestJwt -Payload @{ sub = "user1"; email = "test@example.com"; iss = "test" }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    Assert-Equal -Expected "[REDACTED]" -Actual $event.payload.email -Field "payload.email"
    Assert-Equal -Expected "user1" -Actual $event.payload.sub -Field "payload.sub"
}

Test-Case -Name "3.2 Redacts phone_number claim" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $token = New-TestJwt -Payload @{ sub = "user1"; phone_number = "+1234567890" }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    Assert-Equal -Expected "[REDACTED]" -Actual $event.payload.phone_number -Field "payload.phone_number"
}

Test-Case -Name "3.3 Redacts address claim" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $token = New-TestJwt -Payload @{ sub = "user1"; address = "123 Main St" }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    Assert-Equal -Expected "[REDACTED]" -Actual $event.payload.address -Field "payload.address"
}

Test-Case -Name "3.4 Redacts birthdate claim" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $token = New-TestJwt -Payload @{ sub = "user1"; birthdate = "1990-01-01" }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    Assert-Equal -Expected "[REDACTED]" -Actual $event.payload.birthdate -Field "payload.birthdate"
}

Test-Case -Name "3.5 Non-sensitive claims pass through" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $token = New-TestJwt -Payload @{ sub = "user1"; iss = "test"; aud = "api"; role = "admin" }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    Assert-Equal -Expected "user1" -Actual $event.payload.sub -Field "payload.sub"
    Assert-Equal -Expected "test" -Actual $event.payload.iss -Field "payload.iss"
    Assert-Equal -Expected "api" -Actual $event.payload.aud -Field "payload.aud"
    Assert-Equal -Expected "admin" -Actual $event.payload.role -Field "payload.role"
    $true
}

Test-Case -Name "3.7 Case-insensitive redaction (EMAIL)" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $token = New-TestJwt -Payload @{ sub = "user1"; EMAIL = "test@example.com" }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    $emailValue = $event.payload.EMAIL
    if (-not $emailValue) { $emailValue = $event.payload.email }
    Assert-Equal -Expected "[REDACTED]" -Actual $emailValue -Field "payload.EMAIL"
}

# ============================================================
# Category 4: Claim Diff Tracking
# ============================================================
Write-Host ""
Write-Host "--- Category 4: Claim Diff Tracking ---" -ForegroundColor Yellow

Test-Case -Name "4.1 First token for subject (no diffs)" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $token = New-TestJwt -Payload @{ sub = "diffuser1"; role = "viewer"; iss = "test" }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    $event.claimDiffs.Count -eq 0
}

Test-Case -Name "4.2 Second token, modified claim" -Test {
    $token = New-TestJwt -Payload @{ sub = "diffuser1"; role = "admin"; iss = "test" }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    $modDiff = $event.claimDiffs | Where-Object { $_.claimName -eq "role" }
    Assert-NotNull -Actual $modDiff -Field "role diff"
    Assert-Equal -Expected "Modified" -Actual $modDiff.diffType -Field "DiffType"
    $true
}

Test-Case -Name "4.3 Third token, removed claim" -Test {
    $token = New-TestJwt -Payload @{ sub = "diffuser1"; iss = "test" }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    $removedDiff = $event.claimDiffs | Where-Object { $_.claimName -eq "role" }
    Assert-NotNull -Actual $removedDiff -Field "role diff"
    Assert-Equal -Expected "Removed" -Actual $removedDiff.diffType -Field "DiffType"
    $true
}

Test-Case -Name "4.4 Fourth token, added claim" -Test {
    $token = New-TestJwt -Payload @{ sub = "diffuser1"; iss = "test"; department = "eng" }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    $addedDiff = $event.claimDiffs | Where-Object { $_.claimName -eq "department" }
    Assert-NotNull -Actual $addedDiff -Field "department diff"
    Assert-Equal -Expected "Added" -Actual $addedDiff.diffType -Field "DiffType"
    $true
}

Test-Case -Name "4.5 Different subject - independent, no diffs" -Test {
    $token = New-TestJwt -Payload @{ sub = "diffuser2"; role = "viewer" }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    $event.claimDiffs.Count -eq 0
}

Test-Case -Name "4.6 No subject claim (no diffs)" -Test {
    $token = New-TestJwt -Payload @{ iss = "test"; role = "viewer" }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    $event = Get-LastJwtEvent -BaseUrl $BaseUrl
    $event.claimDiffs.Count -eq 0
}

# ============================================================
# Category 5: Ring Buffer Storage
# ============================================================
Write-Host ""
Write-Host "--- Category 5: Ring Buffer Storage ---" -ForegroundColor Yellow

Test-Case -Name "5.1 Normal operation under capacity" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    for ($i = 1; $i -le 5; $i++) {
        $token = New-TestJwt -Payload @{ sub = "bufferuser$i"; iss = "test" }
        Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    }
    $countResult = Get-JwtEventCount -BaseUrl $BaseUrl
    Assert-Equal -Expected 5 -Actual $countResult.count -Field "count"
    Assert-Equal -Expected 5 -Actual $countResult.totalCaptured -Field "totalCaptured"
    $true
}

Test-Case -Name "5.4 Clear store" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $countResult = Get-JwtEventCount -BaseUrl $BaseUrl
    Assert-Equal -Expected 0 -Actual $countResult.count -Field "count"
    Assert-Equal -Expected 0 -Actual $countResult.totalCaptured -Field "totalCaptured"
    $true
}

Test-Case -Name "5.5 Clear and re-add" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    for ($i = 1; $i -le 2; $i++) {
        $token = New-TestJwt -Payload @{ sub = "readduser$i"; iss = "test" }
        Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
    }
    $countResult = Get-JwtEventCount -BaseUrl $BaseUrl
    Assert-Equal -Expected 2 -Actual $countResult.count -Field "count"
    Assert-Equal -Expected 2 -Actual $countResult.totalCaptured -Field "totalCaptured"
    $true
}

# ============================================================
# Category 8: Dashboard Integration (ILensDiagnosticsContributor)
# ============================================================
Write-Host ""
Write-Host "--- Category 8: Diagnostics ---" -ForegroundColor Yellow

Test-Case -Name "8.1 Metadata check" -Test {
    $diag = Invoke-RestMethod -Uri "$BaseUrl/api/jwt/diagnostics" -ErrorAction Stop
    Assert-Equal -Expected "JwtLens" -Actual $diag.metadata.packageId -Field "PackageId"
    Assert-Equal -Expected "JWT Lens" -Actual $diag.metadata.displayName -Field "DisplayName"
    Assert-Equal -Expected "0.1.0-preview.1" -Actual $diag.metadata.version -Field "Version"
    $true
}

Test-Case -Name "8.2 Snapshot before any events" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $diag = Invoke-RestMethod -Uri "$BaseUrl/api/jwt/diagnostics" -ErrorAction Stop
    $null -eq $diag.snapshot
}

Test-Case -Name "8.3 Snapshot after events" -Test {
    Clear-JwtEvents -BaseUrl $BaseUrl
    $expPast = Get-UnixTimestamp -OffsetMinutes -10
    $token1 = New-TestJwt -Payload @{ sub = "diaguser1"; exp = $expPast }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token1 | Out-Null

    $token2 = New-TestJwt -Header @{ alg = "HS256" } -Payload @{ sub = "diaguser2" }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token2 | Out-Null

    $expFuture = Get-UnixTimestamp -OffsetMinutes 60
    $token3 = New-TestJwt -Payload @{ sub = "diaguser3"; exp = $expFuture }
    Send-JwtRequest -BaseUrl $BaseUrl -Token $token3 | Out-Null

    $diag = Invoke-RestMethod -Uri "$BaseUrl/api/jwt/diagnostics" -ErrorAction Stop
    Assert-NotNull -Actual $diag.snapshot -Field "snapshot"
    Assert-Equal -Expected "1" -Actual $diag.snapshot.data.ExpiredTokens -Field "ExpiredTokens"
    Assert-Equal -Expected "1" -Actual $diag.snapshot.data.TokensWithAlgorithmWarnings -Field "TokensWithAlgorithmWarnings"
    $true
}

Test-Case -Name "8.4 Snapshot data keys" -Test {
    $diag = Invoke-RestMethod -Uri "$BaseUrl/api/jwt/diagnostics" -ErrorAction Stop
    $keys = @(
        "StoredEvents",
        "ExpiredTokens",
        "ExpiringSoonTokens",
        "TokensWithAlgorithmWarnings",
        "LatestTokenAlgorithm",
        "LatestTokenSubject",
        "LatestTokenDirection"
    )
    foreach ($key in $keys) {
        if ($null -eq $diag.snapshot.data.$key) {
            throw "Missing key: $key"
        }
    }
    $true
}

# ============================================================
# Results
# ============================================================
Write-Host ""
Write-Host "========================================"
if ($script:FailCount -gt 0) {
    Write-Host "Results: $($script:PassCount) passed, $($script:FailCount) failed" -ForegroundColor Red
}
else {
    Write-Host "Results: $($script:PassCount) passed, $($script:FailCount) failed" -ForegroundColor Green
}
Write-Host "========================================"

if ($script:FailCount -gt 0) { exit 1 } else { exit 0 }

#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Tests all SampleJwtLensApi endpoints to verify they are accessible and functioning.

.DESCRIPTION
    This script calls each endpoint exposed by the SampleJwtLensApi sample project
    and reports success/failure. It is designed for quick smoke testing of the API surface.

.PARAMETER BaseUrl
    Base URL of the running SampleJwtLensApi instance. Default: http://localhost:5000

.EXAMPLE
    ./test-endpoints.ps1
    ./test-endpoints.ps1 -BaseUrl "http://localhost:5050"
#>
param(
    [string]$BaseUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Continue"
$script:PassCount = 0
$script:FailCount = 0

# ── Helper: Generate a minimal test JWT (unsigned, alg:none) ──
function ConvertTo-Base64Url {
    param([string]$InputString)
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($InputString)
    $base64 = [Convert]::ToBase64String($bytes)
    return $base64.Replace('+', '-').Replace('/', '_').TrimEnd('=')
}

function New-TestJwt {
    $header = '{"alg":"none","typ":"JWT"}'
    $payload = '{"sub":"testuser","iss":"test-script","iat":1700000000}'
    $headerB64 = ConvertTo-Base64Url -InputString $header
    $payloadB64 = ConvertTo-Base64Url -InputString $payload
    return "$headerB64.$payloadB64."
}

# ── Helper: Run a test case ──
function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Method = "GET",
        [string]$Endpoint,
        [hashtable]$Headers = @{}
    )
    $uri = "$BaseUrl$Endpoint"
    try {
        $response = Invoke-WebRequest -Uri $uri -Method $Method -Headers $Headers -ErrorAction Stop
        if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
            Write-Host "  ✅ PASS: $Name (HTTP $($response.StatusCode))" -ForegroundColor Green
            $script:PassCount++
            return $response.Content
        } else {
            Write-Host "  ❌ FAIL: $Name (HTTP $($response.StatusCode))" -ForegroundColor Red
            $script:FailCount++
            return $null
        }
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "  ❌ FAIL: $Name (HTTP $statusCode — $($_.Exception.Message))" -ForegroundColor Red
        $script:FailCount++
        return $null
    }
}

# ══════════════════════════════════════════════════════════════
$testJwt = New-TestJwt

Write-Host "`n🔍 JwtLens SampleJwtLensApi — Endpoint Tests" -ForegroundColor Cyan
Write-Host "   Target: $BaseUrl"
Write-Host "   Test JWT: $($testJwt.Substring(0, [Math]::Min(40, $testJwt.Length)))...`n"

# ── Verify server is reachable ──
try {
    Invoke-WebRequest -Uri "$BaseUrl/api/test" -ErrorAction Stop | Out-Null
    Write-Host "✓ Server is reachable`n" -ForegroundColor Green
} catch {
    Write-Host "✗ Cannot reach server at $BaseUrl — is SampleJwtLensApi running?" -ForegroundColor Red
    Write-Host "  Start it with: cd samples/SampleJwtLensApi && dotnet run`n" -ForegroundColor DarkGray
    exit 1
}

# ══════════════════════════════════════════════════════════════
Write-Host "━━━ Basic Connectivity ━━━" -ForegroundColor Yellow
Test-Endpoint -Name "GET /api/test (no auth)" -Endpoint "/api/test"

# ══════════════════════════════════════════════════════════════
Write-Host "`n━━━ Inbound JWT Capture ━━━" -ForegroundColor Yellow
Test-Endpoint -Name "GET /api/test (with ******" -Endpoint "/api/test" -Headers @{ "Authorization" = "******" }

# ══════════════════════════════════════════════════════════════
Write-Host "`n━━━ Event Store Endpoints ━━━" -ForegroundColor Yellow
Test-Endpoint -Name "GET /api/jwt/events" -Endpoint "/api/jwt/events"
Test-Endpoint -Name "GET /api/jwt/events/count" -Endpoint "/api/jwt/events/count"
Test-Endpoint -Name "DELETE /api/jwt/events" -Method "DELETE" -Endpoint "/api/jwt/events"

# ══════════════════════════════════════════════════════════════
Write-Host "`n━━━ Diagnostics & Options ━━━" -ForegroundColor Yellow
Test-Endpoint -Name "GET /api/jwt/diagnostics" -Endpoint "/api/jwt/diagnostics"
Test-Endpoint -Name "GET /api/jwt/options" -Endpoint "/api/jwt/options"

# ══════════════════════════════════════════════════════════════
Write-Host "`n━━━ Outbound JWT Capture ━━━" -ForegroundColor Yellow
Test-Endpoint -Name "GET /api/outbound-test?token=JWT" -Endpoint "/api/outbound-test?token=$testJwt"

# ══════════════════════════════════════════════════════════════
Write-Host "`n━━━ Verification ━━━" -ForegroundColor Yellow
$countResponse = Test-Endpoint -Name "GET /api/jwt/events/count (post-test)" -Endpoint "/api/jwt/events/count"
if ($countResponse) {
    $countData = $countResponse | ConvertFrom-Json
    Write-Host "    Events in store: $($countData.count), Total captured: $($countData.totalCaptured)" -ForegroundColor DarkGray
}

# ══════════════════════════════════════════════════════════════
Write-Host "`n════════════════════════════════════════"
Write-Host "Results: $($script:PassCount) passed, $($script:FailCount) failed" -ForegroundColor $(if ($script:FailCount -gt 0) { "Red" } else { "Green" })
Write-Host "════════════════════════════════════════"
exit ($script:FailCount -gt 0 ? 1 : 0)

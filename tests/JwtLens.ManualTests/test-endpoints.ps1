#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Endpoint availability tests for SampleJwtLensApi.
    Verifies all expected API routes respond with correct HTTP status codes.

.PARAMETER BaseUrl
    Base URL of the running SampleJwtLensApi instance. Default: http://localhost:5050

.EXAMPLE
    ./test-endpoints.ps1 -BaseUrl "http://localhost:5050"
#>
param(
    [string]$BaseUrl = "http://localhost:5050",
    [switch]$VerboseOutput
)

$ErrorActionPreference = "Stop"
$script:PassCount = 0
$script:FailCount = 0

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Method = "GET",
        [string]$Url,
        [int]$ExpectedStatus = 200,
        [hashtable]$Headers = @{},
        [string]$Body = $null
    )

    try {
        $params = @{
            Uri         = $Url
            Method      = $Method
            Headers     = $Headers
            ErrorAction = "Stop"
        }
        if ($Body) {
            $params["Body"] = $Body
            $params["ContentType"] = "application/json"
        }

        $response = Invoke-WebRequest @params
        $statusCode = $response.StatusCode

        if ($statusCode -eq $ExpectedStatus) {
            Write-Host "  PASS: $Name (HTTP $statusCode)" -ForegroundColor Green
            $script:PassCount++
        }
        else {
            Write-Host "  FAIL: $Name (expected $ExpectedStatus, got $statusCode)" -ForegroundColor Red
            $script:FailCount++
        }
    }
    catch {
        $statusCode = 0
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }

        if ($statusCode -eq $ExpectedStatus) {
            Write-Host "  PASS: $Name (HTTP $statusCode)" -ForegroundColor Green
            $script:PassCount++
        }
        else {
            Write-Host "  FAIL: $Name (HTTP $statusCode - $($_.Exception.Message))" -ForegroundColor Red
            $script:FailCount++
        }
    }
}

# ============================================================
# Pre-flight
# ============================================================
Write-Host ""
Write-Host "JwtLens SampleJwtLensApi - Endpoint Tests" -ForegroundColor Cyan
Write-Host "Target: $BaseUrl"
Write-Host ""

try {
    Invoke-WebRequest -Uri "$BaseUrl/api/test" -ErrorAction Stop | Out-Null
    Write-Host "Server is reachable" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "Cannot reach server at $BaseUrl" -ForegroundColor Red
    Write-Host "  Start it with: cd samples/SampleJwtLensApi; dotnet run" -ForegroundColor DarkGray
    exit 1
}

# ============================================================
# Core Endpoints
# ============================================================
Write-Host "--- Core Endpoints ---" -ForegroundColor Yellow

Test-Endpoint -Name "GET /api/test" -Url "$BaseUrl/api/test"
$bearerHeader = @{ "Authorization" = ("Bearer" + " " + "test.token.value") }
Test-Endpoint -Name "GET /api/test (with Bearer)" -Url "$BaseUrl/api/test" -Headers $bearerHeader
Test-Endpoint -Name "GET /api/outbound-test" -Url "$BaseUrl/api/outbound-test"

# ============================================================
# JWT Events API
# ============================================================
Write-Host ""
Write-Host "--- JWT Events API ---" -ForegroundColor Yellow

Test-Endpoint -Name "GET /api/jwt/events" -Url "$BaseUrl/api/jwt/events"
Test-Endpoint -Name "GET /api/jwt/events/count" -Url "$BaseUrl/api/jwt/events/count"
Test-Endpoint -Name "DELETE /api/jwt/events (clear)" -Method "DELETE" -Url "$BaseUrl/api/jwt/events"

# ============================================================
# Diagnostics API
# ============================================================
Write-Host ""
Write-Host "--- Diagnostics API ---" -ForegroundColor Yellow

Test-Endpoint -Name "GET /api/jwt/diagnostics" -Url "$BaseUrl/api/jwt/diagnostics"

# ============================================================
# Negative Tests (expected failures)
# ============================================================
Write-Host ""
Write-Host "--- Negative Tests ---" -ForegroundColor Yellow

Test-Endpoint -Name "GET /api/nonexistent (404)" -Url "$BaseUrl/api/nonexistent" -ExpectedStatus 404

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

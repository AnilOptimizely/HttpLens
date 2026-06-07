# =============================================================================
# HttpLens SampleWebApi - Endpoint Tests
# =============================================================================
# This script tests the SampleWebApi endpoints to verify HttpLens integration.
# Prerequisites: Start the SampleWebApi first:
#   cd samples/SampleWebApi && dotnet run
# =============================================================================

$baseUrl = "http://localhost:53938"
$passed = 0
$failed = 0

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Url,
        [int]$ExpectedStatus = 200,
        [hashtable]$Headers = @{}
    )

    try {
        $params = @{
            Uri = $Url
            Method = "GET"
            UseBasicParsing = $true
        }

        if ($Headers.Count -gt 0) {
            $params["Headers"] = $Headers
        }

        $response = Invoke-WebRequest @params
        $statusCode = $response.StatusCode

        if ($statusCode -eq $ExpectedStatus) {
            Write-Host "  PASS: $Name (HTTP $statusCode)" -ForegroundColor Green
            $script:passed++
        } else {
            Write-Host "  FAIL: $Name (Expected $ExpectedStatus, got $statusCode)" -ForegroundColor Red
            $script:failed++
        }
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq $ExpectedStatus) {
            Write-Host "  PASS: $Name (HTTP $statusCode)" -ForegroundColor Green
            $script:passed++
        } else {
            Write-Host "  FAIL: $Name (HTTP $statusCode - $($_.Exception.Message))" -ForegroundColor Red
            $script:failed++
        }
    }
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

Write-Host ""
Write-Host "HttpLens SampleWebApi - Endpoint Tests" -ForegroundColor Cyan
Write-Host ("=" * 50)
Write-Host ""

# Check if the server is running
try {
    $null = Invoke-WebRequest -Uri "$baseUrl/api/weather" -Method GET -UseBasicParsing -TimeoutSec 3
} catch [System.Net.WebException] {
    Write-Host "  ERROR: Cannot connect to $baseUrl" -ForegroundColor Red
    Write-Host "  Start it with:" -ForegroundColor Yellow
    Write-Host "    cd samples/SampleWebApi" -ForegroundColor Yellow
    Write-Host "    dotnet run" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

Write-Host "API Endpoints:" -ForegroundColor Yellow
Write-Host ("-" * 50)

Test-Endpoint -Name "GET /api/weather" -Url "$baseUrl/api/weather"
Test-Endpoint -Name "GET /api/github" -Url "$baseUrl/api/github"
Test-Endpoint -Name "GET /api/manual" -Url "$baseUrl/api/manual"

Write-Host ""
Write-Host "Debug Endpoints:" -ForegroundColor Yellow
Write-Host ("-" * 50)

Test-Endpoint -Name "GET /api/debug/store" -Url "$baseUrl/api/debug/store"
Test-Endpoint -Name "GET /api/debug/options" -Url "$baseUrl/api/debug/options"

Write-Host ""
Write-Host "Dashboard Endpoints:" -ForegroundColor Yellow
Write-Host ("-" * 50)

Test-Endpoint -Name "GET /httplens (dashboard)" -Url "$baseUrl/httplens"
Test-Endpoint -Name "GET /httplens/api/traffic" -Url "$baseUrl/httplens/api/traffic"

Write-Host ""
Write-Host "Auth Endpoints (with headers):" -ForegroundColor Yellow
Write-Host ("-" * 50)

$authHeaders = @{
    "X-Test-User" = "admin"
    "X-Test-Role" = "Admin"
}
Test-Endpoint -Name "GET /httplens/api/traffic (authenticated)" -Url "$baseUrl/httplens/api/traffic" -Headers $authHeaders

Write-Host ""
Write-Host ("=" * 50)
Write-Host "Results: $passed passed, $failed failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($failed -gt 0) {
    exit 1
}

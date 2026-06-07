#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Environment guard tests for JwtLens v0.1.
    Category 6: Tests that JwtLens respects AllowedEnvironments configuration.

.DESCRIPTION
    These tests require restarting the SampleJwtLensApi with different ASPNETCORE_ENVIRONMENT values.
    Run with -ExpectDisabled when JwtLens should NOT be active (e.g., Production with AllowedEnvironments=["Development"]).

.PARAMETER BaseUrl
    Base URL of the running SampleJwtLensApi instance. Default: http://localhost:5000

.PARAMETER ExpectDisabled
    When set, expects JwtLens to be disabled (no events captured, no store available).

.EXAMPLE
    # Test with Development environment (default — JwtLens active):
    ./test-jwtlens-environment.ps1 -BaseUrl "http://localhost:5000"

    # Test with Production environment (JwtLens disabled):
    ./test-jwtlens-environment.ps1 -BaseUrl "http://localhost:5000" -ExpectDisabled
#>
param(
    [string]$BaseUrl = "http://localhost:5000",
    [switch]$ExpectDisabled
)

$ErrorActionPreference = "Stop"
$script:PassCount = 0
$script:FailCount = 0

# Load helpers
. "$PSScriptRoot/helpers/jwt-helpers.ps1"

function Test-Case {
    param([string]$Name, [scriptblock]$Test)
    try {
        $result = & $Test
        if ($result -eq $true) {
            Write-Host "  ✅ PASS: $Name" -ForegroundColor Green
            $script:PassCount++
        } else {
            Write-Host "  ❌ FAIL: $Name (returned $result)" -ForegroundColor Red
            $script:FailCount++
        }
    } catch {
        Write-Host "  ❌ FAIL: $Name — $($_.Exception.Message)" -ForegroundColor Red
        $script:FailCount++
    }
}

Write-Host "`n🔍 JwtLens v0.1 — Environment Guard Tests" -ForegroundColor Cyan
Write-Host "Target: $BaseUrl"
Write-Host "Mode: $(if ($ExpectDisabled) { 'Expect DISABLED' } else { 'Expect ENABLED' })`n"

# Verify server is running
try {
    Invoke-RestMethod -Uri "$BaseUrl/api/test" -ErrorAction Stop | Out-Null
    Write-Host "✓ Server is reachable`n" -ForegroundColor Green
} catch {
    Write-Host "✗ Cannot reach server at $BaseUrl" -ForegroundColor Red
    exit 1
}

if ($ExpectDisabled) {
    # ════════════════════════════════════════════════════════════
    # Tests when JwtLens is expected to be DISABLED
    # ════════════════════════════════════════════════════════════
    Write-Host "━━━ Category 6: Environment Guard (Disabled) ━━━" -ForegroundColor Yellow

    Test-Case "6.2 JwtLens not active — events endpoint returns empty" {
        $events = Invoke-RestMethod -Uri "$BaseUrl/api/jwt/events" -ErrorAction Stop
        # Should be empty array (no store registered or store is empty)
        $events.Count -eq 0
    }

    Test-Case "6.2 Sending JWT does NOT create event" {
        $token = New-TestJwt -Payload @{ sub = "env-test-user"; iss = "test" }
        Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
        $countResult = Invoke-RestMethod -Uri "$BaseUrl/api/jwt/events/count" -ErrorAction Stop
        $countResult.count -eq 0
    }

    Test-Case "6.2 Requests still pass through (no error)" {
        $result = Invoke-RestMethod -Uri "$BaseUrl/api/test" -ErrorAction Stop
        Assert-Equal -Expected "OK" -Actual $result.message -Field "message"
        $true
    }

} else {
    # ════════════════════════════════════════════════════════════
    # Tests when JwtLens is expected to be ENABLED
    # ════════════════════════════════════════════════════════════
    Write-Host "━━━ Category 6: Environment Guard (Enabled) ━━━" -ForegroundColor Yellow

    Test-Case "6.1/6.4/6.5 JwtLens active — events are captured" {
        # Clear first
        Clear-JwtEvents -BaseUrl $BaseUrl
        $token = New-TestJwt -Payload @{ sub = "env-test-user"; iss = "test" }
        Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
        $countResult = Get-JwtEventCount -BaseUrl $BaseUrl
        Assert-Equal -Expected 1 -Actual $countResult.count -Field "count"
        $true
    }

    Test-Case "6.3 Empty allowed list means all environments work" {
        # This test is relevant when AllowedEnvironments is [] in config
        $opts = Invoke-RestMethod -Uri "$BaseUrl/api/jwt/options" -ErrorAction Stop
        # If we got here and JwtLens is active, the environment guard passed
        Assert-True -Actual $opts.isEnabled -Field "IsEnabled"
        $true
    }
}

# ════════════════════════════════════════════════════════════
# Results
# ════════════════════════════════════════════════════════════
Write-Host "`n════════════════════════════════════════"
Write-Host "Results: $($script:PassCount) passed, $($script:FailCount) failed" -ForegroundColor $(if ($script:FailCount -gt 0) { "Red" } else { "Green" })
Write-Host "════════════════════════════════════════"
exit ($script:FailCount -gt 0 ? 1 : 0)

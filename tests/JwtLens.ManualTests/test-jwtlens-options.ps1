#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Options toggle tests for JwtLens v0.1.
    Category 7: Tests that JwtLens respects runtime option changes.

.DESCRIPTION
    These tests verify that individual JwtLens options (IsEnabled, CaptureInboundTokens, etc.)
    correctly control behavior. Some tests require modifying appsettings.json and restarting the app.

.PARAMETER BaseUrl
    Base URL of the running SampleJwtLensApi instance. Default: http://localhost:5000

.PARAMETER TestScenario
    Which options scenario to test:
    - "all"                     — Run all tests (default, assumes default options)
    - "disabled"                — Test with IsEnabled=false
    - "no-inbound"              — Test with CaptureInboundTokens=false
    - "no-outbound"             — Test with CaptureOutboundTokens=false
    - "no-weak-alg-flag"        — Test with FlagWeakAlgorithms=false
    - "custom-expiry-threshold" — Test with WarnIfExpiresWithin=00:10:00
    - "custom-weak-algs"        — Test with WeakAlgorithms including RS256

.EXAMPLE
    # Run default options tests:
    ./test-jwtlens-options.ps1 -BaseUrl "http://localhost:5000"

    # Test with IsEnabled=false (after modifying appsettings.json):
    ./test-jwtlens-options.ps1 -BaseUrl "http://localhost:5000" -TestScenario "disabled"
#>
param(
    [string]$BaseUrl = "http://localhost:5000",
    [ValidateSet("all", "disabled", "no-inbound", "no-outbound", "no-weak-alg-flag", "custom-expiry-threshold", "custom-weak-algs")]
    [string]$TestScenario = "all"
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

Write-Host "`n🔍 JwtLens v0.1 — Options Toggle Tests" -ForegroundColor Cyan
Write-Host "Target: $BaseUrl"
Write-Host "Scenario: $TestScenario`n"

# Verify server is running
try {
    Invoke-RestMethod -Uri "$BaseUrl/api/test" -ErrorAction Stop | Out-Null
    Write-Host "✓ Server is reachable`n" -ForegroundColor Green
} catch {
    Write-Host "✗ Cannot reach server at $BaseUrl" -ForegroundColor Red
    exit 1
}

# ════════════════════════════════════════════════════════════
Write-Host "━━━ Category 7: Master Switch & Options ━━━" -ForegroundColor Yellow

if ($TestScenario -eq "all" -or $TestScenario -eq "disabled") {
    Test-Case "7.1 IsEnabled=false — no events captured" {
        $opts = Invoke-RestMethod -Uri "$BaseUrl/api/jwt/options" -ErrorAction Stop
        if ($TestScenario -eq "disabled") {
            # Verify option is actually false
            Assert-False -Actual $opts.isEnabled -Field "IsEnabled"
        }
        if (-not $opts.isEnabled) {
            Clear-JwtEvents -BaseUrl $BaseUrl
            $token = New-TestJwt -Payload @{ sub = "opt-test"; iss = "test" }
            Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
            Invoke-RestMethod -Uri "$BaseUrl/api/outbound-test?token=$token" -ErrorAction Stop | Out-Null
            Start-Sleep -Milliseconds 500
            $count = (Get-JwtEventCount -BaseUrl $BaseUrl).count
            Assert-Equal -Expected 0 -Actual $count -Field "EventCount"
            $true
        } else {
            if ($TestScenario -eq "disabled") { throw "IsEnabled should be false for this test" }
            Write-Host "    (Skipped — IsEnabled is true, set to false and restart to test)" -ForegroundColor DarkGray
            $true  # Skip gracefully
        }
    }
}

if ($TestScenario -eq "all" -or $TestScenario -eq "no-inbound") {
    Test-Case "7.2 CaptureInboundTokens=false — no inbound capture" {
        $opts = Invoke-RestMethod -Uri "$BaseUrl/api/jwt/options" -ErrorAction Stop
        if (-not $opts.captureInboundTokens) {
            Clear-JwtEvents -BaseUrl $BaseUrl
            $token = New-TestJwt -Payload @{ sub = "inbound-off"; iss = "test" }
            Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
            $count = (Get-JwtEventCount -BaseUrl $BaseUrl).count
            Assert-Equal -Expected 0 -Actual $count -Field "InboundEventCount"
            $true
        } else {
            if ($TestScenario -eq "no-inbound") { throw "CaptureInboundTokens should be false" }
            Write-Host "    (Skipped — CaptureInboundTokens is true)" -ForegroundColor DarkGray
            $true
        }
    }
}

if ($TestScenario -eq "all" -or $TestScenario -eq "no-outbound") {
    Test-Case "7.3 CaptureOutboundTokens=false — no outbound capture" {
        $opts = Invoke-RestMethod -Uri "$BaseUrl/api/jwt/options" -ErrorAction Stop
        if (-not $opts.captureOutboundTokens) {
            Clear-JwtEvents -BaseUrl $BaseUrl
            $token = New-TestJwt -Payload @{ sub = "outbound-off"; iss = "test" }
            Invoke-RestMethod -Uri "$BaseUrl/api/outbound-test?token=$token" -ErrorAction Stop | Out-Null
            Start-Sleep -Milliseconds 500
            $events = Get-JwtEvents -BaseUrl $BaseUrl
            $outbound = @($events | Where-Object { $_.direction -eq "Outbound" })
            Assert-Equal -Expected 0 -Actual $outbound.Count -Field "OutboundEventCount"
            $true
        } else {
            if ($TestScenario -eq "no-outbound") { throw "CaptureOutboundTokens should be false" }
            Write-Host "    (Skipped — CaptureOutboundTokens is true)" -ForegroundColor DarkGray
            $true
        }
    }
}

if ($TestScenario -eq "all" -or $TestScenario -eq "no-weak-alg-flag") {
    Test-Case "7.4 FlagWeakAlgorithms=false — no algorithm warnings" {
        $opts = Invoke-RestMethod -Uri "$BaseUrl/api/jwt/options" -ErrorAction Stop
        if (-not $opts.flagWeakAlgorithms) {
            Clear-JwtEvents -BaseUrl $BaseUrl
            $token = New-TestJwtNoSignature -Header @{ alg = "none" } -Payload @{ sub = "weakalg-off" }
            Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
            $event = Get-LastJwtEvent -BaseUrl $BaseUrl
            Assert-Equal -Expected 0 -Actual $event.algorithmWarnings.Count -Field "AlgorithmWarnings.Count"
            $true
        } else {
            if ($TestScenario -eq "no-weak-alg-flag") { throw "FlagWeakAlgorithms should be false" }
            Write-Host "    (Skipped — FlagWeakAlgorithms is true)" -ForegroundColor DarkGray
            $true
        }
    }
}

if ($TestScenario -eq "all" -or $TestScenario -eq "custom-expiry-threshold") {
    Test-Case "7.5 Custom WarnIfExpiresWithin=10min — token expiring in 8min triggers warning" {
        $opts = Invoke-RestMethod -Uri "$BaseUrl/api/jwt/options" -ErrorAction Stop
        # Parse the threshold
        $threshold = [TimeSpan]::Parse($opts.warnIfExpiresWithin)
        if ($threshold.TotalMinutes -ge 10) {
            Clear-JwtEvents -BaseUrl $BaseUrl
            $exp = Get-UnixTimestamp -OffsetMinutes 8
            $token = New-TestJwt -Payload @{ sub = "threshold-test"; exp = $exp }
            Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
            $event = Get-LastJwtEvent -BaseUrl $BaseUrl
            Assert-True -Actual $event.isExpiringSoon -Field "IsExpiringSoon"
            $true
        } else {
            if ($TestScenario -eq "custom-expiry-threshold") { throw "WarnIfExpiresWithin should be >= 10 minutes" }
            Write-Host "    (Skipped — WarnIfExpiresWithin is $($threshold.TotalMinutes)min, need 10min)" -ForegroundColor DarkGray
            $true
        }
    }
}

if ($TestScenario -eq "all" -or $TestScenario -eq "custom-weak-algs") {
    Test-Case "7.6 Custom WeakAlgorithms includes RS256" {
        $opts = Invoke-RestMethod -Uri "$BaseUrl/api/jwt/options" -ErrorAction Stop
        $weakAlgs = $opts.weakAlgorithms
        if ($weakAlgs -contains "RS256") {
            Clear-JwtEvents -BaseUrl $BaseUrl
            $token = New-TestJwt -Header @{ alg = "RS256"; typ = "JWT" } -Payload @{ sub = "custom-weak" }
            Send-JwtRequest -BaseUrl $BaseUrl -Token $token | Out-Null
            $event = Get-LastJwtEvent -BaseUrl $BaseUrl
            $event.algorithmWarnings.Count -gt 0
        } else {
            if ($TestScenario -eq "custom-weak-algs") { throw "WeakAlgorithms should include RS256" }
            Write-Host "    (Skipped — RS256 not in WeakAlgorithms)" -ForegroundColor DarkGray
            $true
        }
    }
}

# ════════════════════════════════════════════════════════════
# Results
# ════════════════════════════════════════════════════════════
Write-Host "`n════════════════════════════════════════"
Write-Host "Results: $($script:PassCount) passed, $($script:FailCount) failed" -ForegroundColor $(if ($script:FailCount -gt 0) { "Red" } else { "Green" })
Write-Host "════════════════════════════════════════"
exit ($script:FailCount -gt 0 ? 1 : 0)

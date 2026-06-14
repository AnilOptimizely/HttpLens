#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Helper functions for JwtLens manual tests.
    Provides JWT creation, HTTP request helpers, and assertion functions.
#>

# ============================================================
# JWT Creation Helpers
# ============================================================

function ConvertTo-Base64Url {
    param([byte[]]$Bytes)
    $base64 = [Convert]::ToBase64String($Bytes)
    $base64.TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function ConvertTo-Base64UrlString {
    param([string]$Text)
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
    ConvertTo-Base64Url -Bytes $bytes
}

function Get-UnixTimestamp {
    param([int]$OffsetMinutes = 0)
    $epoch = [DateTimeOffset]::UtcNow.AddMinutes($OffsetMinutes)
    [int]$epoch.ToUnixTimeSeconds()
}

function New-TestJwt {
    param(
        [hashtable]$Header = @{ alg = "RS256"; typ = "JWT" },
        [hashtable]$Payload
    )

    $headerJson = $Header | ConvertTo-Json -Compress
    $payloadJson = $Payload | ConvertTo-Json -Compress

    $headerB64 = ConvertTo-Base64UrlString -Text $headerJson
    $payloadB64 = ConvertTo-Base64UrlString -Text $payloadJson
    $fakeSignature = ConvertTo-Base64Url -Bytes ([System.Text.Encoding]::UTF8.GetBytes("fakesignature"))

    "$headerB64.$payloadB64.$fakeSignature"
}

function New-TestJwtNoSignature {
    param(
        [hashtable]$Header = @{ alg = "none" },
        [hashtable]$Payload
    )

    $headerJson = $Header | ConvertTo-Json -Compress
    $payloadJson = $Payload | ConvertTo-Json -Compress

    $headerB64 = ConvertTo-Base64UrlString -Text $headerJson
    $payloadB64 = ConvertTo-Base64UrlString -Text $payloadJson

    "$headerB64.$payloadB64."
}

function New-TestJwtTwoSegments {
    param(
        [hashtable]$Header = @{ alg = "RS256"; typ = "JWT" },
        [hashtable]$Payload
    )

    $headerJson = $Header | ConvertTo-Json -Compress
    $payloadJson = $Payload | ConvertTo-Json -Compress

    $headerB64 = ConvertTo-Base64UrlString -Text $headerJson
    $payloadB64 = ConvertTo-Base64UrlString -Text $payloadJson

    "$headerB64.$payloadB64"
}

# ============================================================
# HTTP Request Helpers
# ============================================================

function Send-JwtRequest {
    param(
        [string]$BaseUrl,
        [string]$Token,
        [string]$Endpoint = "/api/test"
    )

    $headers = @{ "Authorization" = "******" }
    Invoke-RestMethod -Uri "$BaseUrl$Endpoint" -Headers $headers -ErrorAction Stop
}

function Get-JwtEvents {
    param([string]$BaseUrl)
    Invoke-RestMethod -Uri "$BaseUrl/api/jwt/events" -ErrorAction Stop
}

function Get-LastJwtEvent {
    param([string]$BaseUrl)
    Invoke-RestMethod -Uri "$BaseUrl/api/jwt/events/last" -ErrorAction Stop
}

function Get-JwtEventCount {
    param([string]$BaseUrl)
    Invoke-RestMethod -Uri "$BaseUrl/api/jwt/events/count" -ErrorAction Stop
}

function Clear-JwtEvents {
    param([string]$BaseUrl)
    Invoke-RestMethod -Uri "$BaseUrl/api/jwt/events" -Method Delete -ErrorAction Stop
}

# ============================================================
# Assertion Helpers
# ============================================================

function Assert-True {
    param($Actual, [string]$Field)
    if ($Actual -ne $true) {
        throw "Expected $Field to be True but got: $Actual"
    }
}

function Assert-False {
    param($Actual, [string]$Field)
    if ($Actual -ne $false) {
        throw "Expected $Field to be False but got: $Actual"
    }
}

function Assert-Equal {
    param($Expected, $Actual, [string]$Field)
    if ("$Actual" -ne "$Expected") {
        throw "Expected $Field to be '$Expected' but got: '$Actual'"
    }
}

function Assert-NotNull {
    param($Actual, [string]$Field)
    if ($null -eq $Actual) {
        throw "Expected $Field to be non-null but got null"
    }
}

function Assert-Null {
    param($Actual, [string]$Field)
    if ($null -ne $Actual) {
        throw "Expected $Field to be null but got: $Actual"
    }
}

function Assert-Contains {
    param([string]$Expected, [string]$Actual, [string]$Field)
    if (-not $Actual) {
        throw "Expected $Field to contain '$Expected' but value was null/empty"
    }
    if ($Actual -notlike "*$Expected*") {
        throw "Expected $Field to contain '$Expected' but got: '$Actual'"
    }
}

#!/usr/bin/env pwsh
# JWT Helper Functions for JwtLens Manual Tests

function ConvertTo-Base64Url {
    param([string]$InputString)
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($InputString)
    $base64 = [Convert]::ToBase64String($bytes)
    return $base64.Replace('+', '-').Replace('/', '_').TrimEnd('=')
}

function New-TestJwt {
    param(
        [hashtable]$Header = @{ alg = "RS256"; typ = "JWT" },
        [hashtable]$Payload = @{ sub = "user1"; iss = "test-issuer" },
        [string]$Signature = "fake-signature-placeholder"
    )
    $headerJson = $Header | ConvertTo-Json -Compress
    $payloadJson = $Payload | ConvertTo-Json -Compress
    $headerB64 = ConvertTo-Base64Url -InputString $headerJson
    $payloadB64 = ConvertTo-Base64Url -InputString $payloadJson
    $sigB64 = ConvertTo-Base64Url -InputString $Signature
    return "$headerB64.$payloadB64.$sigB64"
}

function New-TestJwtNoSignature {
    param(
        [hashtable]$Header = @{ alg = "none" },
        [hashtable]$Payload = @{ sub = "user1" }
    )
    $headerJson = $Header | ConvertTo-Json -Compress
    $payloadJson = $Payload | ConvertTo-Json -Compress
    $headerB64 = ConvertTo-Base64Url -InputString $headerJson
    $payloadB64 = ConvertTo-Base64Url -InputString $payloadJson
    return "$headerB64.$payloadB64."
}

function New-TestJwtTwoSegments {
    param(
        [hashtable]$Header = @{ alg = "RS256"; typ = "JWT" },
        [hashtable]$Payload = @{ sub = "user1" }
    )
    $headerJson = $Header | ConvertTo-Json -Compress
    $payloadJson = $Payload | ConvertTo-Json -Compress
    $headerB64 = ConvertTo-Base64Url -InputString $headerJson
    $payloadB64 = ConvertTo-Base64Url -InputString $payloadJson
    return "$headerB64.$payloadB64"
}

function Get-UnixTimestamp {
    param([int]$OffsetMinutes = 0)
    $epoch = [DateTimeOffset]::UtcNow.AddMinutes($OffsetMinutes)
    return [int]$epoch.ToUnixTimeSeconds()
}

function Send-JwtRequest {
    param(
        [string]$BaseUrl,
        [string]$Token,
        [string]$Endpoint = "/api/test",
        [string]$Method = "GET"
    )
    $uri = "$BaseUrl$Endpoint"
    $headers = @{}
    if ($Token) {
        $headers["Authorization"] = "******"
    }
    try {
        $response = Invoke-RestMethod -Uri $uri -Headers $headers -Method $Method -ErrorAction Stop
        return $response
    }
    catch {
        # Return the error but don't fail - some tests expect pass-through
        return $null
    }
}

function Get-JwtEvents {
    param([string]$BaseUrl)
    return Invoke-RestMethod -Uri "$BaseUrl/api/jwt/events" -ErrorAction Stop
}

function Get-LastJwtEvent {
    param([string]$BaseUrl)
    $events = Get-JwtEvents -BaseUrl $BaseUrl
    if ($events -and $events.Count -gt 0) {
        return $events[-1]
    }
    return $null
}

function Get-JwtEventCount {
    param([string]$BaseUrl)
    $result = Invoke-RestMethod -Uri "$BaseUrl/api/jwt/events/count" -ErrorAction Stop
    return $result
}

function Clear-JwtEvents {
    param([string]$BaseUrl)
    Invoke-RestMethod -Uri "$BaseUrl/api/jwt/events" -Method Delete -ErrorAction Stop
}

function Assert-Equal {
    param($Expected, $Actual, [string]$Field)
    if ($Expected -ne $Actual) {
        throw "Expected $Field='$Expected' but got '$Actual'"
    }
    return $true
}

function Assert-Contains {
    param([string]$Expected, [string]$Actual, [string]$Field)
    if (-not $Actual) {
        throw "Expected $Field to contain '$Expected' but it was null"
    }
    if (-not $Actual.Contains($Expected, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Expected $Field to contain '$Expected' but got '$Actual'"
    }
    return $true
}

function Assert-Null {
    param($Actual, [string]$Field)
    if ($null -ne $Actual) {
        throw "Expected $Field to be null but got '$Actual'"
    }
    return $true
}

function Assert-NotNull {
    param($Actual, [string]$Field)
    if ($null -eq $Actual) {
        throw "Expected $Field to not be null"
    }
    return $true
}

function Assert-True {
    param($Actual, [string]$Field)
    if ($Actual -ne $true) {
        throw "Expected $Field to be true but got '$Actual'"
    }
    return $true
}

function Assert-False {
    param($Actual, [string]$Field)
    if ($Actual -ne $false) {
        throw "Expected $Field to be false but got '$Actual'"
    }
    return $true
}

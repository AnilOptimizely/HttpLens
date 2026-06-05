$ErrorActionPreference = "Stop"

if (-not $env:NUGET_API_KEY) {
  throw "Set NUGET_API_KEY first."
}

$source = "https://api.nuget.org/v3/index.json"

Get-ChildItem -Path "artifacts" -Filter "*.nupkg" | ForEach-Object {
  Write-Host "Publishing $($_.Name)" -ForegroundColor Green

  dotnet nuget push $_.FullName `
    --api-key $env:NUGET_API_KEY `
    --source $source `
    --skip-duplicate
}

Write-Host "All placeholder packages published." -ForegroundColor Green
$ErrorActionPreference = "Stop"

$output = ".\artifacts"

Write-Host "Current directory: $(Get-Location)" -ForegroundColor Yellow

if (-not (Test-Path ".\packages")) {
    throw "The packages folder does not exist. Run this script from the placeholders folder."
}

$projects = Get-ChildItem -Path ".\packages" -Filter "*.csproj" -Recurse

if ($projects.Count -eq 0) {
    throw "No .csproj files found under .\packages."
}

Write-Host "Found $($projects.Count) project(s) to pack:" -ForegroundColor Cyan

foreach ($project in $projects) {
    Write-Host " - $($project.FullName)"
}

if (Test-Path $output) {
    Remove-Item $output -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $output | Out-Null

foreach ($project in $projects) {
    Write-Host ""
    Write-Host "Packing $($project.FullName)" -ForegroundColor Cyan

    dotnet pack $project.FullName --configuration Release --output $output

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed for $($project.FullName)"
    }
}

Write-Host ""
Write-Host "Done. Packages are in $output" -ForegroundColor Green

Get-ChildItem -Path $output -Filter "*.nupkg"
param(
    [string]$ValheimDir = ""
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK not found."
}

if ([string]::IsNullOrWhiteSpace($ValheimDir)) {
    $candidates = @(
        "D:\Steam\steamapps\common\Valheim",
        "C:\Program Files (x86)\Steam\steamapps\common\Valheim",
        "C:\Program Files\Steam\steamapps\common\Valheim"
    )
    $ValheimDir = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $ValheimDir -or -not (Test-Path $ValheimDir)) {
    throw "Valheim directory not found. Run: .\build.ps1 -ValheimDir 'D:\Steam\steamapps\common\Valheim'"
}

$required = @(
    "$ValheimDir\BepInEx\core\BepInEx.dll",
    "$ValheimDir\valheim_Data\Managed\assembly_valheim.dll",
    "$ValheimDir\valheim_Data\Managed\UnityEngine.CoreModule.dll"
)

foreach ($file in $required) {
    if (-not (Test-Path $file)) { throw "Missing reference: $file" }
}

$env:VALHEIM_DIR = $ValheimDir
Push-Location $PSScriptRoot
try {
    dotnet build .\XPortalSharedMapPins.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }

    New-Item -ItemType Directory -Force -Path .\dist | Out-Null
    Copy-Item .\bin\Release\net48\XPortalSharedMapPins.dll .\dist\XPortalSharedMapPins.dll -Force
    Write-Host "Built: $PSScriptRoot\dist\XPortalSharedMapPins.dll" -ForegroundColor Green
}
finally {
    Pop-Location
}

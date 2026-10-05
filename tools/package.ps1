# Builds a Thunderstore-ready package zip into ./dist using the pinned TCLI tool.
#
#   pwsh tools/package.ps1            # build + package
#   pwsh tools/package.ps1 -SkipBuild # package the existing Release build
#
# The version comes from Directory.Build.props via thunderstore.toml. Bump the
# <Version> there (and keep thunderstore.toml's versionNumber in sync) before releasing.
[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if (-not $SkipBuild) {
        dotnet build -c Release
        if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }
    }

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed' }

    dotnet tcli build
    if ($LASTEXITCODE -ne 0) { throw 'tcli build failed' }

    $version = (Select-String -Path 'thunderstore.toml' -Pattern 'versionNumber\s*=\s*"([^"]+)"').Matches[0].Groups[1].Value
    Write-Host ''
    Write-Host "Package ready: dist/LIFELAN-RandomCrestMod-$version.zip" -ForegroundColor Green
}
finally {
    Pop-Location
}

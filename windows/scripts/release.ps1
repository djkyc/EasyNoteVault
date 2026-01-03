$ErrorActionPreference = "Stop"

Write-Host "== Release Prepare =="

$version = $env:GITHUB_REF_NAME
if (-not $version) { throw "No version tag found." }

$src  = "artifacts/windows"
$dist = "dist/windows"

New-Item -ItemType Directory -Force -Path $dist | Out-Null

$zipName = "EasyNoteVault-$version-windows.zip"
$zipPath = Join-Path $dist $zipName

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path "$src\*" -DestinationPath $zipPath

Write-Host "Release package created:"
Write-Host $zipPath

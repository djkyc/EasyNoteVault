$ErrorActionPreference = "Stop"

Write-Host "== Release Prepare =="

$version = $env:GITHUB_REF_NAME
if (-not $version) {
    $version = "dev"
}

$src  = "artifacts/windows"
$dist = "dist/windows/$version"

New-Item -ItemType Directory -Force -Path $dist | Out-Null
Copy-Item "$src\*" $dist -Recurse -Force

Write-Host "Release version: $version"
Write-Host "Files copied to: $dist"

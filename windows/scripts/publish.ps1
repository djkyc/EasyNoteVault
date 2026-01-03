$ErrorActionPreference = "Stop"

Write-Host "== Publish EasyNoteVault =="

# 以脚本位置为基准，避免路径错误
$windowsRoot = Resolve-Path "$PSScriptRoot\.."

$project = Join-Path $windowsRoot "EasyNoteVault/EasyNoteVault.csproj"
$runtime = "win-x64"
$output  = Join-Path $windowsRoot "..\artifacts\windows"

# 校验项目是否存在
if (-not (Test-Path $project)) {
    throw "Project file not found: $project"
}

dotnet publish $project `
    -c Release `
    -r $runtime `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    -o $output

Write-Host "Publish finished successfully."
Write-Host "Output path: $output"

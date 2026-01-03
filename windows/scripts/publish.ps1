$ErrorActionPreference = "Stop"

Write-Host "== Publish EasyNoteVault =="

$project = "EasyNoteVault/EasyNoteVault.csproj"
$runtime = "win-x64"
$output  = "artifacts/windows"

dotnet publish $project `
    -c Release `
    -r $runtime `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    -o $output

Write-Host "Publish finished."
Write-Host "Output path: $output"

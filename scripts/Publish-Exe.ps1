param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("win-x64", "win-x86", "win-arm64")]
    [string]$Runtime = "win-x64",

    [Parameter(Mandatory = $false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $repoRoot "dist"
$outDir = Join-Path $dist "ClampDown\$Runtime"

if (Test-Path $dist) {
    Remove-Item -Recurse -Force $dist
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$projectPath = Join-Path $repoRoot "src\ClampDown.UI\ClampDown.UI.csproj"
$extraProps = @(
    "-p:SelfContained=true",
    "-p:PublishSingleFile=true",
    "-p:PublishReadyToRun=true",
    "-p:DebugType=None",
    "-p:DebugSymbols=false"
)

Write-Host "Publishing ClampDown ($Runtime, $Configuration) -> $outDir"
dotnet publish $projectPath -c $Configuration -r $Runtime --self-contained true -o $outDir @extraProps

$exePath = Join-Path $outDir "ClampDown.exe"
if (-not (Test-Path $exePath)) {
    throw "Expected output not found: $exePath"
}

Write-Host ""
Write-Host "Done."
Write-Host "Executable: $exePath"

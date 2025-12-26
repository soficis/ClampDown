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
$bundle = Join-Path $dist "ClampDown.Bundle\\$Runtime"

function Publish-Project {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $outDir = Join-Path $dist "$Name\$Runtime"
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null

    $extraProps = @(
        "-p:SelfContained=true",
        "-p:PublishSingleFile=false",
        "-p:PublishReadyToRun=true"
    )

    Write-Host "Publishing $Name ($Runtime, $Configuration) -> $outDir"
    dotnet publish $ProjectPath -c $Configuration -r $Runtime --self-contained true -o $outDir @extraProps
}

if (Test-Path $dist) {
    Remove-Item -Recurse -Force $dist
}
New-Item -ItemType Directory -Force -Path $dist | Out-Null
New-Item -ItemType Directory -Force -Path $bundle | Out-Null

Publish-Project -ProjectPath (Join-Path $repoRoot "src\ClampDown.UI\ClampDown.UI.csproj") -Name "ClampDown.UI"
Publish-Project -ProjectPath (Join-Path $repoRoot "src\ClampDown.Helper\ClampDown.Helper.csproj") -Name "ClampDown.Helper"
Publish-Project -ProjectPath (Join-Path $repoRoot "src\ClampDown.Cli\ClampDown.Cli.csproj") -Name "ClampDown.Cli"
Publish-Project -ProjectPath (Join-Path $repoRoot "src\ClampDown.Tray\ClampDown.Tray.csproj") -Name "ClampDown.Tray"

Write-Host "Creating bundle -> $bundle"
$projectsToBundle = @("ClampDown.UI", "ClampDown.Tray", "ClampDown.Cli", "ClampDown.Helper")
foreach ($name in $projectsToBundle) {
    $src = Join-Path $dist "$name\\$Runtime\\*"
    Copy-Item -Path $src -Destination $bundle -Recurse -Force
}

Write-Host ""
Write-Host "Done."
Write-Host "UI:     $dist\\ClampDown.UI\\$Runtime\\ClampDown.UI.exe"
Write-Host "Tray:   $dist\\ClampDown.Tray\\$Runtime\\ClampDown.Tray.exe"
Write-Host "CLI:    $dist\\ClampDown.Cli\\$Runtime\\ClampDown.Cli.exe"
Write-Host "Helper: $dist\\ClampDown.Helper\\$Runtime\\ClampDown.Helper.exe"
Write-Host "Bundle: $bundle"

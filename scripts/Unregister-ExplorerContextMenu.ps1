Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$keys = @(
    "HKCU:\Software\Classes\*\shell\ClampDown.Analyze",
    "HKCU:\Software\Classes\*\shell\ClampDown.UnlockDelete",
    "HKCU:\Software\Classes\Directory\shell\ClampDown.Analyze",
    "HKCU:\Software\Classes\Drive\shell\ClampDown.Eject"
)

foreach ($key in $keys) {
    if (Test-Path $key) {
        Remove-Item -Path $key -Recurse -Force
    }
}

Write-Host "Removed Explorer context menu entries under HKCU."


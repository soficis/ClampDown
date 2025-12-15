param(
    [Parameter(Mandatory = $false)]
    [string]$CliPath = "clampdown"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Set-ContextMenuCommand {
    param(
        [Parameter(Mandatory = $true)][string]$ClassKey,
        [Parameter(Mandatory = $true)][string]$VerbKey,
        [Parameter(Mandatory = $true)][string]$Label,
        [Parameter(Mandatory = $true)][string]$Command
    )

    $base = "HKCU:\Software\Classes\$ClassKey\shell\$VerbKey"
    New-Item -Path $base -Force | Out-Null
    New-ItemProperty -Path $base -Name "MUIVerb" -Value $Label -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $base -Name "Icon" -Value $CliPath -PropertyType String -Force | Out-Null

    $cmdKey = Join-Path $base "command"
    New-Item -Path $cmdKey -Force | Out-Null
    Set-ItemProperty -Path $cmdKey -Name "(default)" -Value $Command -Force | Out-Null
}

$cli = $CliPath

Set-ContextMenuCommand -ClassKey "*" -VerbKey "ClampDown.Analyze" -Label "Analyze locks (ClampDown)" -Command "`"$cli`" analyze `"%1`""
Set-ContextMenuCommand -ClassKey "*" -VerbKey "ClampDown.UnlockDelete" -Label "Unlock & Delete (ClampDown)" -Command "`"$cli`" unlock-delete `"%1`" --recycle-bin --schedule"

Set-ContextMenuCommand -ClassKey "Directory" -VerbKey "ClampDown.Analyze" -Label "Analyze locks (ClampDown)" -Command "`"$cli`" analyze `"%1`" --recursive"

Set-ContextMenuCommand -ClassKey "Drive" -VerbKey "ClampDown.Eject" -Label "Safe eject (ClampDown)" -Command "`"$cli`" eject `"%1`""

Write-Host "Registered Explorer context menu entries under HKCU. (CliPath=$CliPath)"


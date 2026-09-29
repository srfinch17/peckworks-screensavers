<#
.SYNOPSIS
    Removes screensavers from System32, deselects one if it was the active
    screensaver, and deletes their saved settings.

.PARAMETER Saver
    Which screensaver(s) to remove (MatrixRain, Sakura). Leave out for all.
#>
param([string[]]$Saver)
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
if (-not $Saver) {
    $Saver = Get-ChildItem (Join-Path $repo 'src') -Directory |
        Where-Object { $_.Name -ne 'Peckworks.Screensavers.Core' } | ForEach-Object Name
}
$desk = 'HKCU:\Control Panel\Desktop'
$current = (Get-ItemProperty $desk -Name 'SCRNSAVE.EXE' -ErrorAction SilentlyContinue).'SCRNSAVE.EXE'

$deletes = @()
foreach ($name in $Saver) {
    $dest = Join-Path $env:WINDIR "System32\$name.scr"
    if ($current -and $current -like "*\$name.scr") {
        Remove-ItemProperty $desk -Name 'SCRNSAVE.EXE'
        Write-Host "$name was the active screensaver; it is now deselected."
    }
    if (Test-Path $dest) { $deletes += "Remove-Item -LiteralPath '$dest' -Force" }
    Remove-Item "HKCU:\Software\Peckworks\Screensavers\$name" -Recurse -ErrorAction SilentlyContinue
}

if ($deletes) {
    Start-Process powershell -Verb RunAs -Wait -ArgumentList '-NoProfile', '-Command', ($deletes -join '; ')
    Write-Host "Removed: $($Saver -join ', ')"
}
Write-Host "Done."

<#
.SYNOPSIS
    Removes MatrixRain.scr from System32, deselects it if it was the active
    screensaver, and deletes its saved settings.
#>
$ErrorActionPreference = 'Stop'
$dest = Join-Path $env:WINDIR 'System32\MatrixRain.scr'
$desk = 'HKCU:\Control Panel\Desktop'

$current = (Get-ItemProperty $desk -Name 'SCRNSAVE.EXE' -ErrorAction SilentlyContinue).'SCRNSAVE.EXE'
if ($current -and $current -like '*MatrixRain.scr') {
    Remove-ItemProperty $desk -Name 'SCRNSAVE.EXE'
    Write-Host "Matrix Rain was the active screensaver; it is now deselected."
}

if (Test-Path $dest) {
    $del = "Remove-Item -LiteralPath '$dest' -Force"
    Start-Process powershell -Verb RunAs -Wait -ArgumentList '-NoProfile', '-Command', $del
    Write-Host "Removed $dest"
}

Remove-Item 'HKCU:\Software\Peckworks\Screensavers\MatrixRain' -Recurse -ErrorAction SilentlyContinue
Write-Host "Settings removed. Done."

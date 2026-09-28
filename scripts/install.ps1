<#
.SYNOPSIS
    Installs MatrixRain.scr so it appears in Windows' Screen Saver Settings list,
    then opens that dialog so you can pick it.

.DESCRIPTION
    How Windows finds screensavers: the Screen Saver Settings dialog lists every
    .scr file in C:\Windows\System32. That is the entire "registration" process.
    Copying a file into System32 needs administrator rights, so this script asks
    Windows for them (you will see one UAC "allow changes?" prompt).

    With -Activate it also selects Matrix Rain as your screensaver and turns it
    on (wait time 5 minutes), by writing the same registry values the dialog
    writes: HKCU\Control Panel\Desktop\SCRNSAVE.EXE and ScreenSaveActive.

    No-admin alternative: right-click dist\MatrixRain.scr in File Explorer and
    choose "Install". Windows then uses the file right where it is (so do not
    move or delete it afterward).

.EXAMPLE
    .\scripts\install.ps1            # copy + open the settings dialog
    .\scripts\install.ps1 -Activate  # copy + select it + enable it
#>
param([switch]$Activate, [switch]$SkipPublish)
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$scr  = Join-Path $repo 'dist\MatrixRain.scr'
$dest = Join-Path $env:WINDIR 'System32\MatrixRain.scr'

if (-not $SkipPublish -or -not (Test-Path $scr)) {
    & (Join-Path $PSScriptRoot 'publish.ps1')
}

# Copy into System32 through an elevated (administrator) PowerShell.
Write-Host "Copying to $dest (administrator permission needed)..."
$copy = "Copy-Item -LiteralPath '$scr' -Destination '$dest' -Force"
$proc = Start-Process powershell -Verb RunAs -Wait -PassThru `
    -ArgumentList '-NoProfile', '-Command', $copy
if ($proc.ExitCode -ne 0 -or -not (Test-Path $dest)) { throw "Copy to System32 failed or was cancelled." }
Write-Host "Installed."

if ($Activate) {
    $desk = 'HKCU:\Control Panel\Desktop'
    Set-ItemProperty $desk -Name 'SCRNSAVE.EXE'      -Value $dest
    Set-ItemProperty $desk -Name 'ScreenSaveActive'  -Value '1'
    Set-ItemProperty $desk -Name 'ScreenSaveTimeOut' -Value '300'   # seconds idle before it starts
    Write-Host "Matrix Rain selected as your screensaver (starts after 5 idle minutes)."
}

# Open Screen Saver Settings so you can preview, tweak Settings..., and set the wait time.
Start-Process 'control.exe' -ArgumentList 'desk.cpl,,@screensaver'

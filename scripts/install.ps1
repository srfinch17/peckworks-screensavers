<#
.SYNOPSIS
    Installs screensavers so they appear in Windows' Screen Saver Settings list,
    then opens that dialog so you can pick one.

.DESCRIPTION
    How Windows finds screensavers: the Screen Saver Settings dialog lists every
    .scr file in C:\Windows\System32. That is the entire "registration" process.
    Copying a file into System32 needs administrator rights, so this script asks
    Windows for them (you will see one UAC "allow changes?" prompt, covering
    every screensaver being installed).

    With -Activate it also selects one screensaver and turns it on (wait time
    5 minutes), by writing the same registry values the dialog writes:
    HKCU\Control Panel\Desktop\SCRNSAVE.EXE and ScreenSaveActive.

    No-admin alternative: right-click a .scr in dist\ in File Explorer and
    choose "Install". Windows then uses the file right where it is (so do not
    move or delete it afterward).

.PARAMETER Saver
    Which screensaver(s) to install (MatrixRain, Sakura, SakuraDusk, Halloween, Christmas). Leave out for all.

.PARAMETER Activate
    Also make this your active screensaver. Needs exactly one -Saver.

.EXAMPLE
    .\scripts\install.ps1                            # install all, open the settings dialog
    .\scripts\install.ps1 -Saver Sakura -Activate    # install Sakura and select it
#>
param([string[]]$Saver, [switch]$Activate, [switch]$SkipPublish)
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
if ($Activate -and $Saver.Count -ne 1) { throw "-Activate needs exactly one -Saver, for example: -Saver Sakura -Activate" }

if (-not $SkipPublish) {
    if ($Saver) { & (Join-Path $PSScriptRoot 'publish.ps1') -Saver $Saver }
    else        { & (Join-Path $PSScriptRoot 'publish.ps1') }
}

$files = @(if ($Saver) { $Saver | ForEach-Object { Join-Path $repo "dist\$_.scr" } }
         else        { (Get-ChildItem (Join-Path $repo 'dist') -Filter *.scr).FullName })
foreach ($f in $files) { if (-not (Test-Path $f)) { throw "Missing $f. Run .\scripts\publish.ps1 first." } }

# Copy into System32 through ONE elevated (administrator) PowerShell, so there
# is only one permission prompt no matter how many screensavers.
$system32 = Join-Path $env:WINDIR 'System32'
$copies = ($files | ForEach-Object { "Copy-Item -LiteralPath '$_' -Destination '$system32' -Force" }) -join '; '
Write-Host "Copying $($files.Count) screensaver(s) to $system32 (administrator permission needed)..."
$proc = Start-Process powershell -Verb RunAs -Wait -PassThru -ArgumentList '-NoProfile', '-Command', $copies
foreach ($f in $files) {
    $dest = Join-Path $system32 (Split-Path $f -Leaf)
    if ($proc.ExitCode -ne 0 -or -not (Test-Path $dest)) { throw "Copy of $f failed or was cancelled." }
    Write-Host "Installed $dest"
}

if ($Activate) {
    $dest = Join-Path $system32 "$($Saver[0]).scr"
    $desk = 'HKCU:\Control Panel\Desktop'
    Set-ItemProperty $desk -Name 'SCRNSAVE.EXE'      -Value $dest
    Set-ItemProperty $desk -Name 'ScreenSaveActive'  -Value '1'
    Set-ItemProperty $desk -Name 'ScreenSaveTimeOut' -Value '300'   # seconds idle before it starts
    Write-Host "$($Saver[0]) selected as your screensaver (starts after 5 idle minutes)."
}

# Open a FRESH Screen Saver Settings window (closing any stale one first, see
# ScreenSaverDialog.ps1 for why), then check the list the user will actually see.
# "The file is in System32" is not the same claim as "it is in the list".
. (Join-Path $PSScriptRoot 'ScreenSaverDialog.ps1')
$listed = Open-ScreenSaverDialog
foreach ($f in $files) {
    $name = [IO.Path]::GetFileNameWithoutExtension($f)
    if ($listed -contains $name) { Write-Host "Confirmed: '$name' is in the Screen Saver Settings list." }
    else { Write-Warning "'$name' is NOT in the Screen Saver Settings list. The list shows: $($listed -join ', ')" }
}

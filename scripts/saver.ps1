<#
.SYNOPSIS
    Turns the Windows screensaver off while work is going on, and back on
    (pointing at a build) when the work is done.

.DESCRIPTION
    Windows keeps the screensaver choice in the registry under
    HKCU\Control Panel\Desktop: SCRNSAVE.EXE is the file it runs and
    ScreenSaveActive is "1" or "0". No admin rights are needed, and the file
    can live anywhere, so pointing it at a .scr in dist\ is enough for the
    idle timer to run that build: no copy to System32, no UAC prompt.

    Windows reads ScreenSaveActive once at logon, so the script also tells it
    about the change through SystemParametersInfo (SPI_SETSCREENSAVEACTIVE),
    which takes effect at once.

.PARAMETER Off
    Stop the screensaver from starting (while timing or testing, so an idle
    screensaver cannot eat the processor or close a test window).

.PARAMETER On
    Let it start again. With -Use, also make that .scr the one it runs.

.PARAMETER Use
    Path to the .scr to select (for example .\dist\SakuraPond.scr).

.EXAMPLE
    .\scripts\saver.ps1 -Off
    .\scripts\saver.ps1 -On -Use .\dist\SakuraPond.scr
    .\scripts\saver.ps1            # just report the current state
#>
param([switch]$Off, [switch]$On, [string]$Use)
$ErrorActionPreference = 'Stop'

Add-Type -Namespace Win32 -Name Saver -MemberDefinition @'
[DllImport("user32.dll", SetLastError = true)]
public static extern bool SystemParametersInfo(uint action, uint param, IntPtr vparam, uint flags);
'@
$SPI_SETSCREENSAVEACTIVE = 0x0011
$SPIF_UPDATEINIFILE_SENDCHANGE = 0x0003

$desk = 'HKCU:\Control Panel\Desktop'
if ($Off) {
    [Win32.Saver]::SystemParametersInfo($SPI_SETSCREENSAVEACTIVE, 0, [IntPtr]::Zero, $SPIF_UPDATEINIFILE_SENDCHANGE) | Out-Null
    Set-ItemProperty $desk -Name 'ScreenSaveActive' -Value '0'
}
if ($Use) {
    $scr = (Resolve-Path $Use).Path
    Set-ItemProperty $desk -Name 'SCRNSAVE.EXE' -Value $scr
}
if ($On) {
    [Win32.Saver]::SystemParametersInfo($SPI_SETSCREENSAVEACTIVE, 1, [IntPtr]::Zero, $SPIF_UPDATEINIFILE_SENDCHANGE) | Out-Null
    Set-ItemProperty $desk -Name 'ScreenSaveActive' -Value '1'
}
$p = Get-ItemProperty $desk
$timeout = if ($p.PSObject.Properties.Name -contains 'ScreenSaveTimeOut') { $p.ScreenSaveTimeOut } else { '?' }
"Screensaver active: $($p.ScreenSaveActive)   runs: $($p.'SCRNSAVE.EXE')   after: $timeout s"

<#
.SYNOPSIS
    Checks that each published screensaver in dist\ actually works in every mode
    Windows uses, and prints PASS / FAIL for each check.

.DESCRIPTION
    Every check here was first done by hand while building this repo, and each
    one caught a real problem. Scripting them makes the checks repeatable.

    For each dist\<Name>.scr:

      snapshot   Renders 3 seconds off-screen at 1920x1080, saves a PNG, and
                 reports milliseconds per frame. PASS if under 16 ms (60 fps).
      preview    Hosts the /p preview inside a stand-in window, captures it, then
                 closes the host. PASS if the preview quits on its own (Windows
                 destroys the real preview box whenever Screen Saver Settings closes;
                 a preview that lingers is a leaked process).
      settings   Opens /c. PASS if the settings dialog window appears.
      windows    (only with -WindowsLaunch) Temporarily makes this the selected
                 screensaver and asks Windows to start it the real way (the
                 SC_SCREENSAVE system command). PASS if it is running 6 seconds
                 later. Your original screensaver setting is ALWAYS restored.

    IMPORTANT TESTING GOTCHA this script works around: launching a .scr through
    the Windows shell (double-click, or PowerShell's Start-Process) makes Windows
    REPLACE your arguments with /S, so "/c" silently runs full screen. Every
    launch below uses UseShellExecute = false to pass arguments untouched.

    Pictures land in snapshots\verify\ (gitignored). Look at them: a check can
    pass while the picture is wrong.

.PARAMETER Saver
    Which screensaver(s) to check. Leave out for every .scr in dist\.

.PARAMETER WindowsLaunch
    Also run the real Windows launch test (briefly changes, then restores, your
    selected screensaver; the screensaver will cover the screen for ~6 seconds).
    Keep your hands off the mouse and keyboard for the WHOLE run (about 40
    seconds per screensaver): any input quits a real screensaver, which then
    counts as a FAIL.

.EXAMPLE
    .\scripts\verify.ps1
    .\scripts\verify.ps1 -Saver Sakura -WindowsLaunch
#>
param([string[]]$Saver, [switch]$WindowsLaunch)
$ErrorActionPreference = 'Stop'

# The screen-capture helpers need the classic .NET Framework drawing library,
# which Windows PowerShell 5.1 has built in and PowerShell 7 does not. If we are
# in PowerShell 7, quietly re-run this same script under Windows PowerShell.
if ($PSVersionTable.PSEdition -eq 'Core') {
    $argsList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath)
    if ($Saver) { $argsList += '-Saver'; $argsList += ($Saver -join ',') }
    if ($WindowsLaunch) { $argsList += '-WindowsLaunch' }
    & powershell.exe @argsList
    exit $LASTEXITCODE
}
if ($Saver.Count -eq 1 -and $Saver[0] -like '*,*') { $Saver = $Saver[0] -split ',' }

$repo = Split-Path -Parent $PSScriptRoot
$out  = Join-Path $repo 'snapshots\verify'
New-Item -ItemType Directory -Force $out | Out-Null

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Drawing; using System.Runtime.InteropServices;
public static class VerifyNative {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    public struct RECT { public int L, T, R, B; }
    // Photographs one window, even if other windows cover it.
    public static void Capture(IntPtr h, string path) {
        RECT r; GetWindowRect(h, out r);
        using (var b = new Bitmap(Math.Max(1, r.R - r.L), Math.Max(1, r.B - r.T))) {
            using (var g = Graphics.FromImage(b)) { IntPtr dc = g.GetHdc(); PrintWindow(h, dc, 2); g.ReleaseHdc(dc); }
            b.Save(path);
        }
    }
}
'@
# Real pixels, not the scaled-down pretend pixels a non-DPI-aware program sees on a 250% screen.
[VerifyNative]::SetProcessDPIAware() | Out-Null

function Launch($file, $arguments) {
    $si = New-Object Diagnostics.ProcessStartInfo $file, $arguments
    $si.UseShellExecute = $false        # pass our arguments through untouched (see GOTCHA above)
    [Diagnostics.Process]::Start($si)
}

function Pump($ms) {                    # keep our own stand-in window responsive while waiting
    $end = [DateTime]::Now.AddMilliseconds($ms)
    while ([DateTime]::Now -lt $end) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 50 }
}

$files = if ($Saver) { $Saver | ForEach-Object { Join-Path $repo "dist\$_.scr" } }
         else        { (Get-ChildItem (Join-Path $repo 'dist') -Filter *.scr).FullName }

$results = @()
function Record($name, $check, $pass, $detail) {
    $script:results += [pscustomobject]@{ Saver = $name; Check = $check; Result = $(if ($pass) { 'PASS' } else { 'FAIL' }); Detail = $detail }
}

foreach ($scr in $files) {
    $name = [IO.Path]::GetFileNameWithoutExtension($scr)
    if (-not (Test-Path $scr)) { Record $name 'exists' $false "missing $scr (run .\scripts\publish.ps1)"; continue }
    Write-Host "Checking $name..."

    # ---- snapshot ----
    $png = Join-Path $out "$name-snapshot.png"
    Remove-Item "$png*" -ErrorAction SilentlyContinue
    $p = Launch $scr "/snapshot `"$png`" 1920 1080 3"
    $p.WaitForExit(60000) | Out-Null
    if (Test-Path "$png.txt") {
        $txt = (Get-Content "$png.txt" -Raw).Trim()
        $ms = [double]([regex]::Match($txt, 'average ([\d.]+) ms').Groups[1].Value)
        Record $name 'snapshot' ($ms -lt 16) "$ms ms/frame at 1080p"
    } else { Record $name 'snapshot' $false 'no snapshot written' }

    # ---- preview ----
    $host_ = New-Object System.Windows.Forms.Form
    $host_.ClientSize = New-Object System.Drawing.Size 380, 260
    $host_.Text = "verify preview host"
    $host_.Show()
    $p = Launch $scr "/p $($host_.Handle.ToInt64())"
    Pump 4000
    [VerifyNative]::Capture($host_.Handle, (Join-Path $out "$name-preview.png"))
    $alive = -not $p.HasExited
    $host_.Close(); $host_.Dispose()
    Pump 3000
    $p.Refresh()
    Record $name 'preview' ($alive -and $p.HasExited) $(if (-not $alive) { 'exited before drawing' } elseif ($p.HasExited) { 'drew, then quit when host closed' } else { 'still running after host closed (leak)' })
    if (-not $p.HasExited) { $p.Kill() }

    # ---- settings ----
    $p = Launch $scr '/c'
    for ($i = 0; $i -lt 60 -and $p.MainWindowHandle -eq 0 -and -not $p.HasExited; $i++) { Start-Sleep -Milliseconds 250; $p.Refresh() }
    if ($p.MainWindowHandle -ne 0) {
        Start-Sleep -Seconds 2
        [VerifyNative]::Capture($p.MainWindowHandle, (Join-Path $out "$name-settings.png"))
        Record $name 'settings' $true "window '$($p.MainWindowTitle)'"
    } else { Record $name 'settings' $false 'no dialog window within 15 s' }
    if (-not $p.HasExited) { $p.Kill() }

    # ---- real Windows launch ----
    if ($WindowsLaunch) {
        $desk = 'HKCU:\Control Panel\Desktop'
        $had = (Get-ItemProperty $desk).PSObject.Properties.Name -contains 'SCRNSAVE.EXE'
        $orig = (Get-ItemProperty $desk).'SCRNSAVE.EXE'
        try {
            # If a screensaver is ALREADY running, Windows ignores "start the
            # screensaver now" and this check fails for the wrong reason. That
            # happens for real: the person keeps their hands off the mouse for
            # the test, so their own screensaver starts on its idle timer
            # partway through. Clear it first, and say so.
            $already = @(Get-Process | Where-Object { $_.Path -like '*.scr' })
            if ($already) {
                $already | Stop-Process -Force -ErrorAction SilentlyContinue
                Start-Sleep -Milliseconds 800
                Write-Host "  (stopped a screensaver that was already running: $($already[0].Name); your own idle timer probably started it)"
            }
            Set-ItemProperty $desk -Name 'SCRNSAVE.EXE' -Value $scr
            # WM_SYSCOMMAND (0x0112) + SC_SCREENSAVE (0xF140): "start the screensaver now",
            # exactly what Windows does when you go idle.
            [VerifyNative]::SendMessage([VerifyNative]::GetDesktopWindow(), 0x0112, [IntPtr]0xF140, [IntPtr]::Zero) | Out-Null
            Start-Sleep -Seconds 6
            $running = @(Get-Process | Where-Object { $_.Path -eq $scr })
            # A real launch runs on a separate secure desktop that cannot be photographed,
            # so "running and responding" is the evidence.
            Record $name 'windows' ($running.Count -gt 0) $(if ($running) { "running, responding=$($running[0].Responding)" } else { 'not running 6 s after SC_SCREENSAVE' })
            $running | Stop-Process -ErrorAction SilentlyContinue
        } finally {
            if ($had) { Set-ItemProperty $desk -Name 'SCRNSAVE.EXE' -Value $orig }
            else { Remove-ItemProperty $desk -Name 'SCRNSAVE.EXE' -ErrorAction SilentlyContinue }
        }
    }
}

Write-Host ""
$results | Format-Table -AutoSize | Out-Host
Write-Host "Pictures: $out"
if ($results.Result -contains 'FAIL') { exit 1 }

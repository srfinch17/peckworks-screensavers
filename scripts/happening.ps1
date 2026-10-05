<#
.SYNOPSIS
    Renders one "happening" (a small random event in a scene) at chosen moments, as PNG files.

.DESCRIPTION
    A happening normally comes on at random, minutes apart, which makes it
    slow to look at while you are building it. This script asks the
    screensaver to play just ONE happening, starting at time zero, and saves
    a picture of it at each moment you name. It does not take over the screen.

    How it works: it sets the environment variable PECKWORKS_HAPPENING to the
    happening's name (the engine's HappeningDirector reads it), then runs the
    screensaver's /snapshot mode once per moment.

    It runs the normal build output (src\<Saver>\bin\Release\...), so build
    first, or pass -Build.

.PARAMETER Name
    The happening's name as listed in the saver's cast (for Halloween:
    src\Halloween\Happenings\Cast.cs), for example ShootingStar.

.PARAMETER At
    The moments to capture, in seconds since the happening began.

.PARAMETER Crop
    Optional: left, top, width, height of the part of the picture to keep,
    as FRACTIONS of the screen (0 to 1). "-Crop 0.5,0,0.5,0.5" keeps the top
    right quarter. A crop is saved next to the full picture as *-crop.png.

.PARAMETER Sweep
    Instead of one happening, run EVERY happening in the saver's cast for
    -SweepSeconds each and print the average time per frame, with a quiet
    baseline (nothing on) before and after. This is the cost and crash check:
    a happening whose number stands well above the baseline is stamping too
    many pixels. Run it at 3840x2160 (where cost shows) and at 152x112 (the
    preview box, where crashes show).

.EXAMPLE
    .\scripts\happening.ps1 -Name ShootingStar -At 0.4,0.9,1.4
    .\scripts\happening.ps1 -Sweep -Width 3840 -Height 2160
    .\scripts\happening.ps1 -Name MoonFace -At 2,4 -Width 1080 -Height 1920 -Crop 0.4,0.1,0.6,0.3 -Build
#>
param(
    [string]$Name,
    [double[]]$At = @(1.0),
    [string]$Saver = 'Halloween',
    [int]$Width = 1920,
    [int]$Height = 1080,
    [double[]]$Crop,
    [string]$OutDir,
    [switch]$Build,
    [switch]$Sweep,
    [double]$SweepSeconds = 15
)
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $repo 'snapshots\happenings' }
New-Item -ItemType Directory -Force $OutDir | Out-Null

if ($Build) {
    dotnet build (Join-Path $repo "src\$Saver") -c Release -v q --nologo | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $Saver" }
}
$exe = Join-Path $repo "src\$Saver\bin\Release\net10.0-windows\$Saver.exe"
if (-not (Test-Path $exe)) { throw "Not built yet: $exe. Run with -Build." }

if ($Sweep) {
    # The names come straight from the cast file: every "nameof(Something)" in it.
    $cast = Join-Path $repo "src\$Saver\Happenings\Cast.cs"
    $names = [regex]::Matches((Get-Content $cast -Raw), 'nameof\((\w+)\)') | ForEach-Object { $_.Groups[1].Value }
    function Measure-Run([string]$happening, [double]$seconds) {
        $env:PECKWORKS_HAPPENING = $happening          # an empty value means "no test mode": the ordinary random run
        $label = if ($happening) { $happening } else { 'quiet' }
        $timing = (Join-Path $OutDir "sweep-$label.png") + '.txt'
        # "Did it finish?" is answered by the timing file being NEWER than this
        # moment, not by a file existing: one from an earlier sweep may be there.
        $started = Get-Date
        $p = Start-Process -FilePath $exe -ArgumentList '/snapshot', "`"$(Join-Path $OutDir "sweep-$label.png")`"", $Width, $Height, $seconds.ToString([Globalization.CultureInfo]::InvariantCulture) -Wait -PassThru
        if ($p.ExitCode -ne 0 -or -not (Test-Path $timing) -or (Get-Item $timing).LastWriteTime -lt $started) { return "CRASHED (exit code $($p.ExitCode))" }
        return ((Get-Content $timing) -replace '^.*average ', '' -replace ' \(.*$', '')
    }
    try {
        # Nothing comes on in the first 3 seconds of a normal run, so 2.9 s is the quiet baseline.
        Write-Host ("{0,-18} {1}" -f '(quiet baseline)', (Measure-Run '' 2.9))
        foreach ($n in $names) { Write-Host ("{0,-18} {1}" -f $n, (Measure-Run $n $SweepSeconds)) }
        Write-Host ("{0,-18} {1}" -f '(quiet baseline)', (Measure-Run '' 2.9))
    } finally {
        $env:PECKWORKS_HAPPENING = $null
    }
    return
}
if (-not $Name) { throw "Give -Name <happening>, or -Sweep to run them all." }

$env:PECKWORKS_HAPPENING = $Name
try {
    foreach ($t in $At) {
        $seconds = $t.ToString([Globalization.CultureInfo]::InvariantCulture)
        $png = Join-Path $OutDir "$Name-${Width}x${Height}-$seconds.png"
        Remove-Item $png -ErrorAction SilentlyContinue
        # -Wait matters: a windowed program returns at once otherwise, before the PNG exists.
        $p = Start-Process -FilePath $exe -ArgumentList '/snapshot', "`"$png`"", $Width, $Height, $seconds -Wait -PassThru
        if ($p.ExitCode -ne 0 -or -not (Test-Path $png)) { throw "$Name at $seconds s: no picture (exit code $($p.ExitCode)). Is the name in the cast?" }
        Write-Host "$png  ($((Get-Content "$png.txt") -replace '^.*average', 'average'))"

        if ($Crop) {
            Add-Type -AssemblyName System.Drawing
            $bmp = [System.Drawing.Bitmap]::FromFile($png)
            try {
                $rect = [System.Drawing.Rectangle]::new([int]($Crop[0] * $bmp.Width), [int]($Crop[1] * $bmp.Height),
                                                        [int]($Crop[2] * $bmp.Width), [int]($Crop[3] * $bmp.Height))
                $rect.Intersect([System.Drawing.Rectangle]::new(0, 0, $bmp.Width, $bmp.Height))
                $part = $bmp.Clone($rect, $bmp.PixelFormat)
                $cropPng = $png -replace '\.png$', '-crop.png'
                $part.Save($cropPng)
                $part.Dispose()
                Write-Host $cropPng
            } finally { $bmp.Dispose() }
        }
    }
} finally {
    Remove-Item Env:PECKWORKS_HAPPENING -ErrorAction SilentlyContinue
}

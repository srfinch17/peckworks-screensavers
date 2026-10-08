<#
.SYNOPSIS
    Builds each screensaver into a single, self-contained .scr file in .\dist

.DESCRIPTION
    What "publish" means here, in plain terms:

    A normal build leaves a pile of files (Sakura.exe, Sakura.dll,
    Peckworks.Screensavers.Core.dll, config files...) and needs .NET installed
    on the PC to run. A screensaver has to be ONE file that Windows can run from
    C:\Windows\System32, so we ask .NET to:

      -r win-x64                  build for 64-bit Windows specifically
      --self-contained true       pack the .NET runtime itself inside, so the PC
                                  does not need .NET installed at all
      PublishSingleFile=true      glue everything into one .exe
      EnableCompressionInSingleFile=true
                                  zip that one file up to shrink it
      IncludeNativeLibrariesForSelfExtract=true
                                  also pack the few non-.NET helper DLLs inside

    Then we rename Name.exe to Name.scr. That rename is the whole difference
    between "a program" and "a screensaver" as far as Windows cares.

    Every folder under src\ except the Core engine is a screensaver.

.PARAMETER Saver
    Which screensaver(s) to build, by folder name (MatrixRain, Sakura, SakuraDusk, Halloween, Christmas, CabinByStream).
    Leave it out to build all of them.

.EXAMPLE
    .\scripts\publish.ps1                # all of them
    .\scripts\publish.ps1 -Saver Sakura  # just one
#>
param([string[]]$Saver)
$ErrorActionPreference = 'Stop'

$repo   = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repo 'dist'
New-Item -ItemType Directory -Force $outDir | Out-Null

$all = Get-ChildItem (Join-Path $repo 'src') -Directory |
    Where-Object { $_.Name -ne 'Peckworks.Screensavers.Core' } |
    ForEach-Object Name
if (-not $Saver) { $Saver = $all }

foreach ($name in $Saver) {
    if ($name -notin $all) { throw "Unknown screensaver '$name'. Known: $($all -join ', ')" }

    $project = Join-Path $repo "src\$name\$name.csproj"
    $tempDir = Join-Path $repo "src\$name\bin\publish"

    Write-Host "Publishing $name (self-contained, single file)..."
    dotnet publish $project -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -o $tempDir | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $name" }

    $scr = Join-Path $outDir "$name.scr"
    Copy-Item (Join-Path $tempDir "$name.exe") $scr -Force
    $sizeMb = [math]::Round((Get-Item $scr).Length / 1MB, 1)
    Write-Host "Done: $scr ($sizeMb MB)"
}

Write-Host ""
Write-Host "Try one:  right-click a .scr in dist\ and choose Test"
Write-Host "Install:  .\scripts\install.ps1"

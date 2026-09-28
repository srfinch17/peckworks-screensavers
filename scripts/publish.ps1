<#
.SYNOPSIS
    Builds MatrixRain into a single, self-contained MatrixRain.scr file in .\dist

.DESCRIPTION
    What "publish" means here, in plain terms:

    A normal build leaves a pile of files (MatrixRain.exe, MatrixRain.dll,
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

    Then we rename MatrixRain.exe to MatrixRain.scr. That rename is the whole
    difference between "a program" and "a screensaver" as far as Windows cares.

.EXAMPLE
    .\scripts\publish.ps1
#>
$ErrorActionPreference = 'Stop'

$repo    = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'src\MatrixRain\MatrixRain.csproj'
$outDir  = Join-Path $repo 'dist'
$tempDir = Join-Path $repo 'src\MatrixRain\bin\publish'

Write-Host "Publishing MatrixRain (self-contained, single file)..."
dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -o $tempDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

New-Item -ItemType Directory -Force $outDir | Out-Null
$scr = Join-Path $outDir 'MatrixRain.scr'
Copy-Item (Join-Path $tempDir 'MatrixRain.exe') $scr -Force

$sizeMb = [math]::Round((Get-Item $scr).Length / 1MB, 1)
Write-Host ""
Write-Host "Done: $scr ($sizeMb MB)"
Write-Host "Try it:   right-click the .scr and choose Test, or run: & '$scr' /s"
Write-Host "Install:  .\scripts\install.ps1"

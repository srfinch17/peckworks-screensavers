# Peckworks Screensavers

Native Windows screensavers written in C# (.NET 10, Windows Forms), plus a small reusable engine
for making more of them.

## Matrix Rain

A close replica of the "digital rain" from *The Matrix* (1999): mirrored half-width katakana,
near-white flickering leading characters, fading green tails, characters that mutate in place,
drops at varied speeds and brightness, and a soft phosphor glow.

![Matrix Rain](docs/images/matrix-rain.png)

- Runs on every monitor, at native resolution (4K included).
- Live preview in Windows' Screen Saver Settings.
- Settings dialog with a live preview: speed, density, character size, glow.
- One self-contained `.scr` file. The PC does not need .NET installed.

## Install

```powershell
.\scripts\install.ps1            # build, copy into System32 (one admin prompt), open Screen Saver Settings
.\scripts\install.ps1 -Activate  # same, and also select it with a 5-minute wait
```

Then pick **MatrixRain** in the list if it isn't already selected.

No-admin alternative: run `.\scripts\publish.ps1`, then right-click `dist\MatrixRain.scr` in
File Explorer and choose **Install**. (Windows then uses the file where it sits, so leave it there.)

Remove it with `.\scripts\uninstall.ps1`.

## Try it without installing

```powershell
.\scripts\publish.ps1
.\dist\MatrixRain.scr /s                               # full screen; move the mouse to exit
```

For development, run the plain build output (not the `.scr`; see the gotcha in the docs):

```powershell
dotnet build -c Release
$exe = ".\src\MatrixRain\bin\Release\net10.0-windows\MatrixRain.exe"
& $exe /window                                         # resizable window, Esc to close
& $exe /c                                              # settings dialog
& $exe /snapshot out.png 1920 1080 8                   # render 8 s off-screen, save a PNG + timing
```

## How it works

Start with **[docs/HOW_SCREENSAVERS_WORK.md](docs/HOW_SCREENSAVERS_WORK.md)**: a plain-language
guide to what a screensaver is, how Windows talks to it, and how the rain effect is built. Then
read `src/MatrixRain/MatrixRainScene.cs`, which is commented for a first-time reader.

## Layout

```
src/Peckworks.Screensavers.Core/   the engine (command line, windows, drawing, glow)
src/MatrixRain/                    the Matrix Rain screensaver
scripts/                           publish / install / uninstall
docs/                              the how-it-works guide
```

## Performance (measured, i7-8850H laptop)

| Resolution | Time per frame (update + render) | Frame rate headroom |
|------------|----------------------------------|---------------------|
| 1920x1080  | about 10 ms                      | 100 fps             |
| 3840x2160  | about 29 ms                      | 34 fps (the film itself is 24 fps) |

Measure your own with `/snapshot`; the timing lands in `out.png.txt`.

# Adding happenings to a screensaver

A **happening** is one small thing that occurs now and then in a scene: a shooting star, a ghost
rising from a grave, a firework. Halloween has nineteen. This page is the recipe for adding more,
to Halloween or to any other screensaver in this repo.

Think of a cuckoo clock. Most of the time the little door is shut. Now and then it opens, the
bird does its routine, and the door shuts. Each happening is one bird. The **director** is the
clockwork that decides when each door opens.

## The pieces

| Piece | Where | What it does |
|-------|-------|--------------|
| `Happening` | `src/Peckworks.Screensavers.Core/Happenings.cs` | The base class every happening extends. |
| `HappeningDirector` | same file | Shuffles the happenings like a deck of cards and deals one every few seconds. |
| `FrameBuffer.Line` | `Core/FrameBuffer.cs` | Draws a thin line straight into the picture (spider silk, a star's tail). |
| `Sprite` | `Core/Sprite.cs` | A small picture painted once and stamped each frame. `Sprite.Glow` makes a soft light. |
| Scenery facts | the saver's painter, for example `HalloweenScenery` in `src/Halloween/HalloweenPainter.cs` | Where things are: the moon, each tombstone, the ground height, the stencils. |
| The cast | `src/Halloween/Happenings/Cast.cs` | The list of every happening, one line each. |
| The happenings | `src/Halloween/Happenings/*.cs` | One class per file. |
| `scripts/happening.ps1` | | Renders one happening at chosen moments to PNG files, for looking at. |

## How a happening works

A happening is a class with four parts:

```csharp
internal sealed class ShootingStar : Happening
{
    public override float Seconds => 1.7f;              // how long one showing lasts
    public override string? Claims => null;             // optional: a prop it takes over ("moon")
    public ShootingStar(HalloweenScenery s) { ... }     // paint sprites here, ONCE
    public override void Begin(Random rng) { ... }      // roll this showing's dice here
    public override void Draw(FrameBuffer fb, float t)  // draw it as it looks t seconds in
}
```

The one rule that keeps them simple: **`Draw` works everything out from `t` alone**, the number
of seconds since this showing began. It keeps no running tally from frame to frame. Where is the
star? `start + journey * (t / Seconds)`. How faded is the ghost? `Fade(t, Seconds, 1, 2)`.
Because of that rule, any moment of any happening can be rendered on its own, which is how they
are tested.

For things made of many bits (firework sparks, a swarm of bats): in `Begin`, store each bit's
starting direction and speed in an array. In `Draw`, work out where each bit is at time `t` from
those numbers. Do not move them a little each frame.

Read `ShootingStar.cs` (a sky happening that goes behind scenery) and `SkeletonEyes.cs` (one
fixed to a prop) before writing one. They are the pattern.

## Rules (each one prevents a known way these go wrong)

1. **Never use a fixed pixel position.** The scenery is different on every launch: the dead
   tree grows a new shape, the hills move, the screen may be tall and narrow. Ask the scenery
   facts where things are.
2. **Positions are fractions of the width and height. SIZES are fractions of `U`** (the size
   unit: the screen height, or less on a tall screen). Something sized by height alone swallows
   a portrait monitor.
3. **Anything standing on the ground asks the ground**: `s.Ground.YAt(x)`.
4. **To pass behind scenery, use a stencil.** `sprite.Draw(fb, x, y, opacity, s.OpenSky)` and
   `fb.Line(..., s.OpenSky)` only touch pixels where nothing was painted over the sky. Use
   `OpenFromHouse` for things at the haunted house (in front of the house, behind anything
   nearer). Things on the graveyard ground need no stencil: they are in front of everything in
   the backdrop, and the candles, bats and leaves are drawn after the happenings, over them.
5. **Paint sprites in the constructor, never in `Draw`.** Painting is slow; stamping is fast.
6. **Give every size a floor in pixels** (`Math.Max(2, (int)(u * 0.01f))`). The preview box in
   Windows' Screen Saver Settings is about 150 pixels wide, and the same code runs there.
   Nothing may crash, divide by zero, or index an empty list at that size.
7. **Arrive and leave softly.** Multiply opacity by `Fade(t, Seconds, in, out)`, and put
   `Smooth(...)` on any movement that starts or stops. Nothing pops.
8. **A flip-book needs many pages.** Anything that cycles (wings, legs, a wavy hem) needs 16 to
   24 poses to read as fluid. Six poses visibly snaps.
9. **Animation under one pixel is no animation.** Give a bob or sway a floor of a pixel or two.
10. **Dark on dark is invisible.** The silhouette color (12, 8, 20) shows against the sky and the
    mist, but not against the foreground ground. Check that the thing reads where it is.
11. **To repaint over something in the backdrop** (a new face on a pumpkin), the sprite must sit
    on exactly the same pixels. Stamps land on whole pixels, so: pick the sprite's whole-number
    left and top first (`left = (int)MathF.Floor(x - halfWidth) - 2`), then paint inside the
    sprite at `(x - left, y - top)`, and stamp with `Draw(fb, left, top)`, not `DrawCentered`.
12. **Claim a shared prop.** Two happenings that both change the moon say
    `Claims => "moon"`, and the director will not run them together.
13. **Size it to be seen, not to take over.** It should catch the eye from across a room and
    still be a small surprise, not the main act.
14. **Nothing to show? End at once.** If a showing finds it has nothing to draw (no branch to
    hang a web on), make `Seconds` tiny for that showing so the director moves on.
15. Comments are heavy and plain, like the rest of the repo: one idea at a time, a real-world
    comparison where it helps, no em or en dashes.

## Looking at one

```powershell
# Three moments of the shooting star, at 1920x1080, into snapshots\happenings\
.\scripts\happening.ps1 -Name ShootingStar -At 0.5,1.0,1.4 -Build

# A tall screen, keeping only part of the picture (fractions: left, top, width, height)
.\scripts\happening.ps1 -Name BloodMoon -At 3,6 -Width 1080 -Height 1920 -Crop 0.4,0.1,0.6,0.3
```

Each run is a new random scene, so three runs are three different trees and hills. Before
calling a happening done, look at: three or four moments (start, middle, end), two or three
separate runs, a tall screen (1080x1920) and a very wide one (2560x1080).

Each run is its own launch. `-At 1,5,10` is therefore three different random scenes, not three
moments of one scene.

**Cost and crashes.** Every pixel a stamp covers costs time, so one wide soft glow can cost more
than the whole rest of the scene (the first blood moon halved the frame rate on a 4K screen). The
sweep runs every happening and prints its time per frame next to a quiet baseline:

```powershell
.\scripts\happening.ps1 -Sweep -Width 3840 -Height 2160   # cost: anything well above the baseline is too big
.\scripts\happening.ps1 -Sweep -Width 152 -Height 112     # the preview box: nothing may crash
```

Cures for a costly one: make the glow smaller, punch out the part something else covers (see
`BloodMoon.cs`), and skip the stamp while it is too faint to see (see `Lightning.cs`).

To watch them all for real, play the showreel, every happening back to back:

```powershell
$env:PECKWORKS_HAPPENING = 'all'; Start-Process .\dist\Halloween.scr -ArgumentList /s; Remove-Item Env:PECKWORKS_HAPPENING
```

(Moving the mouse closes it. Use a happening's name instead of `all` to loop just that one.)

## Adding one happening to a saver that already has them

1. Write the class in the saver's `Happenings` folder.
2. Add its line to `Cast.cs`.
3. Look at it with `scripts\happening.ps1` (the list above).
4. Update the list of happenings in `README.md`.

## Giving a saver its first happenings

This is what was done for Halloween, in order.

1. **Decide the list.** Write one short paragraph per happening: what happens, how long, where,
   and what it goes behind. About twenty is a good number: with one dealt every 5 to 12 seconds, the
   whole deck takes about three minutes to come round.
2. **Expose the scenery facts** those paragraphs need. The painter already knows where it put
   the moon; add those numbers to the scenery object it returns (see the "Facts for the
   happenings" block in `HalloweenScenery`). Add stencils with the copy-and-compare trick
   (`Brushwork.Unchanged`) at each depth something must pass behind.
3. **Wire the director into the scene**: build it in the constructor from the cast, call
   `Update` in `Update`, and call `Draw` in `Render` right after the backdrop is copied.
4. **Add the settings knob** ("Surprises", 0 to 300 percent, 0 = off).
5. **Write two happenings by hand** as the pattern, and look at them.
6. **Build the rest.** Each is one self-contained file, so they can be built side by side.
7. **Look at every one** (contact sheet of renders), fix, then watch the showreel.
8. **Sweep** for cost and crashes (above), and fix anything that stands out.
9. **Verify** as for any scene change: `publish.ps1`, `verify.ps1 -WindowsLaunch`, measure the
   frame time at 1080p and 4K with and without, update the README.

### Building many at once with helper agents

When an AI assistant builds a batch, the split that worked: the main model writes the list, the
engine wiring, the scenery facts and the two pattern happenings, then reviews every render. The
other happenings go to cheaper helper agents, three or four related ones each. To keep helpers
from breaking each other's builds:

- Put a stub class for every planned happening in the folder first, so the cast compiles.
- Each helper works in its **own copy** of the repo (outside the repo folder), builds and
  renders there, and copies back only its own finished `.cs` files.
- Helpers do not edit shared files. If one needs a scenery fact that is missing, it says so in
  its report, and the main model adds it.
- Tell each helper the paint ORDER around its props, not only where they are. (Halloween's mist
  is painted over the foot of the haunted house; the brief did not say so, and that helper lost
  a round finding out.)
- Each helper must look at its own renders, at several moments, seeds and screen shapes, and
  report honestly what is still weak.

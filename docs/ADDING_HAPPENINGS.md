# Adding happenings to a screensaver

A **happening** is one small thing that occurs now and then in a scene: a shooting star, a ghost
rising from a grave, a firework. Halloween has nineteen; Christmas, Sakura and Sakura Dusk have
twenty each. This page is the recipe for adding more, to any of them or to another screensaver in
this repo.

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
| Scenery facts | the saver's painter: `HalloweenScenery` in `src/Halloween/HalloweenPainter.cs`, `ChristmasScenery` in `src/Christmas/ChristmasPainter.cs` | Where things are: the moon, each tombstone, the ground height, the stencils. |
| The cast | `src/<Saver>/Happenings/Cast.cs` | The list of every happening, one line each. |
| The happenings | `src/<Saver>/Happenings/*.cs` | One class per file. |
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
2. **Positions are fractions of the width and height. SIZES and SPEEDS are fractions of `U`**
   (the size unit: the screen height, or less on a tall screen). Something sized by height
   alone swallows a portrait monitor. Something that crosses the screen "in 11 seconds" runs
   twice as fast on a screen twice as wide, its legs a blur: give it a speed in `U` per second
   and work the seconds out from the distance (`Fox.cs`, `ToyTrain.cs`).
3. **Anything standing on the ground asks the ground WHERE TO STAND, which is not the same as
   where the ground starts.** `s.Ground.YAt(x)` is the hill's crest: its FAR edge, the line it
   makes against whatever is behind it. Trees and houses are planted a little in front of that
   line. A walker whose feet are on the crest is behind every tree it is drawn over, and seems
   to trot along their lowest branches. Give the scenery a "walk here" fact that is nearer than
   everything planted (`ChristmasScenery.WalkY`), and stand on that. Check a walker AT a tree
   and AT a building, zoomed in, not only in the open.
3b. **Trust the picture, not the formula that painted it.** A tree's painted edge is ragged; the
   formula for its width is only a guide. Anything that must sit ON a painted thing tests the
   pixels there (a stencil says "something is painted here") and tries again if it missed
   (`SnowSlide.cs`).
4. **To pass behind scenery, use a stencil.** `sprite.Draw(fb, x, y, opacity, s.OpenSky)` and
   `fb.Line(..., s.OpenSky)` only touch pixels where nothing was painted over the sky. Use
   `OpenFromHouse` for things at the haunted house (in front of the house, behind anything
   nearer). Things on the graveyard ground need no stencil: they are in front of everything in
   the backdrop, and the candles, bats and leaves are drawn after the happenings, over them.
   Two limits to know. A stencil is yes or no per pixel, and the soft edge pixels of a thin
   branch (part branch, part sky) count as "no": behind a big shape much darker or redder than
   the sky they show as a thin bright rim round every twig. Keep big dark shapes clear of the
   twigs (`HillMonster.cs`), or repair the stencil by testing the pixels (`BloodMoon.cs`). And
   the mist does not count as "in front" (it is haze), so a stamp low on the horizon is drawn
   over the haze, not under it.
5. **Paint sprites in the constructor, never in `Draw`.** Painting is slow; stamping is fast.
   The director runs the constructors on a helper thread while the scene is already playing,
   so a constructor must only READ the scenery, never change it, and must not roll dice (that
   is what `Begin` is for).
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
12. **Claim a shared prop, or a shared patch of ground.** Two happenings that both change the
    moon say `Claims => "moon"`, and the director will not run them together. The same goes for
    two that can stand on the same spot: happenings are drawn in the order they began, with no
    idea of who is in front, so a cat and a zombie hand in one place are drawn through each
    other. Halloween's ghost, hand, cat and pumpkin face all claim `"graveyard"`. When you add
    one, check it against EVERY existing one for a shared prop or place, not only the obvious
    pair. A happening may claim several things, comma separated (`"sun,costly"`); it then never
    runs with anything that claims any one of them. Use that for COST too: give every happening
    that is dear to draw at 4K one shared extra claim (`"costly"`), so at most one of them is
    ever on screen. (A single claim per happening let two dear ones that each held a different
    prop run together, and their costs added up.)
13. **Size it to be seen, not to take over.** It should catch the eye from across a room and
    still be a small surprise, not the main act.
14. **Nothing to show? Say so before you are dealt, or end at once.** If a happening can tell
    in advance that it has nothing to show (Santa is off screen, so there is no sleigh to drop
    a present from), return false from `CanBegin`: the director leaves its card in the deck and
    deals another, so it still comes round as often as the rest. `CanBegin` may also be false
    for the whole run on some screens (a rainbow a tall screen's mountain would hide): when
    every card left in the deck can only wait on `CanBegin`, the director starts a fresh round,
    so such a card never jams the deck. If it only finds out in `Begin` (no branch to hang a
    web on), make `Seconds` tiny (under half a second) for that showing; the director deals
    another within a second. Either way, COUNT how often a happening has nothing to show, over
    many seeds and screen shapes: one that is empty four times in five is one the owner never
    sees, and still listed in the README as if he would.
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
moments of one scene, unless you fix the dice: `-Seed 7` paints the same scene every time (any
whole number; a different number is a different scene). Use it to watch one happening unfold in
one place, and to re-check a fix on the exact scene that showed the fault.

**Cost and crashes.** Every pixel a stamp covers costs time, so one wide soft glow can cost more
than the whole rest of the scene (the first blood moon halved the frame rate on a 4K screen). The
sweep runs every happening right through and prints three times next to a quiet baseline: the
average per frame, the worst second, and the slowest single frame. Read the worst second. A
firework is slow for half a second at each burst, which an average over the whole showing hides.
(The slowest frame catches a stutter: one frozen frame that both of the others hide.)

```powershell
.\scripts\happening.ps1 -Sweep -Width 3840 -Height 2160   # cost: anything well above the baseline is too big (takes a quarter of an hour)
.\scripts\happening.ps1 -Sweep -Width 152 -Height 112     # the preview box: nothing may crash (takes seconds)
```

Cures for a costly one: make the glow smaller, punch out the part something else covers (see
`BloodMoon.cs`), and skip the stamp while it is too faint to see (see `Lightning.cs`). Then
give the two or three dearest ones a shared claim (`"costly"` in Christmas), so they never run
at the same time and their costs never add up in one frame.

To watch them all for real, play the showreel, every happening back to back:

```powershell
$env:PECKWORKS_HAPPENING = 'all'; Start-Process .\dist\Halloween.scr -ArgumentList /s; Remove-Item Env:PECKWORKS_HAPPENING
```

(Moving the mouse closes it. Use a happening's name instead of `all` to loop just that one.)

## Adding one happening to a saver that already has them

1. Write the class in the saver's `Happenings` folder.
2. Add its line to `Cast.cs`.
3. Look at it with `scripts\happening.ps1` (the list above).
4. Update the list of happenings in `README.md`. (For another saver, give `happening.ps1`
   its name: `-Saver Christmas`.)

## Giving a saver its first happenings

This is what was done for Halloween and then Christmas, in order. The second time took a
fraction of the first: the engine, the script and this page already existed, so the work was the
list, the facts, the wiring, two pattern happenings, and the helpers.

1. **Decide the list.** Write one short paragraph per happening: what happens, how long, where,
   and what it goes behind. About twenty is a good number: with one dealt every 5 to 12 seconds, the
   whole deck takes about three minutes to come round.
2. **Expose the scenery facts** those paragraphs need. The painter already knows where it put
   the moon; add those numbers to the scenery object it returns (see the "Facts for the
   happenings" block in `HalloweenScenery`). Add stencils with the copy-and-compare trick
   (`Brushwork.Unchanged`) at each depth something must pass behind.
3. **Wire the director into the scene**: build it in the constructor from the cast, call
   `Update` in `Update`, and call `Draw` in `Render`. If the scene has no moving actors of its
   own that a happening could be behind or in front of, one call right after the backdrop is
   copied will do (Halloween). If it has, the happenings need LAYERS, because they are not all
   at one depth: Christmas calls `Draw(fb, 0)` before Santa for the far ones (fireworks,
   northern lights), `Draw(fb, 1)` after Santa and the bulb glows for the near ones (the fox,
   the owl on its twig), and `Draw(fb, 2)` after the falling snow for frost on the glass. Each
   happening says which it is with `Layer`. With a single call, a sleigh that flies behind a
   branch was painted over an owl sitting on that branch. For every happening ask: what in
   this scene moves, and am I in front of it or behind it? If a happening must
   know where a moving actor is (a present falling from the sleigh), the scene writes that
   actor's position into the scenery facts every frame before the happenings draw, and the
   happening reads it in `Begin`. Use the engine's dice (`HappeningDirector.SceneRandom()`)
   for the scene's Random, so `-Seed` works.
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

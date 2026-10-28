# How a Windows screensaver works (and how this one is built)

A plain-language tour. Read top to bottom once; after that, each section stands alone.

## 1. A screensaver is just a program with a funny file extension

A screensaver is an ordinary `.exe` program renamed to `.scr`. That's it. There is no special
screensaver file format, and nothing to register.

Windows builds its Screen Saver Settings list by looking at every `.scr` file in
`C:\Windows\System32`. Copy a `.scr` there and it shows up in the list.

## 2. Windows talks to it through the command line

Windows never calls into your code. It just **runs your program** and leaves a short note on the
command line saying what it wants. There are only three notes:

| Note          | When Windows sends it                               | What we do                                                     |
|---------------|-----------------------------------------------------|----------------------------------------------------------------|
| `/s`          | You went idle long enough (or clicked Preview)      | Cover every monitor, play the rain, quit on mouse or keyboard  |
| `/p 12345`    | The Screen Saver Settings dialog is showing         | Draw a live miniature inside the little monitor picture        |
| `/c`          | You clicked "Settings..."                           | Show our settings dialog                                       |

`12345` stands for a window ID number. Windows calls it an **HWND** ("handle to a window"): the
number Windows uses to identify one particular window. For `/p`, it identifies the little monitor
picture we're supposed to draw inside.

Windows is sloppy about spelling, so real launches include `/S`, `-s`, `/c:12345`, `/p:12345`,
and plain nothing (which means `/c`). `Core/CommandLine.cs` accepts them all.

Two extra notes exist only for development:

| Note                                  | What it does                                                      |
|---------------------------------------|-------------------------------------------------------------------|
| `/window`                             | Plays in a normal resizable window that ignores the mouse. Esc closes it. |
| `/snapshot out.png [w h seconds]`     | Renders with no window at all, saves a PNG, and writes the time per frame to `out.png.txt` (average, worst second, slowest frame, garbage collections); with `PECKWORKS_SECTIONS` set to a file, also what each part of the frame cost; with `PECKWORKS_STARTLOG` set to a file, Sakura Pond also writes how long each part of the scene took to BUILD (the painter, the boughs, the lawn's pages) |

> **Gotcha found while building this:** if you launch a `.scr` by double-clicking it, or with
> PowerShell's `Start-Process`, Windows' file association **replaces your arguments with `/S`**.
> So `Start-Process MatrixRain.scr /c` quietly runs the full-screen saver instead. To test the
> other modes, run the `.exe` from `src\MatrixRain\bin\...`, or launch with `UseShellExecute = false`.
> `scripts\verify.ps1` does exactly that and checks every mode for you.

## 3. The "wake up" rule

What makes a full-screen animation a *screensaver* is that it vanishes the moment you touch
anything. `ScreensaverWindow` quits on any key press, any click, and any mouse movement.

There's one trap. Windows sends a "mouse moved" message the instant our window appears, even
though nobody touched the mouse, and a desk bump can nudge a mouse by one pixel. So we remember
where the pointer was the first time we saw it, and only quit once it has traveled more than 10
pixels from there.

## 4. The engine vs. the screensaver

The code is split in two so the next screensaver is easy to make:

```
src/
  Peckworks.Screensavers.Core/     the ENGINE: knows how to BE a screensaver
    CommandLine.cs                 reads /s /p /c
    ScreensaverApp.cs              the front door: picks a mode and starts it
    ScreensaverWindow.cs           full-screen / preview / windowed; the wake-up rule
    SceneView.cs                   the "little TV": runs the update-render loop, one frame per screen refresh
    FrameBuffer.cs                 our private sheet of pixels, and the fast copy to screen
    BloomEffect.cs                 the soft glow
    NativeMethods.cs               the few raw Windows functions .NET doesn't wrap
    IScreensaverScene.cs           the contract a screensaver fills in
    ScreensaverSettings.cs         a list of knobs, saved in the registry
    SettingsDialog.cs              the Settings dialog: live preview + one slider per knob
    Sprite.cs                      a small picture painted once and stamped each frame (bats, Santa, glows)
    FrameSections.cs               a lap timer for a frame: which part costs what (off unless PECKWORKS_SECTIONS is set)
    Happenings.cs                  small random events: the base class, and the director that deals them out
    Sakura/PetalField.cs           the falling petals (or leaves, or snow), shared by every painted scene
    Sakura/Brushwork.cs            banks, hills, trunks, blossoms, a pine, and self-growing branches
    Sakura/Boat.cs                 the little boat that crosses the water in both sakura scenes
    Sakura/Plants.cs               one Wind for a whole scene; Mottle (smooth random blotches); plants as flip-books (tufts of six kinds including wildflowers, cattails, irises); a Lawn whose carpet slides in the gusts; Boughs (painted branches laid over the frame each frame, swaying on springs)
    Minds/Mind.cs                  the parts every animal mind is built from: dials, weighted dice, one doing at a time, cooldowns (see MINDS.md)
    Minds/Space.cs                 3D steering: Chain (a body's shape for touching), Feelers (room along a line, the smallest change of course) (see MINDS.md, section 8)
    ChimneySmoke.cs                puffs that rise, cool, spread and thin (Cabin by Stream)
    Photo/PhotoBackdrop.cs         a photo fitted to the screen, plus where its water is
    Photo/WaterRipples.cs          a still brook's reflections, rippling; trout rising
    Photo/FlowingWater.cs          a running river: the fine texture carried downstream, rocks left still
    Photo/PhotoEvening.cs          evening on a daytime photo: dusk, lamplit windows, lamps
    Photo/PhotoFireflies.cs        fireflies that fly their own paths and flash, near and far
    Photo/PhotoMist.cs             soft banks of mist drifting across part of a photo

  MatrixRain/                      a screensaver built on the engine
    Program.cs                     Main(), plus the "definition" handed to the engine
    MatrixRainScene.cs             the rain simulation (start here, it's the fun part)
    GlyphAtlas.cs                  pre-drawn character stamps
    MatrixRainSettings.cs          declares its knobs

  Sakura/                          another screensaver built on the engine
    Program.cs                     Main(), plus the definition
    SakuraScene.cs                 backdrop + glints + the shared petals
    SceneryPainter.cs              paints Mount Fuji, the lake, trees, and branches once
    SakuraSettings.cs              declares its knobs
    Happenings/                    twenty small random events (Red Fuji, a kingfisher, a Shiba Inu), one class per file

  SakuraDusk/                      the same petals over a sunset pond
    Program.cs                     Main(), plus the definition
    SakuraDuskScene.cs             backdrop + golden glints + the shared petals
    DuskPainter.cs                 paints the sunset, hills, pagoda, pond, bridge, lantern, trees
    Flock.cs                       the birds: sprites that cross the sky, flapping and gliding
    SakuraDuskSettings.cs          declares its knobs
    Happenings/                    twenty more, all different (floating lanterns, Basho's frog, the first star)

  SakuraPond/                      a koi pond seen from above, under cherry branches
    Program.cs                     Main(), plus the definition (draws at most 2560 wide)
    SakuraPondScene.cs             the layers, back to front
    PondPainter.cs                 paints the pond bed and water, lily pads, branches and their shadows; calls the shore painter
    Shore.cs                       paints the corner of land: the lawn's mottled ground and carpet (clover and moss patches, worn earth), the sand edge, rocks that look like rocks, the island and the small rock formation, the fallen log (off by default)
    Shoreline.cs                   the water's edge, alive: laps up the sand and back along the bank every frame, and tells the animals where the water is right now
    Body.cs                        draws an animal (koi, snake, turtle) along its spine
    Koi.cs                         the koi: swimming, steering, beat and glide, varieties
    Minnows.cs                     schools of tiny fish that move as one, and rush to the dragonfly's rings
    Tadpoles.cs                    clutches of tadpoles in the shallows that grow legs and leave as froglets

    Turtle.cs                      the turtle: paddling, floating, diving, hauling out to bask
    Frog.cs                        the frogs: sitting on pads, rocks and the bank, leaping, kicking, floating, diving, climbing out
    Crayfish.cs                    a crayfish that comes out from under a bank rock, walks the shallow bed, feeds, and flips backward from a fish
    Prawns.cs                      a few see-through freshwater prawns on the bed of the shallows: they feed, walk, swim a little, and flick backward from a fish
    DeepDwellers.cs                the crayfish and long-armed prawns of the deep bed: they forage, skitter, fight each other, snap at a minnow that dips down, and are food for the biggest koi, a diving turtle and the diving snake
    Petals.cs                      petals falling from the blossom, landing, floating
    Ripples.cs                     rings spreading on the water, and the bow waves of swimmers at the surface
    Dragonflies.cs                 hover, dart, perch, dip, and the game with the minnows
    Swallows.cs                    swooping visits to drink
    Snake.cs                       a snake swimming across, now and then, feeling its way round things
    Lizard.cs                      a lizard dashing about the bank and basking on the rocks, now and then
    Mouse.cs                       a field mouse scurrying between cover in the grass, hiding from the snake and lizards
    TurningStamp.cs                a picture stamped at any angle (dragonflies, swallows, their shadows)
    Light.cs                       where the sun is and how we look at the pond
    SakuraPondSettings.cs          declares its knobs
Since 2026-10-27 a prawn is PREY as a tadpole is (`ISnack`, `ISnackField`: the shared `Snatch` works on
either, the predator chases a nose and never learns which): a koi at least half hungry, or the turtle
ten minutes from its last meal (`_hungerT`), may spot one within 0.1u and chase it, nosing down to the
shallows' bed with a smaller margin than usual; the prawn's own flick is its escape, the catch is the
mouth within reach of its rostrum, and a prawn eaten is gone from the bed and back at its home from under
the stones 90 to 240 s later (`Prawn.Eaten`, `Reborn`). Its mind goes on ticking while it is gone.
Since round thirty-seven the deep bed's crayfish and prawns are a third field (`DeepDwellers`, stages 6 and
7 in the log): one of the largest koi that is hungry and already below half depth, a hungry turtle on a dive
below half depth (the look 0.25u on the screen with the ground distance tested at 0.12u in its "ok": a
turtle at half depth is drawn 0.13u higher than a dweller on the bed, and a 0.12u screen look found none in
thirty stress minutes), and the diving snake (its head past 0.4 of the depth, `Snake.LookOnBed`, the head
going down to the prey's depth four times as fast as a dive eases and held there through the chase; a dive's
target is 0.3 to 0.7 and the head eases toward it slowly, so a look from 0.55 never came in ten stress
minutes) may take one; a dweller eaten is gone two to five minutes and a newcomer
of its kind walks in from the picture's edge (`Dweller.Reborn`, `DeepBed.EdgeWay`), the pond going on past
the view. A koi's catch of a prawn or a dweller now takes 0.4 off its hunger (a tadpole 0.1): until this round
a fed koi was as hungry as before and went straight back for the next.

  Halloween/                       falling leaves, bats, and jack-o'-lanterns
    Program.cs                     Main(), plus the definition
    HalloweenScene.cs              backdrop + candle flicker + bats + the shared engine dropping leaves
    HalloweenPainter.cs            paints the moon, haunted house, graveyard, pumpkins, trees
    HalloweenSettings.cs           declares its knobs
    Happenings/                    nineteen small random events, one class per file
      Cast.cs                      the list of them all
      ShootingStar.cs, ...         each one: how long it lasts, and how it looks t seconds in

  Christmas/                       falling snow, twinkling lights, and Santa
    Program.cs                     Main(), plus the definition
    ChristmasScene.cs              backdrop + Santa's sleigh + twinkling lights + the shared engine dropping snow
    ChristmasPainter.cs            paints the night sky, mountains, cabin, snowy pines, strings of lights
    ChristmasSettings.cs           declares its knobs
    Happenings/                    twenty small random events (a fox, a snowman, fireworks), one class per file

  CabinByStream/                   a cottage in a flowery meadow at dusk, aiming at a photograph's look
    Program.cs                     Main(), plus the definition
    CabinByStreamScene.cs          backdrop + light sliding down the stream + the windows' flicker + the actors below
    MeadowPainter.cs               paints the dusk sky, the far forest, the textured meadow, the stream, the cottage, the trees
    FishSchool.cs                  trout holding against the current
    Squirrels.cs                   squirrels running between the trees and climbing them, each on a plan
    Fireflies.cs                   blinking lights over the meadow, mirrored in the stream
    CabinByStreamSettings.cs       declares its knobs

  CotswoldBrook/                   RETIRED: kept as a reference, not built (RETIRED.md says why)
```

A screensaver only has to write one class with two methods:

```csharp
void Update(double elapsedSeconds);   // move the world forward this much time
void Render(FrameBuffer target);      // draw the world as it is right now
```

The engine calls those two about 60 to 100 times a second. That's how every animation works,
from a flip-book to a video game: nudge the world, draw it, repeat.

**Why "elapsed seconds" and not "one step per frame"?** Computers aren't metronomes; a frame can
take 10 ms or 30 ms. Move a raindrop "one step per frame" and it rains faster on a faster PC.
Move it "speed times elapsed time" and it rains at the same pace everywhere.

## 5. Drawing fast: the private sheet of graph paper

The screen is graph paper where each square (pixel) holds a color. `FrameBuffer` is our private
copy: one long list of numbers, one per pixel, rows laid end to end like lines in a book. The
pixel at column `x`, row `y` sits at index `y * Width + x`.

Each pixel is one number, `0x00RRGGBB` in hexadecimal: two digits each for red, green, and blue,
each running 0 to 255. Pure green is `0x0000FF00`.

We color the private sheet, then copy the whole finished sheet to the screen in one call
(`SetDIBitsToDevice`). Drawing on a hidden sheet and swapping it in whole is called **double
buffering**. It prevents flicker, because the viewer never sees a half-drawn frame.

*One frame per refresh.* The screen redraws itself at a steady rate, 60 times a second on most
screens, like the pages of a flip-book turning. The first version drew on a Windows timer that
ticked about 64 times a second, slightly out of step with the pages, so a few times a second one
drawing was never seen or one was shown twice. Falling petals hid it; a river flowing steadily
showed it as a judder (measured on a 4K laptop: 41 frames a second, gaps from 17 to 59 ms). Now
`FramePump` (in `SceneView.cs`) draws a frame and then waits for the screen to turn its page
(`DwmFlush`) before drawing the next: 57 to 58 frames a second, nearly every gap 16 to 18 ms.
When a frame takes too long to be drawn AND shown inside one page (about 16.7 ms), it misses its
page and lands on the next, 33 ms later; a scene hovering round the limit then shows at 16, 33,
16, 33, which the eye reads as a stutter, worse than an even 30. So the pump watches how long
recent frames took and, when they are too close to the page time to make it reliably, waits for
two pages per frame on purpose, every frame (an even 30), coming back to one page per frame once
frames are clearly quick again. The scene is told the real time that passed either way, so
nothing moves slower, only in bigger steps. The other lever is drawing fewer pixels: Sakura Pond's
"Draw width" setting (1920 by default) is the width it draws at on a wider screen, and Windows
enlarges the picture; on the owner's 4K laptop 2048 held 60 frames a second where 2560 held an
even 30. The default went from 2048 to 1920 when the boughs began to sway (2026-10-22): the new
build at 2048 took two pages on one frame in ten during a gust (269 of 300 frames at one page in
the on-screen log, against 292 for the build before), and at 1920 it holds 292 to 294 of 300, the
old build's figure. Splitting the boughs' work differently did not help: shifting every row on
the carpet frames and none on the tuft frames made the peak bigger, not smaller (231 and 264 of
300); it is the PEAK frame that drops, so the work has to be spread evenly, not moved.

*The first second, and the frame an animal first appears (2026-10-20).* Two kinds of stutter were
measured on the owner's 4K laptop with the frame log and the section timer, and neither was in the
painting. The first: a .NET program ships as a recipe, and each method is turned into machine code
(the JIT, "just in time") the first time it runs. By default that happens in two tiers, a quick rough
compile first and the optimised one later in the background, so for the first seconds every painter
ran its rough version at half speed: the first frames cost 15 to 20 ms instead of 9, the pump dropped
to an even 30, and because the cost then sat right on the pump's way back (62 percent of a page for
four seconds) it could stay at 30 for the whole run (299 of the first 300 frames at 33 ms). Now the
tiers are off for every saver (`src/Directory.Build.props`: `TieredCompilation` false, so every method
is compiled at full strength the first time), and the pump comes back once the cost sits under 78
percent of a page for a second and a half (it drops to 30 at 90 percent). The second: with the tiers
off, the first call of a big method pays the full compile inside the frame, 10 ms and more, and in a
screensaver the first call of the snake's or a lizard's code is the frame that animal first appears:
one dropped frame, exactly as something new came on. So at start-up `ScreensaverApp` compiles every
method of the engine and the saver on a helper thread (`CompileEverythingNow`, `RuntimeHelpers.
PrepareMethod`) while the window is made and the scene pre-warmed. What that pass cannot reach is the
framework's own generic code for OUR types, which is compiled the first time a given shape of call
runs: a collection spread (`[.. a, .. b]`) over points compiled the shared array pool for `PointF` on
the frame the snake first came (7 ms), `Rocks.Where(...).ToList()` compiled on a lizard's first climb
(4.5 ms), `OrderBy` over the falling petals on the first frame. The rule that came out of it: no LINQ
and no collection spreads on a per-frame or per-event path of a saver; plain loops into arrays kept
from frame to frame (`Scratch.Gather`, `Scratch.Keep` in `SakuraPondScene.cs`), which also make no
garbage. Found with the JIT's own list of what it compiled and when (`DOTNET_JitDisasmSummary=1`,
`DOTNET_JitStdOutFile=<file>`): the methods at the END of that list are the ones compiled mid-run.
After: a steady 60 from the fourth frame (298 of 300 on-screen frames at one page, 153 before), the
slowest update 1.8 ms against 13.6, the frame at the 2048 draw width 8.8 ms against 9.9.

*Reading a hitch.* The section timer (`PECKWORKS_SECTIONS`) now prints, beside each section's average,
its slowest single frame after the first second and WHICH frame it was, and the scene's Update is
timed in laps of its own (`Sections.Lap()` at the top of Update, marks named "update: koi" and so on),
because a hitch hides in an average and an update spike looks exactly like a render spike from
outside. The snapshot's timing line also says how much the run allocated per second and the slowest
Update alone. An average that is fine and a slowest frame that is not points at an event (a first
visit, a hatch, a collection); the worst-frame column names the section and the frame, and the
frame names the moment in the logs.

*The ripples in parallel (2026-10-20).* The rings were painted one after another on one core, 1.2 ms
of a 9.7 ms frame at the 2048 width. Now every ring is prepared once per frame (radius, band,
strength, the rows it covers) and the rows of the picture are split into bands of 32 painted on
separate cores, each band painting, row by row, the rings that cross it, in the rings' own order: no
two cores ever touch the same pixel, and the picture is pixel for pixel the old one (checked by a
frame diff at two seeds, 0 pixels). 0.84 ms to 0.35; a frame with only a few rows of ring is painted
straight through, because handing work to the thread pool costs more than it saves then.

*The bow waves (2026-10-23).* The owner's note: the topmost fish "look like they aren't even in the
water at all", and the pond must stay calm. So the surface shows only where something disturbs it.
Any swimmer with its back at the surface (a koi or turtle shallower than `Koi.WakeZ`, the snake, a
swimming frog) calls `Ripples.Wake` every frame, and every 0.012u of travel it leaves a WAVELET: a
thin, faint, slow ring of which only the BACK half is drawn (the half behind the heading the swimmer
had when it left it). A point moving faster than its rings spread leaves them lined up in a V behind
it, which is what a bow wave is; a slow swimmer leaves rounder arcs. The wavelets ride the same
prepared-ring machinery as the gulp rings (one more flag per prepared ring, a half-plane test per
pixel), so they cost nothing the section timer can see. They are decoration: when the pond is near
its ring cap, or the wake list (96) is full, a wavelet is skipped so a gulp or a landing petal
always finds a slot. The snake's older rule, a ring off its head every 0.4 s, now fires only while it
is frightened.

*Drawing smaller on purpose.* A saver built from a small photo can say `MaxRenderWidth`: on a
wider screen it draws its sheet at that width and Windows stretches it to fill the screen
(`StretchDIBits`). Drawing every pixel of a 4K screen from a 1500 pixel picture adds work, not
detail.

The normal .NET way to draw text (`Graphics.DrawString`) is far too slow for thousands of
characters, 60 times a second. So `GlyphAtlas` draws each character **once** at startup and keeps
it as a little grid of "ink amounts" (0 to 255). Drawing a character afterward is just copying
that grid in the right shade of green, like using a rubber stamp.

## 6. The rain: nothing actually falls

This is the key idea, and it's in `MatrixRainScene.cs`.

Picture a stadium scoreboard made of character-shaped light bulbs. **The bulbs never move.**
Each column has an invisible cursor (a "drop") walking down it one bulb at a time. When the
cursor steps on a bulb, the bulb flashes to full brightness with a new random character, then
slowly dims.

A bright bulb at the cursor, dimmer ones just above it, dimmer still above those: that's a comet
with a fading tail. Your eye reads "a stream falling", but only the light moved. That is exactly
how the film's rain behaves.

Film details the code reproduces:

- The leading character is near-white and flickers through random characters as it moves.
- Characters left in the tail occasionally change on their own.
- Drops fall at different speeds, and some are dimmer, which reads as depth.
- Movement is in whole-character steps, not smooth sliding.
- The characters are mostly half-width Japanese katakana, **mirrored**, plus digits (not
  mirrored) and a few symbols.

## 7. The glow

Squint at a neon sign at night and the letters bleed light into the air around them. The glow
(`BloomEffect.cs`) fakes that in three steps:

1. **Shrink** a copy of the frame 4x in each direction (16x fewer pixels, so everything after is cheap).
2. **Smear** it with a blur.
3. **Add** the blurry copy's light back on top of the sharp original.

Adding (rather than replacing) is why dark areas near bright characters pick up a green haze
while the characters themselves stay crisp. With the glow turned off, the rain looks flat,
like text in a terminal.

## 8. Sakura: a painted backdrop and falling petals

Sakura works like a theater: a **backdrop** that never moves, and **actors** that do.

**The backdrop** (`SceneryPainter.cs`) is painted once, at startup, with .NET's ordinary 2D
drawing kit: gradients for the sky and lake, curves for Mount Fuji's outline, and thousands of
small circles for the blossoms. It's painted back to front like a landscape painting, so each
layer covers what's behind it: sky, mountain, haze, lake (with the mountain drawn again upside
down and faint, as a reflection), far shore, the two banks of land, the trees standing on them,
and finally the branches.

The banks are the ground truth for the trees. Each bank is a line of points, and it can answer
"how high is the ground at this x?" Every trunk's foot is drawn a little *below* that answer, so
the tree visibly stands on the bank's surface instead of floating above the shoreline. (The foot
is painted over the bank, not hidden by it; it looks planted because it ends past the rim, the
same trick a hand drawing uses.) The pine is one solid cone shape with clumps of needles painted
over it for texture; the blossoming trees are a forked trunk with a cloud of blossoms on top.
Tree and branch sizes come from the screen height on a normal wide screen, but from the width on
a tall or square screen, so the grove does not swallow a portrait monitor.

The branches are *grown*, not drawn by hand. A small rule walks forward in steps, wobbling a
little and sagging under its own weight, and now and then starts a thinner copy of itself heading
off at an angle. A rule that uses itself like this is called **recursion**, and it's how most
computer-drawn plants are made. Since the random numbers differ each run, so do the branches.

Painting the backdrop takes under a second. After that, each frame just **copies** the finished
picture (fast) and draws the petals on top.

**The actors** are a few hundred petals, run by `Core/Sakura/PetalField.cs` (shared with Sakura
Dusk, which only changes the light on them). Each one:

- **falls** slowly, and is pushed sideways by a breeze that rises and fades over time;
- **sways** side to side, like a leaf rocking as it drops;
- **spins** in the plane of the screen;
- **tumbles**: it flips over and over. That's faked in 2D by squashing the petal's width by
  `cos(flip angle)`, which swings smoothly from 1 to 0 and back, so the petal looks like it's
  turning face-on, then edge-on, then face-on again;
- has a **depth**: near petals are bigger, faster, and more solid, far ones small, slow, and
  slightly see-through. That difference is called **parallax** (close things cross your view
  faster than far things, like fence posts versus hills from a car window), and it's what makes
  the scene feel 3D.

Petals are drawn pixel by pixel. For each pixel near a petal, the code asks "where is this pixel
in the *petal's own* coordinates?" (turning the pixel backward by the petal's angle). In those
coordinates the petal is always upright, so the question "is this inside the petal shape?" stays
simple no matter how the petal is turned.

**Sakura Dusk** (`DuskPainter.cs`) is the same theater with a different backdrop: a sunset sky
(a gradient from violet to warm yellow, with a "path gradient" glow around the sun that is
strongest at its center and fades to nothing at its edge), three ranges of hills made from sine
waves added together, a pagoda silhouette, a pond that mirrors the nearest hills and the pagoda
(painted again upside down, clipped to the near water, then washed back toward the water color)
and, more faintly, the bridge, an arched bridge whose deck is a curve bowed upward by a steering
point above its middle, and a stone lantern with a glowing window. The sun is placed on the far
ridge under it, so it always sets behind the hills whatever shape they took. The shared pieces (banks, trunks, blossoms, branches) come
from `Core/Sakura/Brushwork.cs`, so both scenes have the same hand. The petals get a warm tint,
and the glints are golden and live only in the sun's reflection, which the painter hands over
as a rectangle; each glint also checks the pixels under it and lights only bright water, so none
ever lands on the bridge that crosses the reflection.

**Halloween and Christmas** are the same theater again, with two new ideas.

*The falling things changed shape, not behavior.* A leaf and a snowflake fall, sway and drift
just as a petal does, so `PetalField` simply has a `Shape` switch (petal, leaf, or snow) and a
pair of `Colors` to mix between. Halloween asks for leaves from gold to deep red; Christmas asks
for soft white dots.

*Sprites.* A bat with scalloped wings, or a sleigh with four reindeer, is too detailed to work
out pixel by pixel sixty times a second. So `Sprite.cs` does what `GlyphAtlas` does for the
Matrix characters: paint the thing **once** at startup onto a small see-through sheet, then stamp
that sheet onto the frame wherever it is needed. To animate, paint a few sheets in different
poses and stamp a different one each frame, like the pages of a flip-book. The bats have seven
wing positions in three sizes; the sleigh has four gallop poses.

A soft **glow** is also a sprite: a round patch of color, strong in the middle and fading to
nothing at the edge. Stamping it at changing strength is what makes a jack-o'-lantern flicker, a
Christmas bulb twinkle, and Rudolph's nose shine. The bulbs themselves are painted into the
backdrop as dull dots (strung along each pine by asking the pine how wide it is at each height);
the animation only adds the light.

*Santa flies behind the overhanging branches* with one trick. Right after painting the sky, the Christmas painter
keeps a copy of the picture. When the whole backdrop is finished, it compares the two: any pixel
that did not change is still sky. That gives a stencil with one yes/no per pixel, and the sleigh
is only ever stamped on the "yes" pixels. Nothing has to know where the branches are. (He flies
well above the pines, so in practice it is only the corner branches that cover him.)

**Happenings** (in every painted saver) are small events that come and go: a shooting star, a
ghost rising from a grave, a spider spinning a web, a snowman building itself. Think of a cuckoo clock: each happening is one bird behind
a little door, and a **director** (`Core/Happenings.cs`) is the clockwork that opens one door
every few seconds. The director shuffles the happenings like a deck of cards and deals from it,
so you see them all before any repeats. Christmas, Sakura and Sakura Dusk each have their own deck of twenty, none repeated.

Each happening is one small class with one rule: it is told `t`, the number of seconds since it
began, and works out the whole picture from that number alone. It keeps no notes from one frame
to the next. That is what makes them easy to test: `scripts\happening.ps1` can ask for "the
firework at 2.5 seconds" and get exactly that picture, without waiting for it to come up.

To know where things are (which pixels are the moon, where each tombstone stands), a happening
asks the painter, which hands over those facts along with the finished backdrop. The ones in the
sky pass behind the hills and branches with the same stencil trick Santa uses. The recipe for
adding one, or for giving another screensaver its own set, is
[ADDING_HAPPENINGS.md](ADDING_HAPPENINGS.md).

**The boat on the Sakura lakes** (`Core/Sakura/Boat.cs`) is those same two ideas again. It is a
sprite, painted once, plus a second sprite of the same boat painted upside down and stamped
faintly underneath as its reflection. Each painter keeps a copy of the picture at the moment the
water is finished, and compares it with the final backdrop to get an "open water" stencil, so the
boat slips behind the banks, the trees and the bridge. In Sakura Dusk the boat is smaller and
sails far out near the opposite shore: any closer and it lined up with the bridge's railing and
looked as if it were riding on the bridge.

**Cabin by Stream** is the same theater with a different ambition: to look like a photograph
rather than an illustration. Nothing in the engine changed for it; the difference is all in how
the backdrop is painted (`MeadowPainter.cs`), and three ideas do most of the work.

*Texture, pixel by pixel.* A flat green gradient looks painted at once. So after the meadow's base
colours go down, the picture is pulled out as a plain array of pixels and every meadow pixel is
nudged lighter or darker by noise: small clumps near the horizon, bigger ones near the bottom
(near grass looks coarser than far grass), plus broad soft patches, plus a fine per-pixel
speckle. Then it goes back into the picture and painting carries on. The water near each bank is
darkened the same way (it mirrors the grass above it; only the middle of the channel sees the
sky). At the very end, a vignette darkens the corners and a tiny grain is added to every pixel,
which breaks up the smooth bands a computer gradient has.

*One light.* The afterglow low on the left decides everything: the lit front of the cottage and
its shadowed side, the warm rim on the upper-left of every tree crown and bush, the long soft
shadows that all fall to the right, and the violet haze that far things fade toward.

*Depth cues.* Perspective is faked by scale: flowers, grass blades, stones, fish and fireflies
are all bigger near the bottom of the picture than near the horizon, and the stream widens as it
comes toward you. Mist lies along the foot of the far forest, and the stream's far end is lost in
it. Dark bushes in the middle distance and blades of grass along the bottom edge give the eye
steps to judge the distance by. On a tall screen the meadow's depth is capped and the sky takes
the room instead, so the cottage is never a speck in an endless field.

The moving parts are all actors in the usual sense. The **smoke** is a crowd of puffs, each born at
the chimney with an upward speed that eases off as it cools, a sideways lean from the breeze, a
size that grows and an opacity that fades. The **trout** face up the screen and hold against the
current: they drift slowly down, then dart up a body length, wagging harder as they go, and are
stamped only on open water so the stones and banks are always in front of them (the same stencil
trick as the boat). Each **squirrel** follows a short plan of legs (sit, run, climb up, sit, climb
down, run back) at speeds in u per second, and is drawn from a flip-book of running poses, the
same poses turned a quarter turn for climbing (head up going up, head down coming down, as real
squirrels do), and a mirrored stamp for running left. The **fireflies** are the Sakura Dusk
happening's idea made permanent: a home spot, two sine waves per direction for the wander, a blink
cycle of its own, and a reflection in the stream where one crosses it.

**Cotswold Brook** (now retired; see "Why it was retired" below) took the next step: no painting at all. However much texture and light the
meadow got, code strokes stayed an illustration; a camera does stone and a thousand leaves
better. So the backdrop is a photograph (packed inside the `.scr`, credited in the README), and
only what moves is animated. What the saver's own file holds is knowledge of THIS photo, measured
by eye on a grid laid over it: the outline of the water, the bridge piers standing in it, the
chimney, a box round each window.

*Cover fitting.* The photo is enlarged just enough to fill the screen both ways and the overflow
trimmed (more from the right than the left, so the cottages stay), the way a phone fits a
wallpaper. Every place is written as a fraction of the photo, and `ToScreen` turns it into a
pixel wherever the trim fell. Sizes of things tied to the scenery (ripples, smoke, windows) are
fractions of the photo's height on screen; free-flying things (fireflies) are fractions of the
screen, so they stay firefly-sized whatever the photo's shape.

*Water.* The ripples do not paint anything: each water pixel shows the photo's pixel from a
fraction of a pixel to the side, by an amount that is a wave travelling toward you. So the
reflections wobble with the photo's own colours. Trout are dark slim shapes stamped only on open
water, and only over BRIGHT reflections: over the dark mirror of the reeds a real trout is
invisible from the bank, and so is a drawn one.

*Day for night.* Film crews shoot "night" in daylight and darken it afterwards; this does the same.
A second copy of the photo is made at startup: darker, most of the colour gone, tinted blue (the
light at dusk comes from the sky, not the sun), with the sky itself repainted deep blue overhead
and rose at the horizon, and green things darkened further so trees read as silhouettes. The
screen shows a mix of the two copies. The mix moves one step of 256 at a time, each step spread
over four frames: redoing a whole 4K picture at once made a frame late, and letting one sweep
jump many steps showed as a wave running down the screen. Windows light up one by one: inside
each window's box, anything that is not bright and strongly coloured (the stone frame) counts as
glass and is blended toward lamplight, keeping its curtains and glazing bars.

*Fireflies* are little insects in the picture's space rather than lights pasted on the glass:
each lives in a place with a depth (the reeds in front, the water, the far bank), flies a curving
path that turns by two slow waves of its own, shows as a faint dot, and flashes every few
seconds. Near ones are bigger and cross faster.

*Depth map.* A photo is flat: the code cannot tell the near reeds from the far wall by their
colour. So a second picture rides along with it, `brook_depth.png`, the same shape, where each
pixel says how NEAR that bit of the scene is (white near, black far). It is made once, on the
developer's machine, by an AI model (Depth Anything V2 Small) that judges distance from one
picture the way you can with one eye shut: by size, overlap, haze and where things meet the
ground. `scripts/depthmap.py` runs it; the saver only reads the grey picture.

With it, a moving thing can sit IN the photo. Each firefly is given a distance picked from the
scenery in its own patch (most as near as the nearer part of it, a few deeper in), and
`Sprite.Draw` skips any pixel where the scenery is nearer than the firefly: hold a cut-out at
arm's length in a garden and the flowers closer to your eye cover it, the hedge behind does not.
A box on the screen is not a slab of one distance (the foot of a reed bed is far nearer than its
top), which is why the distance comes from the map, not the box.

*Mist* uses the map twice. A still sheet lies on the water, thickest where the map says far (by
the bridge) and fading to nothing at the reed tips, and rising softly from nothing at the foot of
the cottages so it has no hard top. It never moves, so it is painted once into both the golden
and the dusk copy of the photo (more into the dusk one: mist gathers as the air cools) and costs
nothing per frame; blended in every frame at 4K it cost several milliseconds. Soft banks drift
through it to keep it alive, shown only where the map says the scenery is beyond the reeds.
Banks alone looked like streaks of light, not mist: mist on water is a sheet, stirred.

*Why it was retired.* Its trout were one flat, side-on picture, able to face only left or right,
flipped like a playing card to turn round and slid sideways to "dart", swimming under a pond so
bright with reflections that no real fish could be seen through it. On a painted scene that
might pass; on a photograph it is the one thing the eye goes to. Two lessons hold for any photo
saver: a creature is either right in every way (really visible there, drawn in the photo's own
perspective, turning by bending, moving the way that animal moves) or left out; and anything
that moves is judged by watching it move, never from still frames. `src/CotswoldBrook/RETIRED.md`
has the full list.

**Sakura Pond** goes back to painting, and changes the camera: we look DOWN at a koi pond,
from above and a little in front. That changes how everything is drawn.

*Things lying on the water* (koi, lily pads, ripples, floating petals) are seen at a slant, so
each is squashed a little top to bottom (`Ripples.Squash`): a circle on the water looks like a
slightly flattened oval, and a koi swimming up the screen looks a little shorter than one
swimming across it.

*Height becomes a position.* A thing high above a spot on the water shows a little further up
the screen than the spot, and its shadow (the sun is up and to the left) falls down and to the
right of the spot, further away the higher it is (`Light.cs`). So a falling petal slides down
the screen toward the spot where it will land, while its faint shadow slides in from the other
side to meet it; the moment they meet, it lands. Dragonflies and swallows carry their shadows on
the water below them the same way, and that gap between animal and shadow is what tells your eye
how high each one flies.

*A body that bends.* The koi and the snake are drawn along a SPINE: a line of points from nose to
tail, each with how wide the body is there (`Body.cs`). The head swims a path; the body is laid
along the path the head has already swum, like train carriages following the track, so every
turn bends the whole body through the curve. A swimming wave runs down the koi's body (the head
nearly still, the tail swinging widest: a carp swims from the back half, so the wave's width
grows as the cube of the distance along the body and only four fifths of a wavelength fits on
it; a wave that grew as the square with nearly a whole wavelength threw the body into an S with a
second bend at the tail, "a super wiggle like a worm"). The tail BEATS AT THE SPEED OF THE FISH
(round thirty-four): beats a second is lengths a second divided by the stride, six tenths of a
length per beat as a carp swims, with a slow flutter as a floor on a glide, and the wave's width
grows with the effort. Three fixed rates (2.3, 1.5 and 0.7 a second by state) had given a stride
of 0.35 lengths and a wave running four times the swim speed (a real carp covers half to four
fifths of what its wave covers), which read as "the movement doesn't match the speed of the
fish". The head's rate of turn is eased
over an eighth of a second, because the body is laid along the path the head took, and a
steering sum that flipped sign from frame to frame was laid into that path as kinks. The
remembered path and the walk back along it are the shared `Core/Minds/Trail.cs`. The snake's head weaves side to side and the body
follows it point for point, which is how a snake really swims. For every pixel near the spine
we work out two numbers, how far along the body (s) and how far out to the side (v), and the
animal says what colour it is there; the markings are stuck to (s, v), so they ride along the
body as it bends. Each short piece of spine paints only its own strip of pixels, and
neighbouring strips meet on the line halving the angle at their joint, like the corner of a
picture frame, so no pixel is painted twice.

*Grass in the wind.* The lawn is painted twice: the ground once into the backdrop, and the
thousands of short blades onto a clear sheet of their own, the "carpet" (`Shore.cs`). Every frame
the carpet is laid back over the ground SHIFTED a few pixels downwind by how hard the wind leans
the grass at that spot, and brightened where it leans hardest, because bent blades show their pale
tips (the shared `Lawn` in `Core/Sakura/Plants.cs`). The wind (`Wind`, the same file) is ONE object
the scene updates once a frame, and everything that sways asks it: the carpet, the tufts, the
cattails and the irises, each at its own spot, so a gust reaches them as one travelling wave
(a plant further downwind leans a moment later). Nothing keeps a clock or a random offset of its
own. The falling petals read the same wind's SWELL (how strong the breeze is right now), so they
gust when the grass does, though they do not ride the wave across the screen. The wind at any spot
is one number, the lean: a slow swell that comes and goes over half a minute, times a broad soft
hump travelling downwind (the gust front, with no sharp crest), times a slowly drifting pattern of
patches across and along the wind, so each gust is a blob of its own shape rather than a bar the
width of the field, plus a faint quick ripple. (The first wind was a sharpened bar crossing at
0.4u a second; on screen it read as a ruler sliding over the lawn, and the tufts flipping pages
hid it anyway. A probe that proves the wave travels: the brightness of two lawn patches 400 px
apart along the wind peaks 56 frames apart at 1440p, against 54 predicted from the wave's speed.)
The sheen is a real lift in brightness (up to half again at full lean, the blue channel less, so
it goes pale yellow-green, not grey). The lawn shows only where no branch is in front of it, and the
branches' dappled shadow darkens the moving grass as it darkens the ground.

The lawn's VARIETY comes from two places. The painter (`Shore.cs`) reads smooth random blotches
(`Mottle`, in Plants.cs: a random value pinned to each corner of a grid and blended between, three
grid sizes added so the shapes are ragged) to lay the ground: lush dark green where damp, warmer
yellow-green where sunny, bare earth in the most worn spots, moss patches with a velvety speckle;
the same blotch thins the carpet's blades where the ground is worn, leaves them off the moss, and
on a second blotch's patches paints a clover leaf (three round leaflets with a pale chevron)
instead of a blade, so clover reads as a rounder, denser texture. The standing tufts (`Lawn`) are
dealt from a deck of 36 flip-book designs: plain grass in three heights and three colour families
(the lawn's green, a yellower sun-baked one, a bluer damp one), dry tawny tufts, taller grass gone
to seed (thin limber stems with a pale oat-shaped head painted along the stem's top), and three
wildflower kinds (daisies, buttercups, dandelions with the odd seed clock), each a few blades
with two to four flower stalks rising out of them. Where each tuft goes follows the painted
carpet: the planter measures the carpet's thickness cell by cell and gives a spot a chance in
proportion, so the tufts thin out over the worn and mossy ground the painter made (the two never
have to agree by any other means). Which KIND a tuft is follows blotches of its own: flowers grow
in drifts of one kind (a coarse grid picks the kind per drift), seed heads in their own patches
and along the lawn's edges, dry tufts as a sprinkle. The lawn rolls all of this on dice dealt
once from the scene's, so however much it rolls, nothing the scene rolls after it moves.

Taller tufts stand in
the carpet, each a flip-book (`Flipbook`) of 48 pages from upright to "flat downwind" (24 read as
fluid at a glance, but a tall tuft's tip moved nearly two pixels a page at 1440p, a visible step
every few frames in a gust; at 48 it is under a pixel: the owner's rule is that wind must be
smooth); the pages of all 36 designs are painted on every core at once, after every design's
blades have been rolled in order, so the dice land the same whatever the core count. The blades of a page are
worked out in three dimensions (the bend angle grows from root to tip, each short step going a
little less up and a little more along the ground) and projected like everything else in an
overhead scene (`Overhead`: squash, lift, and where a shadow falls): height lifts a point up the
screen, and throws its shadow down and to the right. A flower is stamped at its stalk's tip, seen
from above (a disc of petals round an eye) with a small round shadow on the ground under it, so a
daisy nods with the wind exactly as the blade under it would. Tuft shadows are faint (26 of 255):
hundreds of dark parallel shadows at 48 wove a lattice over the whole lawn. A tuft stamps the page
nearest its lean. The cattails and the iris clumps are the same machinery (`Clumps`): a few plants
that each stand in one place, one flip-book each, stiffer than grass; an iris leaf (a sword leaf,
`Taper` 2) is painted as a lit half and a shaded half either side of a pale midrib, which is what
makes a flat strip read as a leaf with a fold. The lean itself comes from a coarse grid worked
out once a frame (a sine per pixel would cost more than the drawing), and the tufts are drawn in
horizontal bands on several cores, even bands then odd, since a tuft can only ever touch tufts in
its own band or the next. All of this lives in the engine for the next scene seen from above with
a meadow or a reed bed: it paints its carpet and its ground once, then makes a `Wind`, a `Lawn`
and a `Clumps`, and nothing of its own that moves. Two rules travel with them: the lawn is drawn
first each frame (half its rows are kept from the frame before), and the projection is overhead
(a side-on scene needs a different one).

*Boughs that sway.* The cherry branches used to be painted into the backdrop. Now the painter
paints them a second time, with the same seed, onto a clear sheet of their own (`Pond.Branches`),
and the backdrop the scene restores each frame is the picture WITHOUT them. Every frame the
scene lays that sheet over everything on and under the water (`Boughs`, Core/Sakura/Plants.cs)
with each pixel taken from a little upwind of itself: the shift grows with the pixel's distance
from its tree's trunk (a bough's tip swings, its base barely moves; the two crowns' centres are
the trunks, `Pond.TreeRoots`), and its amount comes from two damped springs per tree driven by
the wind's lean at the trunk, one swinging in 2.6 s for the thick base and one in 1.3 s for the
tips, so a gust bends the boughs over and they swing back past upright and settle, as a branch
does. The picture is sampled between pixels (four neighbours blended), so the motion is smooth
at any fraction of a pixel; the shifting is done on alternate rows into a kept sheet and the kept
sheet laid over the picture every frame; each row is walked only along the stretches (spans of
4-pixel tiles) a live pixel can reach by its own sway; in a lull, when no spring has moved a
sixteenth of a pixel since the sheet was last shifted, the shifting is skipped and the sheet is
laid as it is; and the parts that hardly move at all (under three tenths of the full sway: the
crowns and the thick wood near the trunks, a pixel or two at a full lean) are painted into the
backdrop once when the scene is built and never laid live, with the whole sheet kept as the source
so a live pixel at the zone's edge samples a neighbour that is there (cleared, it showed as a dark
seam along the edge). The first version resampled every row's whole painted stretch, which spans
both trees: ten milliseconds at 1440p; now about 390 thousand live pixels in 680 thousand span
pixels (`PECKWORKS_STARTLOG` prints both), about 3 ms at 1440p while the boughs move, which is
the floor of this approach (the blossom is out over the water, not near the trunks, so widening
the still zone from a fifth to three tenths freed only one pixel in nine). Two things it does not do, said plainly: the branches' shadows on the
water are painted once and do not move with them (a soft dappled shadow shifting a few pixels is
not missed), and anything that passes BEHIND a branch (a koi, a dragonfly, a low petal) is cut at
the branch's resting footprint (`Pond.Open`), so for the few pixels a bough has swung away from,
the water shows between it and the thing behind. A side effect that turned out to be a fix: a fish
under the blossom's edge is now hazed toward the WATER behind it (`Pond.Pixels` holds water
there), where before the painter mixed it toward the pink. Only the petals still in the air above
the branches are drawn after them.

*Petals in the eddies.* The grass leans along one prevailing wind, but a petal in the air is
light enough to follow the swirls in it. `Petals.Air` reads a slow pattern of eddies over the pond
(the same `Mottle` blotches the lawn is painted with, drifting downwind at a fiftieth of the screen
a second): the eddy at the petal's spot turns its course left or right of the wind, by up to a
right angle or so, and the wind's own lean there (strong in a gust, faint in a lull) sets how fast
it is carried, higher petals catching more. A petal falling through the pattern curves one way,
then the other, two petals a little apart take different turns, and a petal spins faster in a
gust. A petal on the water feels a third of the same push, on top of its slow circling drift. The
instrument that showed it: the brightest red-minus-green over 240 frames of a water region, which
draws every falling petal's path as a streak; the streaks curve and cross where before they were
parallel lines.

*Rocks that look like rocks.* A rock is a lumpy ring of 9 to 13 points (two slow waves of radius
plus jitter), a touch wider than tall because we look down on a rounded lump at a slant. It is
painted in the order light builds a stone: a soft shadow on the ground to its lower right, the
body with a gentle rounding toward the sun, then a lit
FACE (the rim points on the sunny side joined by a ragged inner edge, filled three times at three
sizes so its edge is a soft crease) and a dark face on the far side, short crack lines from the
rim part of the way in, a fine speckle of grain, moss and a spot of lichen on top, a light edge
where the rim faces the sun and a dark edge where it faces away. Stones are not one grey: warm,
cool, greenish, light and dark. Then the WATER IS LAID BACK OVER the stones (`PondPainter.WrapWater`):
a rock in a pond is not pasted on top of the water, its flanks slope into it. For every pixel of a
rock on the water side of the bank, a THIN band just inside the rock's own rim (half a percent of
u) goes dark and a little blue, the wet flank, and the water just outside the rim goes dark too,
the surface meeting the flank in shadow, so the water visibly goes round the stone; both fade out
where the rim comes to the sand, so the waterline ends at the ground. A stone ON THE BANK (any
ground under its disc) gets nothing else: until the twenty-ninth round the part of such a stone
that lay past the bank's curve was tinted toward the water by the depth there and the part on the
sand side was dimmed in a strip along the curve, and a 6x crop showed the bank's line running
straight across the stone's face with a paler half beyond it, which the owner read as "the water
line on top of the rock". A stone the bank runs under is a stone standing on the ground with water
against its far flank, and its wet rim is the whole of its waterline. Only a stone standing out in
the shallows (no ground under it) still sinks: it takes on up to half the water's colour, darkened,
once the water under it is deep enough, its top still breaking the surface. (The first wrap mixed a
band a tenth of a stone wide toward the water at 85 percent: every stone on the shore had its water
side turned to a teal ghost with the bank's line cutting straight across it.)

*The water's edge, alive (`Shoreline.cs`).* Where the pond meets the sand nothing is painted as a
line. Think of a ruler laid across the water's edge at every point along the bank: zero at the
painted edge, minus up the sand, plus out into the water. Each frame the water sits at a different
mark on that ruler, a swell of three slow rhythms (4.5, 2.9 and 7 seconds) travelling along the
bank, 0.013u each way at most; a pixel beyond the mark is under water, one before it is bare. The
swell is not a plain sine: watch a pond's edge and the water runs UP the sand quickly and drains
back slowly, so each rhythm is a sine with its crest pushed forward (a rounded sawtooth), and the
pale thread at the edge is brightest while the edge is advancing (foam rides the run-up, not the
drain). A ring on the water that reaches the bank washes up it: `Ripples.Surge` says how hard the
rings are pushing at each of 256 points along the bank, and that is added to the swell there (a
koi's gulp by the sand sends a visible wash up the beach; a ring far out never arrives). Every pixel within reach of the edge was given two colours when the shore was built: a
sand pixel's wet colour is the shallowest water painted over it by the painter's own water recipe,
a water pixel's bare colour is wet sand. Each frame the shore writes only the pixels whose state
differs from the backdrop (the backdrop copy already put the rest down), mixes the five pixels
either side of the edge softly, lays a pale thread where the water's skin meets the ground, and
remembers WETNESS per pixel: sand the water has just left stays dark and dries back over about six
seconds. That memory is what makes a lap readable: the water's own colour over sand is nearly the
water's colour, so the edge alone barely shows; the dark band it leaves is what the eye follows. A
fixed wobble along the bank is baked into the picture at rest, so the painter's smooth curve runs
a little further up the sand here and less there. The water side of every stone's rim is in the
band too, measured in from the stone's own rim: when the swell runs up the sand it runs up the
stone's foot as well (the stone darkens and goes bluer under the skin of water, and dries from
that), so the waterline on a stone moves with the water instead of sitting painted on it. Lily
pads are left out: they float. The same object answers the animals: WetNow
(is this point under water this frame), EverWet (can the water ever reach it) and EverDry (can it
ever be bare; a tadpole clutch never settles there). A lizard or a mouse drinks as close to the
edge as the rest edge allows, and when a swell reaches its nose it scuttles back about two thirds
of a body and thinks again. Cost: one compare per band pixel and a write only where the state
changed; with the stones' feet in it and the wash's allowance the band is about 85,000 pixels at
1440p (shore section 1.0 ms there, 0.2 before the feet and the wash; about 0.6 ms at the 1920 draw
width). The lever left, if it is ever needed: the band's eight arrays streamed as one, or four
chunks on four cores.

*Cattails.* One clump stands in the shallows a little way out from the bank: stiff stems with
velvety brown heads and a pale spike, and long leaves between them that splay and bend over. It is
the same flip-book as a grass tuft (24 pages of lean, each blade bent in three dimensions and
projected with height lifting it up the screen and its shadow thrown onto the water), stamped once
a frame at the lean the wind gives that spot, and only where no branch is in front.

*A goldfish is a koi with a different nature.* The goldfish (`Koi.Kind.Goldfish`, in `Koi.cs`) swims on exactly the same body, steering,
mind and depth rules as a koi; only what it IS changes. Its body is small (0.042 to 0.052u against a koi's 0.095 to 0.155u), deep
(half width about 0.2 of the length: a wakin is nearly an egg) with a round blunt head and a very slim tail stalk, and the tail fin
fans out wide and long (reaching 1.55 body lengths from the nose, a notch down its middle so it reads as a forked double tail)
and thins toward its end. The sideways wave in the body stops growing past the fin's first quarter, or the long fin would swing like
a paddle. Its nature is a handful of numbers read in the same places a koi's are: it beats 1.3 times as fast, cruises a little
slower in body lengths, keeps to the upper water (a small fish deep down is lost), gulps twice as often, and flinches from further
off. It follows other goldfish twice as readily and never starts a chase at a koi; an idle wander mostly heads for the neighbourhood of
another goldfish, which is all the "shoal" there is. A goldfish only goes to look at what is near, because it crosses the pond
at about a third of a koi's pace, and a faraway petal used to leave it "investigating" four fifths of the time.
Its play (2026-10-27, "make the little goldfish more fun to watch") is the DART (`Doing.Dart`): two to four
quick bursts near the top of the water, each a sharp swerve the other way (a point three lengths ahead and
off to the new side, pulled at harder than a chase), then it wanders on; a face of the dice about the size
of a look's, the energetic more, and its chases after another goldfish are twice as ready as a koi's. It
never idles shallower than 0.08 of the pond now, and takes a tenth more of the water's colour at every
depth than a koi would (`LaySpine`): a small, bright, crisp fish right under the surface read as sitting
ON the water ("the little goldfish sometimes look like they're above the surface of the water"); the haze
and the depth are what say "under". The idle drift of every fish reaches deeper since the same day ("make
more fish willing to swim deeper": a koi's to 0.55 and 0.8 of the pond where it reached 0.4 and 0.6, a
goldfish's to 0.38 where it reached 0.25).

*Rigid animals turn as a whole.* A dragonfly or a swallow does not bend, so it is a picture
painted once (several, for the wingbeat) and stamped at any angle every frame (`TurningStamp.cs`),
reading the picture smoothly between its pixels so a slow turn never steps. Never flipped. The
turtle is rigid too, but it lives in the water with the koi, so it uses the same spine drawing
as the koi with a STRAIGHT spine that turns as one piece: that way it gets the same haze with
depth, the same branch shadows and the same squash for free. Its legs are JOINTED (an upper
leg from the hip, a lower leg from the elbow or knee, a webbed foot) and they row the way the
film of painted turtles and sliders says a pond turtle rows (Rivera and Blob, Journal of
Experimental Biology): the diagonal pairs together (front left with back right, then the other
pair; only a sea turtle flaps both front flippers at once, and `Turtle.PairOffset` is that one
difference), the sweep back the LONG part of the cycle (56 percent, `ThrustShare`), the leg
straight and the foot spread on the push, and the leg folded at the elbow and the foot closed
to a blade on the way forward (`Fold`). A front leg sweeps a smaller arc than a back leg, and a
leg reaches about a fifth of the shell's length past its rim and no more (a third until the
owner said "turtles legs are too long", twice, on 2026-10-27). A
cruising turtle rows in bouts of two to six seconds and then holds its legs trailing and
coasts for a second or two (`_bout`, `_gliding`; the bouts' lengths come from a hash of its
dials, not the scene's dice); a dive, an approach or a chase keeps rowing. (Until 2026-10-26
night the legs were straight rods pivoting at the shell with the push a 35 percent snap and
the return a slow drift, always rowing: "janky and unnatural". The owner's guess was that
both feet move in unison; the papers say that is the sea turtle, and the pond slider's
alternate.) The
head and neck are the one part that bends: they turn about the shell's front by a neck angle
that leads a turn (about half the turn's rate) and looks about while it basks, so the shell no
longer swings for a head turn. Its rate of turn is eased over a sixth of a second (a shell has
mass): the old bank rule read off the room grid switched on and off from frame to frame and the
turn rate flipped sign sixty times a second, which the owner saw as "not fluid". It steers with
the same feelers as the koi (`Core/Minds` Feelers, its own `RoomAround`: the bank, the bed as a
gap felt before it is touched, the surface and every body in the water), so it goes over, under
or round a fish instead of ploughing through it, keeps a third of a shell length of water under
it on a dive, and its rise and sink obey the koi's rule (`Light.HoldDepth`, shared by
both): a change of depth moves the picture up or down the screen, so it is held to what the
animal's own forward motion can hide. Two turtles that meet are pushed apart half each
(`Turtle.Nudge`, in the school's touch loop); a koi in a turtle is still the one moved.

*The turtle has a mind and two places to sit.* Like the koi it runs on the shared `Core/Minds`
parts: two dials dealt at birth (`_sunny`: how often it hauls out and how long it sits;
`_nerve`: how soon it slips back in when the snake comes near), a `Doing` (Cruise, Float, Dive,
Approach, Climb, Bask, Slip) and weighted dice in `Decide()`. A dive goes right down (0.6 to
1.0 of the pond, the bed's clamp and its own margin deciding where it stops) for 14 to 30 s,
and is nearly twice as likely from a cruise as it was (2026-10-26 night, "make animals swim
down to the bottom": one dive in five minutes on one seed before, four on one seed after).
`PECKWORKS_TURTLELOG` names a file for a line per turtle per frame (where, how deep, heading,
turn rate, speed, stroke phase, doing), the instrument the gait was measured with. `PECKWORKS_SNATCHLOG` names one for every predator's look, catch, miss and
abandon (`Snatch.Log`), as the spliced instruments used to. A haul-out is PLANNED first (a
point on open water beside the perch, a second one further out on the same line so it arrives
pointing the right way, where the shell's middle will rest) and refused if the straight swim in
is not clear of the bank or another turtle holds the perch. The island cap is approached from
the side the turtle is on; the log from either side, square on, and the turtle then turns on the
spot to lie along it. The climb and the slip are a smooth curve for the nose with the heading
along the curve (so it is always nose first). How high a part of the body is above the water is
read from WHERE it is (`Height()`: 0 over water, 1 well inside the perch), so the nose rises
first and a slip is the same thing backward. Up is a lift up the screen plus a small shadow
down and right on the perch; while any of it is up it is not "in the water" (no haze, depth 0,
no cross shadows) and is painted against the whole view instead of the water only. Its turning
circle is kept no tighter than 0.6 body lengths (0.85 while lining up on a perch), or the middle
of the shell drifts sideways and it reads as going tail first. Its look is a red-eared slider's seen from above (`Turtle.Skin`): an olive-green shell with pale yellow-green seams, an olive head and neck with thin yellow lines and a small red patch behind each eye, and legs that end in webbed, clawed feet (`Turtle.Leg`: an upper leg, a lower leg, a palm and four toes fanned from it, each tipped with a claw; the fan opens on the push), because a pond slider has feet and only a sea turtle has flippers. The slip off a perch tests its landing and its way by `Pond.Land`, not by the water mask: the water mask leaves the lily pads out, and a log beached with pads along both sides once held a turtle for a hundred seconds with every way off refused (a turtle slides in under a pad as a koi swims under one).

*The ducks paddle, dabble and herd.* A duck (`Ducks.cs`: `Ducks` is the visitor and director, `Duck` a
grown duck, `Duckling` a fluff ball) is rigid, so it has a STRAIGHT spine like the turtle's, laid out
from the middle of its body (so turning on the spot swings the bill and tail round the middle) with a
neck whose three points swing about the body's front: the head leads a turn and looks about up to 60
degrees without the body following, and turns right back over the shoulder to preen. The body is
painted by `Body` from colours baked once per species and sex (the folded wings are two darker
panels with a pale line between, the rump and tail dark); the head, bill, eyes and the species'
marks (the mandarin drake's sails, the teal's green eye patch) are ovals on top, at the head's own
height. Height is the pond's usual lift up the screen with a shadow that hugs the bird (a shadow
thrown far from its caster reads as lift): back 0.01u, head 0.03u, a dabble lifts the tail end and
sinks the head, a flap lifts the whole bird and stamps two pictures of the open wings
(`TurningStamp`, made once at start-up). Its mind is the shared kit (`Core/Minds`): two dials per
visit (calm, fussy), a `Doing` (Cruise, Look, Dabble, Preen, Flap, Wait, Herd), weighted dice; the
steering is the snake's (feelers from just past the bill over the pads, rocks, log, reeds and the
bank, the smallest turn that clears, a refused step forces the turn) with an eased turn rate. The
ducklings do not steer by themselves: each has a place on the mother's trail (`Trail`) a set way
back, the line closing up when she stops, and it feels the way as she does. Where it POINTS and how
hard it CLOSES are kept apart, as a car follows a road: it points at a spot a body length ahead of
its place along her trail (so in place it simply faces the way the line goes, and off to one side
it eases back onto the line at a shallow angle), and its speed is her pace plus a gentle closing on
how far behind its place it is, capped at a third over her cruise, with a turn no tighter than a
body length across and never over 3.5 radians a second. The first version aimed each duckling at
its place itself at a speed set by the distance to it: every duckling raced at its slot at nearly
twice her speed, overshot, swung round at seven radians a second and came at it again, S-curves
and full circles behind a mother paddling straight (six to thirteen circles per duckling per visit
on the log, two reversals a second; the owner's words were "chaotic" and "looping ducks"). When the
water between a duckling and its place is not clear (she went round a pad), its place is a point
of her trail just ahead of where it is, in the same shape. A slide along an edge eases the heading
toward the slid way rather than copying it (a two radian snap in one frame, then a circle back),
the feelers search the whole way round, and a duckling wanting to go and getting nowhere for a
second (a pocket between two pads and the cattails, where it paddled on the spot) swims the clearest
of sixteen ways out for two seconds, as a grown duck does. The paddle's sway of the body is a degree each
way about every 0.7 s; at four degrees and six to ten beats a second (round twenty-five) the whole rigid picture
flicked about its middle nineteen times a second while the steering heading, which every metric read, sat
calm: that was the "shaking". The cute things are the
director's: a weighted dice picks one (never two at once, the last one much less likely), sets the
ducklings' doings and runs it a phase at a time. A straggler (more than 0.15u from her and more
than 0.07u behind its place) makes her stop and look back until it is in place. A duck is solid to
the koi (its `Chain` is in the scene's surface list, never moved by a fish) and shoves the floating
petals aside. Nothing moves a duck, so a duck must go round whatever else is at the surface: the
turtle's shell (while it is up near the surface) and the beads of the snake's body and of every
swimming frog are handed to the duck's world every frame as circles, and its feelers and step rule
read them like rocks. The snake does the same in return (points along the ducks' bodies are circles
in its own clearance test, and a touch from one startles it), a swimming frog dives from any part of
a duck, and a duck that something at the surface still gets onto (the snake swimming into a waiting
hen, a frog crawling under her: feelers only keep a duck off things while it moves) recoils off it
along the line between their closest parts, onto open water only (a turtle up at the surface counts
too; and when the two closest pieces cross exactly the chain test answers straight up, so the push is
then sideways along the line between the two middles, the koi school's own fallback). Pairs that may
still overlap for a few seconds, each by nobody's rule: a frog and the snake, a frog and the turtle, the
snake and the turtle (none of them moves for the other), a duckling and a floating frog. A duck's shadow is painted only on
water in view, like every other shadow: painted on all water, it showed as a grey duck, wings and
all, lying on the blossom wherever the duck itself was hidden under a branch. The two
adults of a pair are solid to each other (overlapping bodies are pushed half the way apart along the
line between their closest parts, no faster than a body length a second: the second duck used to
stop wherever it was when the first stopped, often on top of her). And the duck's world knows that
the land goes on beyond the screen's edge: a point off the picture reads the last pixel on the
picture in its row or column, water beside water, land beside the land corner. The first version
called everything off the screen water, and a drake skirted the bank along that strip into the
bottom corner, where the bank meets both edges, and sat in the grass for the rest of the run. A grown duck
(never a duckling) may also take a chance meal: from a plain Cruise only (never a look, dabble, wait or
herd), with the bill on the picture and the director's leave (not on the way out, the mother not in the
middle of a cute thing), a `Snatch` (Tadpoles.cs, its own dice, seeded from the duck's dials) may single out
a tadpole or froglet within a tenth of the screen of the bill. Then `Doing.Snatch` closes in at 1.3 times the
cruise through the same feelers and step rule, and within a quarter of a body length tips down as in a dabble
with the neck reaching forward; the prey is eaten only if, while the head is under, the bill is within a fiftieth
of the screen of its nose (the head stays down up to 1.3 seconds while the prey is still close), else it is a
dip at nothing. A herd, a wait or the end of the visit ends it (all through
`Duck.Start`, and `EndHunt` at the visit's end, so no tadpole stays claimed).

*The frogs sit, leap and kick.* A frog (`Frog.cs`, a `Frogs` holder like `Turtles`) is rigid, so
it has a STRAIGHT spine like the turtle's (the same `Body` drawing: haze by depth, branch shadows,
squash), with a few small ovals on top: two eyes with a pale rim, four legs, a pale throat. It
never bends: it turns by hopping round on the spot, or, before a leap, during the crouch. Every
quick motion is a HOP, a script from its start to its end: a crouch (legs folded, the body swinging
round to face the way it will go), then a flight at an even speed along the ground with the height a
parabola, so it lands where its shadow is. Height is a lift up the screen and a shadow down and
right (`Light.cs`, the same rule as the petals), and the shadow is painted on the pad or the bank
where it sits and over the water in a leap. The pose is one number, 0 (legs folded in a Z beside the
body) to 1 (stretched out behind), eased, and the hops and the swimming kick both drive it. A KICK
(round thirty) is the real sequence: the THRUST (the legs sweep out and back together over 0.22 s,
the body surges), the GLIDE with the legs left stretched out behind, together, streamlined, then
the RECOVERY (the legs draw forward into the Z over 0.26 s, which costs a little speed) and the
next thrust, about one a second; it turns mostly in the thrust (2.5 radians a second there, 1.0 in
the glide), the way a frog steers with its kick. Before, the legs folded all through the glide and
the frog turned as freely gliding as kicking, which read as a sprite sliding. A LEAP gathers first
(the crouch, 0.24 s: the knees drawn in under the body, the feet back under the hips, the body a
touch shorter, `_gather`), springs (the legs straight out within the first eighth of the flight)
and lands in the water with the legs still out, skimming on at most of its flight speed and
slowing (`_skid`), its first kick coming when the glide is done; only a landing on a perch folds
the legs, in the last fifth of the flight and the rest of the way as it settles. A turn on the spot
takes as long as it is big (0.12 s plus 0.22 s per quarter turn) and a turn past a quarter turn is
two hops with a short rest between (a frog turning right round after a climb once did it in a tenth
of a second). Afloat it treads water: a lazy half-kick every few seconds. Round thirty-five, his
verdict on all of that ("their legs don't seem to sync with their swimming motion, they have no
indication that they're in the water, they look like they're just on top of everything, the
water doesn't get disturbed when they hit it"): the kick is a WHIP (in the first half of the
thrust the knees and feet sweep out wide, `_spread`, and come together as the legs straighten;
stretched, the feet sit a little apart with the webs open), each thrust leaves a small ring from
each foot and a float's lazy kick a smaller one, and a landing is a real SPLASH: a big ring, a
thin one behind it, a disc of foam that fades in a third of a second and eight drops flung up
that fall back with tiny rings (the koi leap's spray; measured, the water round a landing lifts
by sixteen to twenty grey levels where one ring had lifted it by three). And a frog IN the water
is painted in it: only the head is clear, the back slopes under, the rump and hind legs take
half the water's haze and the arms four tenths (the murk per spine point, `Body.Paint`'s
`murkAt`), so `Body.Deepen` darkens and greys them as it does a deep fish. Frogs also LIVE ON
THE BANK now: in the water a third of the time (half to two thirds before; a frog wants out after
twelve seconds wet, a koi has to come closer to send it under, two dives in one spell end the
chain, and it swims to a NEAR perch, the next pad or stone, never across the pond). The FORELEGS are not on that dial: each is three pieces (upper arm, forearm,
hand with three fingers) whose elbow and wrist are eased toward a pose per state (`EaseArms`):
planted beside the body with the hands set down under the eyes when it sits, swept back along the
flanks in the flight of a leap, held still along the sides while it swims (a frog swims with its
hind legs), spread out sideways when it floats. Its mind is on the shared parts (`Core/Minds`): two dials dealt at birth (bold,
restless), a `Doing` (Sit, Snap, Leap, Swim, Float, Dive, Climb), weighted dice, a cooldown after a
fright. The PERCHES (a lily pad with bare green leaf to sit on, not its notch or a flower; a dry bank
rock; a spot on the bank's edge) are found once when the scene is made, and one frog holds one.
A swim to a perch goes first to a point out in the water in line with it and then in along the line,
so it arrives pointing at it, and it is never planned across a gap narrower than a frog. In the
water it is solid to the koi (its `Shape`, a chain of beads, goes into the scene's list of
surface bodies), it steers round the pads (a pad is a wall to a frog, which swims ON the water, and
no wall to a koi, which swims under), and a swim whose steps are refused for over a second (wedged
between pads) ends in a hop to open water. NOTHING CLIPS (round thirty): a frog in the water is
pushed clear of the snake, a swimming turtle and a duck by the same chain test every swimmer shares
(`Frog.PushOff`, the frog being the light one and moving the whole overlap, along the ground plane,
no faster than two lengths a second), a touch from any of them is a fright, any part of the snake's
body within a tenth of the screen is a threat (not only its head: a frog once surfaced and floated
on the snake's back for two seconds), and a dived frog will not come up under one of them (it swims
on under the water and looks again, up to four times). A dive is the koi's depth rule (`Light`): smaller, hazier
and sunk down the screen, so a pad can cover it. Frogs are built last in the scene, with dice of
their own, so adding them moved nothing else. One frog is grown and the rest are young (`Frogs` hands each a size: 1 for the first, about two thirds for the others), smaller and quieter but living exactly the same life.

*A school moves as one with three rules.* The minnows (`Minnows.cs`) have no leader and no
choreography. Every frame each fish adds up a few pulls: keep going the way I am going, head for
the school's shared target, face the way my neighbours face, keep a little room from the fish
beside me, get away from a koi, turn from the bank. The sum is the way it wants to go, and it
turns toward that on a circle no tighter than about its own length (tighter, and the body curls
into a C with its nose off the line it is moving along). Those few rules, the classic "flocking"
rules, are enough for a dozen fish to behave like one creature. The school itself has one shared
mood: drifting toward a spot it picked in open view, rushing to a lure, or milling round it, each
fish on its own place in a slow ring. Each minnow is a tiny bending body on a short trail, like a
koi; its body stays nearly straight and only the tail flicks (a wave down the whole body made them
wriggle like worms, which the close-up strip showed and a still never would). The pulls are soft, and
the pull toward a school on the far side of the island beat the "turn from the bank" push, so a
minnow sat with its nose on the stone, tail beating, trying to swim through it (35 such pins in ten
minutes on one seed, up to 12 s each). A refused step is now a rule of its own, as for every other
animal (`Minnow.TryStep`, `FollowWall`): the fish FOLLOWS the wall, swimming along it on a remembered
side (the side that leads toward the school) with a little "away" mixed in, flips the side when the
way along is blocked too (a corner), and only lets the school's pull have it back when there is open
water round it AND a clear line to the school's target (`LineClear`). The side has to be remembered: a
one-frame dodge was tried first, and in the pocket where the beached log meets the bank the pull
dragged eleven fish back into the dead end every frame (10 s wiggles that the pin count, which only
sees refused steps, passed; a "confined within 12 px" count found them). The tail beat follows the
distance actually swum, so a fish that cannot move does not pretend to swim. The dragonfly's game
is the two halves meeting: a dragonfly asks where the nearest school is, flies over, hovers, skims
low tapping the water three times (three small rings), and the taps call the school; then it darts
off far and its timer runs before it plays again, taking the schools in turn.
Two things were missing from the wall-following until round thirty-six ("a minnow can swim in there and
never get back out. it just spins in fast littke circles forever", at a formation with three caps). The
heading it sets comes from the room grid's "open way" (the direction of more room), and in a gap between
two caps that direction points at one cap from one side of the gap's midline and at the other cap from
the other side, so a fish crossing the midline by a pixel had its heading flipped right round, every
frame, and stood vibrating in the gap at full speed (four fish for 288 s each on the stress run, the
heading through 4300 turns). Now a heading that would turn more than a right angle from the one it has
is read as the ridge, not a wall, and the remembered side flips so the fish keeps on along the gap; and
a fish that has followed for two seconds with room round it lets go whatever the line to the school says
(a target behind a stone is never in clear sight: a fish zigzagged on a ridge of open water for 75 s).
And the gaps themselves are closed: a crevice between two of a stone's caps narrower than about 0.032u is
filled in on the room grid by a morphological closing of the walls (a second distance transform, run in
a box round each stone; the picture, the bed and the Land mask are untouched), so no animal steers
into a gap it cannot turn in. Measured with every school sent to the three-cap formation for five
minutes (`PECKWORKS_MINNOWGOAL`): fish-seconds spinning 6030 before, 9 after, the rest of the "spins"
the mill round a lure that is meant to be there.

And since round thirty-seven a school has a DEPTH (`School._z`, shared by its fish). About one new
drift target in sixteen (`PECKWORKS_MINNOWDIP` multiplies it) is a Dip: a spot on the deep bed (floor at
least 0.65 of the depth, in view, with room; half the time within 0.04u to 0.14u of one of the deep
dwellers, `Minnows.DipNear`), swum to briskly (pace 3) while the school sinks toward 0.9 of the bed less a
margin, then a Graze of 10 to 25 s at a slow pace (the fidget and the room rule make the mill), then the
next drift with the depth wanted back at 0, the pace brisker the deeper it starts. The sink and the rise are
held to half the fish's MEAN SPEED as swum (Light.HoldDepth's share with the nose across the screen: a school
that sank faster than it swam would be seen sliding down the screen; from the mood's pace it was 77 px/s on
the first frames of a scatter, before the fish had got up to speed), and each fish's own depth moves no faster
than half the distance IT REALLY STEPPED last frame can hide (not the speed it wanted: refused at a slope it
sank on the spot) and is the school's
capped by the bed under its nose less a margin, EASED, down at the school's rate and up at that or 0.03 a
second (a cap taken outright dropped 0.03 of the depth a frame at a hollow's edge: an 800 pixel a second
slide on the first run; a rise floor of 0.08 bobbed a grazing fish 17 px/s over a hollow's edge, a tenth of
the deep fish-frames reading as slides); a deep fish's step is refused onto
a bed shallower than it is (unless the bed here is shallower still), so a school keeps inside its hollow
rather than riding up the wall. A fish is drawn shrunk about its nose by the perspective and sunk down the
screen by its depth (`Minnow.LaySpine`, the identity at the surface, so a school that never dips is drawn
pixel for pixel as before), with the pond's own haze for its depth, through the deep stencil from a quarter
of the depth down; a school past a quarter of the depth is drawn BEFORE the koi (`Minnows.DrawDeep`), under
all of them at once, one cut rather than a place in the koi's depth order (a dozen fish a few pixels long).
A school down on the bed is left alone by the dragonfly's lure, the koi's tease and the snake's hunt, is
scared by every koi's head whatever its depth rather than the surface list, and is what a crayfish's claw
can reach.

*Tadpoles hang, burst and grow up.* A clutch (`Tadpoles.cs`: `Tadpoles` holds clutches, a `Clutch`
is the cluster, a `Tadpole` is one animal and, later, the froglet it becomes) lives in water shallow
enough that the bed shows, near the cattails. Unlike the minnows they have no flocking: no
alignment (they do not face one way), only a gentle pull to the cluster's middle, a little room
between neighbours, and each one's own temper (how often it bursts, how far from the middle it likes
to be, whether it grazes at a rock, the bank or a pad). Each is a bending body on a short trail like
the minnow, but the body is a stiff lump and only the tail waves (a slow small wave when sculling,
seven beats a second in a burst). A burst is five body lengths a second for half a second and the speed then bleeds
off over a quarter of a second, a coast to a stop: at ten lengths a second, switched off dead, with the tail at ten
beats, a tadpole read as a mosquito skating on the water (his note after twenty-one, 2026-10-20). A tadpole that is
nearly still cannot change shape by moving, since
the spine is laid along where the head has been, so it swings its whole body round a point in the
middle of the body (a rigid pivot) before a burst, curling the tail into a C; a moving one turns by
bending. It hangs at 0.9 of the bed's depth under its nose, drawn shrunk, hazed and sunk by that
depth as the koi are, with a small shadow hugging it on the bed; a spot where the bed is too
deep to show is refused like land (except to a clutch moving house, below), and so is a heading whose bed rises so fast that the lift would
cancel a burst down the screen (the first timeline showed bursts that went nowhere). Growing up is
slow and one at a time: buds, hind legs, forelegs, the tail shrinking to a third, then a froglet
with the frog's look at a smaller size that rises to the surface and kicks (a push of a quarter
second, a glide) toward whichever screen edge has the fewest non-water pixels on the way,
shoving petals aside, and is removed when it leaves the screen. When the cluster has thinned to
three a new hatch of half-size tadpoles appears at the cattails or a pad's rim. THE POND'S LAW
(nothing clips): tadpoles and froglets are far too small to be solid to a fish or a pad, so they
pass under every koi, pad and turtle and flee them (they are not in the scene's surface list);
they never enter land, rock or the shade of a branch; a froglet at the surface is solid to the
floating petals only. *Haunts and ventures (round twenty-one).* A clutch does not stay at one spot. Its
home (the point the tadpoles keep to) has a place, `Clutch.Haunt`: Shore (within 0.3u of the cattails,
as the first home; the last shore home is remembered, so the way back always exists), Island (a ring
round the island's dry cap where the bed climbs the shoulders into sight) or Open (the outer shallows,
beyond 0.15u of the cattails and 0.12u of the island's edge). Every two to five minutes (`HauntStay`) it
rolls the next one (half Shore, a third Island, the rest Open; no island on the seed, no Island), picks
a spot of that kind with `Good`, the test the first home passed, at least 0.12u from where it is, and
plans a ROAD to it (`PlanRoad`): the straight line, checked every 0.01u by `Tadpole.CanTransit` (water
in view, not land, not under a pad, not the island's cap or a rock, 0.015u of room round it), or round
one stop. A spot with no road is dropped and another of its kind tried (six times), then the next
kind; with none at all the move waits 15 s: a cluster drifting through a stone would be a clip. The
home then slides along the road at 0.014u a second (`AdvanceHome`), slowing to a third of that as the
middle tadpole of those not scared falls 0.06u to 0.14u behind, so the cloud arrives together; on
arrival it stands still until that middle tadpole is within 0.07u, and only then is the move over. While
it lasts (`Tadpole.Transit`) tadpoles may step over water too deep to see the bed in, which no tadpole
otherwise does (`OkFloor`), and there they hang at `Tadpole.DeepZ` (0.3: the sunlit upper water, hazy
but seen); out over deep water they are also less easily startled (0.055u, not 0.09u) and bolt at five
body lengths a second (six in the shallows), veer a little toward home when they bolt, and burst twice as often and scull home at two lengths a second, because
the open water is full of koi (without that, 30 percent of a migrating cloud's frames were flights and the
cloud was strewn across the pond). A straggler left out in deep water afterwards swims back by the
old rules. A scare keeps the clutch's spacing: a fleeing tadpole bends its flight away from any
neighbour within 2 body lengths (wider than the 1.2 it keeps when idling: at 1.2 a flight still ended
as a packed cloud at that spacing), by at most half a radian (uncapped, a packed clutch's push sent
tadpoles back toward the fish that scared them, and one clutch fled on the spot for a minute), when
straight away is blocked each tadpole tries its own side
first (half swerve left, half right, by a roll already made), so a flight splits round a rock, and
each scared tadpole's next burst comes at its own moment (1.2 to 2.2 s, off a roll it already has)
rather than the whole clutch winding up together. And a tadpole HABITUATES: after 2.5 s of unbroken
fleeing it stops minding what is there (minding again after about 7 s calm), and a flight under way
keeps its direction unless the new one is a radian or more off. The instrument that found this was
the per-minute table of states: a fish parked beside the clutch (a resting koi, the turtle on the
island's cap) kept every tadpole in Flee for as long as it stayed, re-aimed every frame, speed zero
while the turn was large, so ten tadpoles sat turning on the spot in a knot for a minute: that knot,
not the flight, was the fuzz. Before that every tadpole ran the same way from
the same fish with no spacing at all, and the tightest moments of every clutch were flights piled
against the island ("a fuzzy ball of fuzz", his note after twenty-two). Measured on three seeds of a
five-minute stress run: the tightest five seconds went from 1.0, 0.5 and 0.4 body lengths from the
clutch's middle to 1.5, 1.4 and 1.0 with the first two changes alone. The pond is deep: the bed is lost in the haze 0.12u off the bank, so the island can only be
reached across deep water, and the Open haunt is only a little way beyond the Shore one. When the
cluster has thinned to three it heads for the Shore first, because a hatch only happens there. A
VENTURE (`TryVenture`, `Tadpole.Venture`): about once a minute (`VentureGap`, 50 to 90 s) one calm
free-swimming tadpole is given a standable spot 0.10 to 0.20u from the home with a clear walk to it; for
it "home" is that spot (its own rules do the swimming), it stays 6 to 14 s (it may graze), then home is the
clutch's again. Not while the clutch moves. A tadpole pinned for two seconds (every step refused, or no
free heading) may crawl out through the hidden band round it (`CanCrawl`: only its nose must be in view):
a hatchling in a one-pixel nook at the cattails' foot used to sit there for good. They are drawn in three
passes by depth (`Clutch.Pass`): the clutch and a
deep froglet before the koi, a froglet in the top of the water right after the koi (over the fish
below it, under the ripples and the floating petals), and a froglet with its back out after the
petals. The petals heaped against the bank are part of the painted backdrop, so the pond keeps the
list of their pixels (`Pond.Heap`) and copies them back from the backdrop each frame once
everything under the water is painted (`Pond.CoverWithHeap`): a tadpole under the heap is hidden
by it, like a fish under the live petals.

*Tadpoles are food, taken by chance.* The owner's rule: an animal that happens to be near the
tadpoles and sees them will sometimes single one out, chase it and eat it; nothing goes looking for
them. One small shared piece does the "sometimes" and the "single one out" for every eater
(`Snatch`, at the end of `Tadpoles.cs`): each predator owns one and asks it three things a frame.
May I start (`Look`: its own dice first, at a chance per second, so a fish needs a few seconds beside
the clutch to make up its mind; then `Tadpoles.Spot`, the nearest living, unclaimed, visible tadpole
or froglet within sight of the mouth; and it will not look again for minutes after a chase, hit or
miss). Where is it now (`Prey`: it flees, so the goal is read afresh). Have I got it or lost it
(`Chase`: eaten when the animal's own idea of its mouth is within its own idea of reach of the prey's
nose, `Tadpoles.Eat`; given up when the patience of a few seconds runs out). The piece never steers:
a koi runs its ordinary goal and depth at the prey (flat out, down to the tadpole's depth, the bed's
clamp keeping it honest), a turtle paddles half again as fast with its feelers still on, a sitting
frog uses its dragonfly snap (one hop; the prey is taken only if the snout lands on it), the crayfish
walks at one within a few steps and then stands and feeds, and the snake and the ducks have their own
gestures (their paragraphs). Anything that takes the animal over (a flee, a bump, a herd, a scare)
abandons the chase in one line at the top of its `Begin`. A prey has one hunter at a time (`Hunter`)
so two fish never both "catch" it. Each `Snatch` rolls its own `Random`, seeded from the animal's
dealt dials: one roll from the animal's own dice would move every roll it makes afterwards, and the
builds are compared at one seed. An eaten tadpole leaves as `Exit.Eaten`, counted by its clutch, which
hatches more when thinned; a small ring marks a catch near the surface.

*A lizard on land is the koi's method on the ground.* The lizard (`Lizard.cs`) is the same
bending spine, its head leading and its body and long tail laid along the trail behind it, with
a running S-wave that grows down the tail and dies away when it stops, and four sprawling legs
swinging on opposite corners in step: each an upper leg out sideways, a lower leg turned forward
at the knee (bending more on the push of a step) and five long thin toes spread like a hand, a dark
claw on each (`Lizard.Legs`; one small oval when a toe would be under 2.5 px). They were one flat
oval each until round twenty-three ("he currently has the turtle paddle limbs", his note); the
5x and 6x crops that judged the change are in the kit. What makes it a lizard rather than a small snake with
legs is the RHYTHM: a dash of under a second, then dead still for one to three, then another; a
climb onto a dry rock and a long bask; and it comes in from the screen's edge and leaves the same
way, so it never pops into being. It asks the land mask (the ground plus whatever stands in the
water, a rock's wet side, the log) before every step, and it turns no faster than its speed
allows, because a pivot on the spot folded its tail into a hairpin on the close-up strip. Its
shadow is the same body painted dark and faint a little down and to the right, so it lies ON the
grass. It is drawn after the lawn and before the irises, so it runs over the grass and the rocks
and can pass under the iris leaves.

There are two lizards (a `Lizards` holder, like `Turtles`), and each has a small mind in the
koi's style. Two dials are rolled when it is made: bold (stays out longer, basks longer, goes
looking for company, wins a standoff) and energy (longer dashes, shorter rests). The dials only
load the dice that are rolled whenever a rest ends: roam, climb a rock, go and drink, or go and
find the other lizard. A drink is a dash to a spot a body width and a half short of the water
(found by walking from the lizard toward the pond's middle until the land mask says water), a
stand with the nose to the water, and one tiny ring half a second in. A meeting is spotted by
`Lizards`: both out, on screen, noses within two body lengths, neither met anyone this visit.
Both walk up and face each other for about two seconds, then the bolder charges at the other,
which runs far away from it and leaves within a dash or two. If the snake's head comes within
a fifth of the screen height, a lizard drops everything and runs for the nearest edge it can
reach over land. Two lessons from the instruments: a lizard that had run up to the shore facing
the water only ever turned one frame at a time (every step was refused), so it stood for minutes,
and now it steps off along the shore toward its goal; and "the nearest edge" can be water, so the
way out is a land pixel on an edge with an all-land line to it, or a hop toward one. Those three
walking rules (the step that slides along the edge, the all-ground line, the longest clear run
away from a threat) are now `Core/Minds/Ground.cs`, written once for anything that walks on a
surface with an edge, and the lizard's trail is the shared `Trail`. Two more things it does: when
a dragonfly sits on a dry bank rock within reach (dragonflies perch on stones as well as pads
now), a lizard may STALK it, a creep at a wary walk that stops a body length short, at which the
dragonfly takes off away from it; and a swallow passing low over the bank (`Swallows.Shadow`)
sends any lizard within 0.12u bolting for cover, not again for ten seconds.

*A crayfish walks on the bed and escapes backward.* The crayfish (`Crayfish.cs`, a wrapper like
`Lizards` holding one `CrayfishBody`) is a rigid shell on a straight spine (the turtle's way), with
the abdomen's curl in a tail flip drawn as a shortening of everything behind the carapace. Its
legs, two long antennae, two claws (arm, palm and two fingers that open) and eyes are small
ovals and lines laid on that body, each driven by a pose that is eased frame by frame: claws held
ahead, raised and wide (threat), closed (flip), or one reaching to the mouth (feed); the legs'
phase runs with its signed speed, so they ripple forward when it walks and backward when it backs
up, and freeze when it stops. It turns by pivoting about the middle of the shell, never about the
nose (turning about the nose swept the middle across a lily pad with no step ever tested).
Where it lives is worked out once, when the scene is made: from the middle of each bank rock and
the log it looks outward for the first open water, lays the body down under the stone, and keeps
the place only if the whole body is on non-water pixels there (so it simply is not drawn, which is
what "under the rock" looks like), the bed shows a little way out (0.02u to 0.12u deep), and a
dozen free walks lead away from the doorway. The picture is only drawn on open water, so a claw,
the head and then the tail appear at the stone's edge as it comes out. On the bed its Z is the
floor under its middle (never deeper than it), so it is sunk down the screen, hazed toward the
water and shrunk exactly as a koi at that depth; a step to a pixel that is not open water, or
where the haze would pass 0.65, is refused. A walk has arrived when its goal is within a quarter
of a length of the nose (the claws' reach): a turn swings the nose round the middle on a circle of
that radius, so a goal nearer than that lay on one side of the nose one frame and the other side
the next, and the crayfish shivered to and fro until the walk's clock ran out ("stuck on the rock,
jitters and vibrates" when the goal sat beyond a stone). A refused step ends the walk and bans the
next one for two seconds (rolled again at once, the dice picked a new walk toward a new goal, the
body swung, the step was refused again, frame after frame), and on the way home a refused step
holds the body for most of a second before the next waypoint is planned. Boxed in on the way home
(it walked nose first into a notch of the bank where every way out but the way in puts its middle
on stone) it backs out along its own heading while the planner keeps trying, and after three seconds
of that slides straight home under the stone rather than vanishing where it stands (it did, once in
twelve seeds). It is solid to nothing: a koi is kept a margin of
water above the bed by its own rules, so every fish passes over it, and the crayfish reacts
instead. A koi, turtle or duck within 0.09u (a bold crayfish: 0.06u) sends it flipping backward
two or three times, 0.1u in 0.15 s each with a 0.4 s pause (a flip that would end on a refused
pixel stops short), swinging round first if the threat is behind; a bold one with the threat
ahead raises its claws first and flips only if it comes within 0.05u. Four seconds between scares.
Then it holds still with the claws up for a second or two, walks home and slides back under the
stone. A visit is 30 to 95 s of activity every two to five minutes (the "Crayfish" knob).

*Prawns are the crayfish's method, made small, see-through and always out.* The prawns
(`Prawns.cs`: a wrapper like `Crayfish`, a `PrawnBed` that answers "may a prawn stand here" and
finds the homes, and one `Prawn` per animal) copy the crayfish: a rigid body on a straight spine
(ten points, rostrum to tail fan), legs, antennae and eyes laid on it as small ovals and lines, a
mind on `Mind<Doing>` with two dials dealt at birth by `Traits.Deal` (bold, busy), and a tail
flick drawn as a shortening of everything behind the carapace. What differs: a prawn is
see-through (the body colour carries an alpha of 0.5 to 0.6, so the bed shows through it, with a
darker, more solid gut thread and stripes), it has no rock to hide under (its home is a patch of
visible shallow bed, floor 0.02u to 0.065u down where it can, within 0.15u of a stone that stands in
the water, with room all round and twelve free lines out, found once in the constructor and at
least 0.08u from the other prawns' homes; a seed that has no such place simply has fewer
prawns), and its position is the MIDDLE of the body, so a pivot turns it about its middle. It
feeds most of the time (the front legs reach in and out at about two beats a second), walks at
0.012u a second with the legs rippling four steps a second, rarely swims (0.025u a second, lifted
0.01u to 0.02u off the bed by a bell-shaped rise, the legs trailing and, when it is long enough on
the screen, four swimmeret paddles beating), and turns in a pivot of 0.3 s a quarter turn. A
scare is any koi, turtle or duck within 0.07u (bold: 0.05u) of its middle while it feeds, walks or
swims: it swings its tail toward the roomiest way back (0.15 s) so that backward means away, then
flicks two to four times, 0.06u in 0.12 s each with a quarter second pause, a flick that would end
on a refused pixel stops short and a later flick with no room behind it is dropped, then sits
still a second or two with its feelers spread and walks back toward home if it was carried beyond
seven tenths of its range. Three seconds between scares. It keeps within 0.2u of home (bold: 0.3u), plus the
one flick a scare may carry it. Solidity: a prawn is solid to nothing and passes under everything
(the fish pass over it, the pads and petals cover it, the stencil of open water hides it under a
branch, and two prawns may pass over each other, being tiny and see-through); the pixels that are
not open shallow water (the land, the stones and their caps, the pads, the cattails' foot, water
where the haze would pass 0.65) are walls to it, tested at its middle and both ends by the step
rule, so a wanted step onto one is refused. On the bed its Z is the floor under its middle (eased
over 0.3 s, never deeper than the bed), so it is sunk, hazed toward the water and shrunk like a
koi at that depth. At the preview size (a prawn four pixels long) it is one soft dash. It is
drawn right after the crayfish and before every fish. `PECKWORKS_PRAWNLOG` names a file that gets
one line per prawn per frame (position, doing, depth, heading, speed, pose, the drawn rostrum and
tail, the nearest threat's distance and whether it was visible).

*The deep dwellers are the prawns' method again, one class for two species, with claws, fights and a
snap.* `DeepDwellers.cs` (round thirty-seven, "a crayfish and prawns of maybe different species to occupy
the deep ... battle each other ... skitter along the deep areas barely visible, foraging ... eaten by some
of the largest koi, sometimes turtles, and snakes ... a chance of catching a minnow in its pincers") holds a
wrapper like `Prawns` (and the referee of the fights), a `DeepBed` that answers "may a dweller stand here"
(open water in view through the DEEP stencil, so the stones' caps stand in front of them, with a floor at
least 0.62 of the pond's depth, 0.33u: `DeepBed.DeepZ`, and the sunk picture of the point on open water
too), and one `Dweller` per animal, a `DeepKind` of Crayfish (a signal crayfish, 0.072u to 0.082u at the
surface, drawn at about half that on the bed) or Prawn (a long-armed prawn, 0.05u to 0.058u). Every third
from the first is a crayfish. Homes are deep free spots with room all round, 0.15u apart, found once. The
mind is the prawn's (Feed, Walk, Turn, Flick, Settle) plus Threat (claws up at a fish over it, by the bold
ones), Bout and Lunge; a feed is 6 to 25 s and a walk 1 to 4 s at 0.024u a second (a prawn 0.02u, twice
the shallows' prawn's), the skitter he asked for; a walk stops short of another dweller in its way (a fight is the only touch). The
claws are a shared geometry (`ClawPoints`: shoulder, elbow, palm, tip, in ground pixels) that both the
drawing and the snap use, eased by `UpdateClaws` toward the pose the doing wants: held ahead, raised and
wide (a display), locked (the grapple), folded (a flick), one to the mouth (a crayfish's feed, or either
kind eating a minnow), or shot out at the aim and snapped shut (a lunge). A crayfish's claw is an arm, a
broad palm with the chalk-white hinge patch and two fingers; a prawn's is two long thin segments and a
small pincer, in bluish grey.

THE FIGHTS are one object, `Bout`, that moves both animals, so the pair stays in step. The referee
(`DeepDwellers.Referee`) hands one out when two dwellers are both idle, both free, within 0.14u on the
ground and rested from their last (four to eight minutes a pair): a roll a second at about one chance
in thirty, likelier for two bold ones. The winner is rolled then, from strength (length, kind, boldness:
a crayfish counts half again; the weaker wins one time in five). Face (both pivot to face each other, and
nothing follows until both are within 0.15 rad of it; not there in 2.5 s, no fight: a display begun with
one still side-on had the pair walking into each other, middles 6 px apart on one natural run), Display
(claws up and wide, feelers whipping, a slow edge forward, 1.2 to 2.5 s), Close (a walk's pace until the
claws' reach of each meets, or it comes to nothing after 5 s), Grapple (2 to 5 s locked, the pair moved as
ONE along the line between them, by the same vector so the gap never changes: a rock to and fro at 1.4 Hz
by a third of the claws' reach plus the winner's gain of a third of a walk's pace toward the loser, who
gives way first; six tenths of a second of steps the bed refuses ends it, the pair rocking in place
meanwhile: ended at the first refusal, a pair against the deep's rim had five grapples of 0.0 s in one
run), Break (the loser flicks away, backward, with its own escape, the winner stands with its claws up a
second or two). A pair whose closing walk the bed refuses stands off, claws up, and parts without a
grapple (Close gives up after 5 s). The claws' reach is in the DRAWN size (`_len`, the perspective size the body
is laid out in): with the surface size the first fights grappled 35 px apart on the screen.

THE SNAP (`Lunge`): while it feeds, walks or is settled, a dweller asks the schools for the nearest minnow
at its own depth within twice the SNAP'S reach (`SnapReach`, where the pincer's tip is with the claw shot
out, about 1.85 times the resting reach; `Minnows.NearestFishDeep`); it turns to it if it is not nearly
ahead (a quick pivot), DASHES at it (up to 0.6 s at three times its walk, the fish's nose read afresh each
frame, over the moment the fish is at the snap's reach: aimed at the resting reach, the extended claw
overshot the fish by half a body and every snap in twenty stress minutes missed), then the near claw shoots out
toward the fish (the palm along the arm, so the whole claw points at the aim), the pincer open, the reach half
again, and at the snap (0.2 s) closes on whatever living minnow's nose is within 0.45 of a length of its tip
(`Minnows.Take` with a depth window, whose TOP must be given: its default top is the snake's 0.3 and excluded
every deep fish for seven builds, found by tracing one snap against the minnow log; a minnow is two thirds of
a dweller's length, so a pincer closing on its front half is a hold): a hit is
held, a silver sliver across the fingers eaten away over 12 to 20 s of feeding, a miss is a miss, and
either way the school bursts away from the claw (`Minnows.Scatter`). Six to twelve seconds between snaps.
(With the look at one reach and the snap from where it stood, ten stress minutes with the schools on the
bed most of the time gave three snaps and no hit: a dweller is slow and a milling minnow is seldom within a
claw of it.)

The rest of it: scares as the crayfish's (within 0.08u, bold 0.055u, of its middle: a bold one with the
threat ahead raises its claws and flicks only if it comes within 0.045u, else it flicks at once, the tail
toward the roomiest way back, two to four flicks of 0.1u, a prawn 0.07u; four seconds between), from
every koi's head and middle whatever its depth, the turtles, the ducks' heads and the DIVING snake's head;
a range of 0.25u from home (bold 0.35u) it walks back to; drawn through the deep stencil with its own
shadow a hair down and right, legs, feelers, body, claws and eyes, all darkened and greyed by its depth
(`Body.Deepen`) and hazed toward the water, so that at the bed it keeps about four tenths of its own
colour (the same-scene gate's footprint read 7 to 12 grey levels of difference against the bed: barely
visible, as asked). `PECKWORKS_DEEPLOG` names a file for a line per dweller per frame (doing, place, depth,
heading, speed, curl, visible, nose, fight partner and phase, lunges, catches, wins, losses, scares, the
nearest threat, alive, seconds of a held meal); `PECKWORKS_DEEPBOUT` multiplies the fights' chance and
divides their rest; the eaters' looks and the claws' hits and misses go to `PECKWORKS_SNATCHLOG`.

*A field mouse is the lizard's method, made small and shy.* The mouse (`Mouse.cs`, with a `Mice`
holder like `Lizards`) copies the lizard's shape: a spine laid along the shared `Trail` (14 points,
the first 8 a plump body and the last 6 a very thin tail), the three shared walking rules in
`Core/Minds/Ground.cs` for stepping, lines and runs, a land mask asked before every step, a
faint shadow that is the same body painted dark, and a `Mind` of doings. What makes it a mouse is
its rhythm and its love of cover. It scurries 0.2 to 0.6 seconds at about three body lengths a
second, with the tail streaming straight behind (still, the tail lies in a slow lazy curve), then
stops dead for a sniff (the nose pulled back and forth a hair at about 3 Hz), a sit-up (the body
15% shorter and the head lifted a few pixels up the screen, its shadow left on the ground, which
is the gap that reads as height), a groom (a quick sideways shiver of the body) or a nibble. Its
next spot is chosen by SCORE: lawn is best, the foot of a dry rock or the side of the log is good,
bare sand is poor and taken only when nothing else is in reach. When the snake's head, a lizard's
nose or a swallow's shadow comes near it bolts (twice as fast) to the nearest lawn spot further
from the threat, and on arriving in grass it usually VANISHES there: a 0.3 second fade out,
hidden for 10 to 40 seconds, a fade back in at the same spot. Two dials, timid and greedy, are
rolled once and only load the dice. It leaves after 8 to 16 stops, through the screen's edge, and
only when its whole length, tail included, is off the screen.

*The snake feels its way.* The snake (`Snake.cs`) used to run on rails: a curve planned when it
set out, so it swam straight over a lily pad as if the pad were a shadow. Now it is steered like
the koi. It has a goal (a point well beyond the far edge of the screen) and a heading, and each
frame it feels along its heading with six points from just past its snout to a body length ahead,
asking how much room there is to the nearest lily pad, rock, the log, the cattails or an iris
clump (all held as circles, the log as a thick line). When the room ahead drops under a margin a
little wider than its own weave, it tries headings either side and takes the smallest turn that
clears the thing; "enough room" counts the same as "lots", so it skirts a pad rather than swinging
out for the middle of the pond. The body follows the head's trail, so the whole snake flows round
the pad. Two lessons the log taught: the feelers must start close (the first build's nearest
feeler was a third of a body length out, further than a pad is wide, so it sat jammed against a
pad for two minutes with every feeler in clear water beyond it), and a step that is refused
(the next spot would be inside something) must itself force an avoidance turn, or the goal's pull
holds the nose against the obstacle for ever. It is also nervous: every frame it checks a point on
each koi, turtle and the lizard against points along its whole body, and a touch startles it, a
burst to three times its speed, a hard swing away from what touched it (the weave quickens by
itself because distance drives it), a ring thrown off, and then a few seconds of calming down. It
cannot startle again for ten seconds or so, so a koi keeping pace does not set it off every frame.

*The snake has a small mind.* Like a koi, the snake does one thing at a time (a `Doing`) and rolls
weighted dice for the next when that one ends. Two dials are rolled at the start of each visit,
boldness and nerve, so every visit is a slightly different snake: a bold one has more "hunt" and
"lie on the log" faces on its dice, startles only at a close touch and calms sooner; a nervous one
has a heavier "just cross and go" face, startles from further off and stays frightened longer. The
doings: *Cross* (swim for a point beyond the far edge and leave, the only way a visit ends),
*Explore* (swim to a spot in open water, then another), *Hunt* (creep toward the nearest school of
minnows, from `Minnows.NearestSchool`, slowing to half speed over the last bit with a narrower
weave; when its head is 0.12u from the school's middle it lunges for about half a second at three
times its cruise with a ring off its head; the minnows already flee a snake's head, so they scatter
by themselves) and *Bask* (only if the log floats: swim to a point beyond the log's near end, climb
on along the log's own line, and lie there 10 to 30 seconds). While it lies, the steering is
switched off and the spine is simply laid on the log's middle line with a gentle S, eased in over
a second and a half from wherever the body was; a snake longer than the log lets its tail hang off
the near end into the water. Then the lying body becomes its trail, it faces along the log, throws
two rings and swims off the far end. The log is not an obstacle to a snake that is climbing on,
lying on or leaving it, and is to every other doing. All of this only chooses a goal and a speed:
the feelers, the "never stuck" rule and the startle (which ends a hunt or a lie) still have the
last word. The weave's width is eased from one doing's value to the next, so the head never jumps
sideways when the doing changes. *A chance meal* (`Doing.Snatch`): while it is only crossing or
exploring with its head on the water, a tadpole or froglet at the top of the water within 0.15u of
its nose and in front of its head (within 75 degrees of where it points; the tadpoles bolt from 0.09u, so a smaller look would leave no time to begin) may be
singled out (the shared `Snatch` in `Tadpoles.cs` rolls the dice, a bold snake sooner, then finds the
nearest one). For up to five seconds the goal is that tadpole's nose at a pace of 1.4 cruises, and
whenever the nose is within 0.12u (a tadpole bolts at 0.09u) and within 20 degrees of dead ahead it
lunges for 0.35 s (cut short if the nose ends up behind the head) at 0.6u a second (a tadpole's burst is about twice the snake's cruise, so a slower
lunge never closes), with a ring off the head; the catch is the nose within 0.02u of the head. A
touch, a hunt, a tadpole diving out of reach or the visit ending drops the chase, and it rests four to eight minutes after any chase.

*The snake dives* (2026-10-27, "make the turtle and the snake be willing to go deep also"). Half of
its explores are dives: the head eases down to a third to seven tenths of the pond on the way to
the spot, and the body follows it, because the trail it is laid along remembers the depth the head
had at each point (`_trailZ`, the koi's way), so it goes down head first and the tail follows. Each
point is drawn sunk down the screen by its depth, smaller and hazier (`Light.SinkPx`, `Scale`,
`Murk`, nothing at the very top so on the water it swims clear as before), through the deep water
stencil that leaves out the stones' collars and caps, and under the fish: a deep snake is drawn
before the koi school (`DrawDeep`), so every fish shallower than it paints over it (a fish deeper
still is a known pass). Deep, it is on no surface list (a frog is not pushed off it, it brushes no
petal, no bow wave), it hunts and basks at nothing (the dice drop those faces, the scans skip), a
step onto a bed too shallow for its depth is refused, and the end of the explore, or any other
doing, eases it back to the surface over ten seconds or so. The head reads the bed a little AHEAD
of it as well as under it, and when it is deeper than that bed allows it rises at a set rate
(round thirty-five, "snake gets hung up on prawn somehow. happened near a rock": with the head
rising only with the bed already under it, every step at a steep slope, a rock's hill where the
prawns live, was refused until the 5 s jam breaker, the koi's old deadlock in a new body).
`PECKWORKS_MINNOWLOG` names a file for a line per minnow per frame (school, fish, mood, place, heading,
speed, wall side, room, whether it moved, flash), `PECKWORKS_MINNOWGOAL=island|formation|formation2`
sends every school's idle target to that stone for a stress run, and `PECKWORKS_MASKDUMP` names a
folder for the steering masks as PGM pictures (water, open, land, stonetop, shoal, the room grid at
every pixel) so a script can flood-fill them for a pocket before a mind is blamed (all three round
thirty-six); `PECKWORKS_DEEPLOG`, `PECKWORKS_DEEPBOUT` and `PECKWORKS_MINNOWDIP` are round thirty-seven's (the
deep dwellers' log, their fights raised, the minnows' dips raised). `PECKWORKS_SNAKELOG` names a file for a line per frame (clock, doing, place, depth, speed, the
hunt's note, whether the step was refused, the bed's cap); `PECKWORKS_SNAKEDIVE=1` makes every
explore and crossing a dive, for the instruments.

*The snake gets hungry* (round twenty-seven). `Snake._hunger` runs from 0 (full) to 1 (famished), is kept across visits and
rises with the clock (0 to 1 in 8 to 14 minutes, a bold snake sooner; the rate is worked out from the two dials, never rolled).
A lizard or a mouse brings it to 0, a tadpole or froglet down by 0.15 to 0.25, a minnow by 0.1 to 0.15. Above 0.5 the wait
between visits runs faster (twice as fast at 1); above 0.3 ("peckish") the prowl's face in the dice, how far prey is noticed
(0.9u grows to 1.44u), the chance of going to look and the tadpole look all grow, and the minnow Hunt exists at all (below 0.3 it
never strikes at a minnow). The Hunt aims at the nearest single fish (`Minnows.NearestFish`), lunges from 0.125u (just outside the
0.11u at which a minnow bolts) when the fish is within 20 degrees of dead ahead, 0.35 s at 0.6u a second; the outcome is rolled
at the launch (50 percent, 70 for a bold snake) and when it lands the jaws take the nearest minnow within 0.028u (`Minnows.Take`:
a school never drops below four, the taken fish is simply gone, and a newcomer joins from a water pixel on the picture's edge three
to six minutes later from the school's own dice). A take, like a caught tadpole, is a 0.7 s gulp: the snake brings itself up short,
the head swells a little, the neck bows, a small ring leaves the head; no bulge. A famished snake (0.8 and over) may strike three
times a visit and a hungry one stays a little longer. A crossing or an explore that has had its head on the lawn for 30 s heads
for the nearest edge of the picture (`WatchLawn`): a hungry snake goes to the land corner more often and could be trapped in the
pocket between the bank rocks and the edge.

*Five skins and a hunt.* Each visit wears one of five skins (`Snake.Colour(s, v)`: a shimahebi,
aodaisho, yamakagashi, hibakari or karasuhebi), picked from the two dials already rolled at the door
(`(bold*1000 + 7*nerve*1000) mod 5`), never from a new roll, because one extra roll there would move
every later roll in the scene and the owner compares builds at one seed. The marks are looked up from
(s, v) with a sine or two, so they ride along as the body bends. The same trick gives the snake its
own dice for the hunt (a `Random` seeded from the dials), so a meeting never touches the scene's dice
either. A snake between visits is drawn out by prey: its clock runs twice as fast while a lizard
or a mouse is out. THE HUNT has four new doings. A lizard or mouse that is out, within 0.14u of the head and
ahead of it (75 degrees), while the head is on the land or within 0.03u of it, is a MEETING; a snake
that happened on the animal rolls once per animal per visit whether to go for it (50 to 90 percent by
boldness), one that went to look at it (a prowl, from 0.9u) goes for it without a roll, and then Stalk
creeps. A prowl pulls toward the prey as hard as a stalk does (1.8, not a cruise's 0.5: at 0.5 the turns
round the bank rocks won and a prowl toured the far side of the pond), sees the prey from 115 degrees
round instead of 75 (three prowls in seven passed it broadside), and may put its head a twentieth of
the screen past the picture's edge and no further (the prey sits on the land corner right by it; with
no limit one prowl toured the whole pond off the picture for 46 s). THE WAY OUT IS THE WAY IN: the
snake lays a crumb every 0.03u of its path with how open the water was there, and an explore or a
crossing that begins in a tight spot (the room grid under 0.035u: among the bank rocks or on the
lawn, where a prowl or a stalk took it) first walks the crumbs back to the last one in open water,
at the stalk's margin and with no edge walls, then goes for its goal. It does not walk them one by
one: each frame it takes the furthest crumb it can reach in a straight clear line (`LineClear`), because
the crumbs of a looping stalk loop too, and walked in order they replayed the loops (45 s to get 0.3u
away on one seed). Without that the goal lay beyond
the rocks, the picture's edge was a wall, the feelers found only ways ALONG the pocket, and the snake
paced it for 73 s ("only move in circles on that land"). A snake inside two things at once (the edge
wall and a rock) TURNS toward the way out at most 4 radians a second instead of facing it outright:
set outright, the way out flipped between the two every frame and the head shook. THE ORBIT
BREAKER (round twenty-nine): a goal on the far side of a lily pad cluster, or of the bank rocks
while it is on the lawn, makes the pull toward the goal and the avoidance round the obstacle turn
the head the same way, and the head goes round and round at full speed in a circle a third of a
body length across with the body laid along it (the owner saw "the snake balled up on a lily pad";
the timeline found a 12.6 s orbit of 100 by 76 pixels at 720p on the lawn, nothing blocking it).
The snake sums how far its heading has turned with an eight second memory (`_spin`); a whole
circle and a bit (6.9 radians, more than any honest turn round a stone) is an orbit, and the
answer is a DETOUR: for four seconds the pull is off and it simply swims on, steering round
whatever is in the way, so it comes out past the thing it was circling; a retreat along crumbs is
given up at the same time (its crumb may be the thing being circled). The stalk itself
creeps at a third of a cruise with the weave at a quarter, the feelers still on. The stalk is dropped
if the prey's nose gets further than 0.2u (it ran or hid; counted as abandoned). At 0.05u it Strikes:
the neck is drawn back into an S for a third of a second (a drawing-only offset laid over the front
third of the spine, the koi's chain is untouched), then the head shoots to the prey's nose in 0.12
seconds with the mouth open (the head's drawn width x1.5). The outcome is rolled at the launch (50
percent, plus or minus 15 for the snake's boldness and the prey's agility, held to 25..80); the snake
tells the prey through `IPrey.Strike`. A catch freezes the prey where it is until the jaws arrive. A
miss makes it dodge in the same frame: the lizard turns and flings itself 0.04u sideways in 0.15 s (the
heading swings toward the escape at 10 rad/s, the body bends after the head and the trail is carried half
the way: moving the nose alone left the body on its old line, folded, with the jaws on its hips, and
carrying the whole trail slid it tail first like a stick), the mouse jumps 0.025u up and 0.03u aside in 0.3 s the same
way (drawn lifted by `Light.Lift` x height, shadow left on the ground and slid down and right by the
height, the way petals are), and the jaws of a miss stop three tenths short of where the nose was, and never nearer than 0.025u to
the line the body lay along (a strike from behind or beside found the body between the jaws and the nose). SWALLOW: `IPrey.Hold` puts the prey's nose at the
jaws every frame, lays its body out in front of the snake (turned toward land if the straight way is
water) and thrashes it, hard for a second; then the swallowed share grows over 3 to 5 s: those spine
points crowd together inside the mouth and are not drawn, and the legs, ears and eyes of the animal are
skipped as they go in. When all of it is in, the animal is Dead, and a BULGE (the half-width x1.33
for a lizard, x1.5 for a mouse, over a tenth of the length) travels from behind the head to the middle
of the body over 20 to 30 s and then shrinks over 25 s; it is a drawn width only, the chain is unchanged.
A fed snake is slow (x0.6), hunts no more, is much more inclined to bask on the log and only flinches
at a touch. MISS: the head is back to its width at once with a small kick of the neck, a second of
looking about, three seconds' chase at 1.3 times its cruise, and it gives up. Lizards and mice see the
snake only from ahead or the side (and not at all while basking, drinking or nibbling, until it is
0.07u away); about half of those that see it freeze (a lizard) or sit up (a mouse) rather than run, and
stay so until the snake is within 0.04u, so a hunt can find prey that does not run. The `Lizards` and
`Mice` wrappers hold the slots: an eaten animal's slot stays empty for 120 to 300 s, then a new animal
with new dials, rolled from the wrapper's own dice, comes in from the edge.

*The fallen log* (`ShorePainter.Log`) is painted once, like a rock, with the same lighting rules:
a soft shadow down and right, the body shaded across its width so it reads as round, long wavy
bark streaks, a few short cracks, a knot or two, moss toward the sun, and the cut end as a disc of
growth rings flattened by the slant, which shows only when that end points down the screen toward
us. Rim light stays under 50 alpha. A log beached across the water's edge is painted before the
water is laid back over the stones, so its wet end darkens under the water like a rock's flank; a
floating log is painted after, with a dark waterline along its lower side, because a floating
thing does not sink into the water the way a standing stone does. Where it lies, how long and
thick, which way, and every streak and knot are rolled fresh each time.

*Behaviour from watching the real thing.* Koi beat and glide, steer toward a spot they have
picked anywhere in the pond (a few headings tried either side when the bank comes close, taking
the smallest turn that clears it), and keep clear of each other.

*A mind on top of the reflexes.* The koi's steering only ever takes a point to swim toward and how
firmly to turn to it; the bank, the other fish and the "never stuck" rule have the last word. On
top of that sits a small mind (the top of `Koi.cs`): one thing at a time (a "doing": wander, rest,
investigate, follow, chase, flee), a timer for how long to keep at it, and a roll of weighted dice
when it ends. The weights are the personality: three numbers dealt once per fish, energy,
sociability and curiosity, so a curious fish's dice have more "go and look" faces and a sociable
one's more "follow" and "chase" faces. Each doing only changes what the steering is handed (a petal's
spot, a point behind a friend, the fish being chased, or a point straight away from the chaser), how
hard it swims (a chase is all beating, a rest all glide) and how deep it hangs (near the surface to
nose at something, deep to rest). Being chased interrupts anything; a dragonfly's tap interrupts a
curious fish that is not busy. A swallow skimming low is a shadow sweeping over the water, and a
fish under it flinches: a dart away and a dive, then a sulk deep down (the bolder the fish, the
closer the shadow has to pass). On top of the dials each fish keeps a notebook of how fond it is of every
other fish (`_like`, 0 to 1; `Meet`, `Befriend` and `Company` in `Koi.cs`). A line starts from how alike the
two fish's dials are, grows every frame the pair is within two body lengths and not a quarter of the pond's
depth apart (in full when one is following the other, a third as fast for merely passing) and fades toward a
small floor with a half-life of twelve minutes when they are apart. `Company` turns a liking into 0 (a
stranger) to 1 (best friends), and the mind leans on it in four places: the Follow face of the dice (up to
five times the weight, and the best-liked fish in reach is the friend, not the nearest), the length of a
Follow (ten to thirty seconds for a stranger, up to a minute or two for a friend), the pace (a follower beats
when its friend beats and sets its speed to the friend's plus a push to close the gap to its slot, which for
a friend is the stranger's, a length behind and half a length aside: the notebook build's 1.5 behind and 0.6
aside left a follower a median 2.5 lengths from its friend's middle against 1.65 before, and read as two fish,
not a pair; a firmer pull, a quicker closing push and a slot at 1.2 each moved that by a tenth, and the bumps
that end a Follow are with third fish, not the partner), and the idle goals (an idle wander is picked beside the
friend's own destination, a rest beside a resting friend and a little under it). Two fish may follow EACH
OTHER: the lower school index LEADS (`LeadsPair`: it stays in Follow, so the pairing lasts a friend's long
timer, but cruises to idle goals of its own at its own pace) and the other keeps the friend's slot behind
it. A leader, not two slots: two fish each steering for the slot behind the other met head to tail (a
third of all Follows ended in a bump), and abreast, a length apart on agreed sides, they still bumped on
every turn (17 pair bumps in ten minutes on one seed), because two slots on opposite sides agree only
while the two headings do. That mutual follow is the pond's long pairing (pairs kept together 30 s or more on every seed before
the notebook came); a "lead handover" in the first notebook build, which sent the other fish off to wander
whenever a fish chose to follow its own follower, broke every such pair within seconds and halved the long
pairings, which is what he saw ("grouping up together less"). A ring of three or more is still refused
(`CanKeepCompany`). The first liking is alike squared (not to the fourth), so most fish have someone above a
stranger from the first minute, and a liking grows at up to 1/60 a second while close (a minute or two
side by side makes a friend). No more than three keep company together (`Party`), and none of it touches
the steering reflexes, the bump or the shy, which still have the last word. Now and then a big koi teases a school of minnows (the doing `Tease`, Koi.cs): once a second, from an idle moment in the upper water with a school of minnows in view within a fifth of the screen and ahead of its nose, a fish rolls its own dice (seeded from its dials, so no other roll in the pond moves) and may run at the school for 0.8 to 1.6 seconds at a real dash (2.4 times its cruise: half as fast again as any other rush). The minnows already flee any koi head near the surface, but at a rush's pace the school was always clear before the koi arrived and only eased aside, which read as a koi swimming up to a shoal that moved away. So once the head comes within a tenth of the screen's unit of the shoal's middle (and the rush is at least a quarter second old, so the dash is seen before the burst), the koi tells the school to SCATTER (`Minnows.Scatter`, the school's `Scatter` and `Regroup` moods): for most of a second every minnow bolts flat out straight away from the head, so the shoal bursts into a ring, then they gather again at a point a tenth of a unit further from the koi (or where they were, if there is no room that way) and drift on. Then the rush ends, the speed is eased (never set) down to a cruise over about half a second, the nose swings slowly away, and it rests or wanders on. It never catches or eats, goldfish do not do it, a bump or a flinch ends it cleanly, and only one fish plays in the pond at a time (a pond-wide cooldown of ten seconds, a per-fish one of 20 to 60). Now and then a koi leaps (the doing `Leap`): from an unhurried
moment in the upper water with open water a few lengths ahead and the whole thing in view, a
burst flat out to the surface, then half a second in the air on a thrown ball's arc (height = peak
x 4p(1 - p)), still moving forward at the speed it left with, stiff as a rod, with no steering
and nothing in the water allowed to touch it. The pond's one rule for height draws it: the body is
lifted up the screen by `Light.Lift` x height and its shadow falls on the WATER down and right by
(`ShadowX`, `ShadowY`) x height, so the gap between fish and shadow opens and closes through the
jump (in the air the far shadow is right: it is what says "out of the water"; under water the
same far shadow said "floating above it", which is why the bed shadow hugs its fish). It comes
down with a big ring, a thin ring, a handful of drops on arcs of their own with a tiny ring where
each lands, the neighbours flinch as from a swallow's shadow, and it dives to get over it. In the
air it is painted after everything on the water's surface (`KoiSchool.DrawOut`, after the ripples,
the petals, the snake and a perched turtle), and it shoves no petals until it lands. That is the
whole brain: a state machine, three dials, and dice.
The machinery of it (the dice, the dials, the one-doing-at-a-time timer, the cooldowns) is the
engine's `Core/Minds/Mind.cs`, and the koi, the lizards and the snake all run on it; the recipe
for giving a new animal a mind, and the rules learned building these three, is
[MINDS.md](MINDS.md).

*Depth is real.* A koi lives on the water's plane at some depth below it, and the pond has a
bed. `Pond.Floor` says how deep the water is under every pixel: it slopes up to the bank the way
the painted water does, and it also rises to meet each rock's rim over a short slope, because a
stone standing in the water widens out under the surface like the foot of a hill. A fish may be
no deeper than nine tenths of the floor under ANY part of its body (the spine is laid, the
shallowest floor under it found, and the depth capped there and then, so it holds on every
frame), and it looks a body length and a half ahead so it rises up the slope as the bank or a
rock comes near, the way a fish follows the bed, rather than finding itself inside it. It WANTS
to be a little above the bed, never on it (three tenths of its length of water under it): a fish
that aimed for the cap itself sat exactly on the clamp's boundary, and every floor step the bed
made under it as it drifted along a rock's flank lifted it in a two pixel hop, one near-rock
frame in ten. The bed is read under a ball as wide as the fish, and a step onto a bed with no
water for the fish at its depth is refused like a step onto land, so a fish that cannot rise as
fast as the bed comes up stops at the stone's foot and turns. On the
screen, depth is drawn the way height is drawn for the petals and the dragonflies, only in
reverse: we look at the pond at a slant, so the deeper a fish is, the LOWER on the screen its
picture sits than the water above it (`Light.Sink`, a little less than `Light.Lift` because
water bends the light and a pond looks shallower than it is). A fish rising to gulp is seen to
come up the screen as it sharpens. Where the bed shows through (the shallows, by the painter's
own rule for how much bed shows at a depth), the fish throws a shadow on it: the same body
painted dark and blurred, down and to the right by the gap between the fish and the bed, the
blurrier the bigger that gap, under the fish. Fish at very different depths may pass right over
each other, and a fish deep down is out of reach of the minnows and the snake (they only see fish
shallower than 0.06u).

*How deep the pond really is.* The full depth (`Pond.DepthU`) is 0.4u, deep enough that the middle
of the pond can hide a fish. A fish may be at any depth the bed allows, and three numbers in
`Light.cs` turn depth into a picture. `EyeU` (0.35u) is how high the eye is above the water: a thing
under the surface is drawn smaller the further it is from the eye (`Light.Scale`, about six tenths
of its size at the limit of sight), the way a coin at the bottom of a pool looks smaller than the
same coin in your hand. `Light.Murk` mixes it toward the water's own colour, a little at the top
and more and more with depth. `SeeU` (0.84u; 0.26, 0.30, 0.36, 0.44 then 0.70 in earlier rounds,
raised each time the owner asked to see more of the deep, the last time "again. make the water
more transparent", and once more with the pond itself) is the depth where the murk would reach
all the way; it is past the pond's full depth (`Pond.DepthU`, 0.54u since round thirty-five, "i
need a tiny bit more depth"; 0.48u that morning, 0.40u before), so a fish on the bed keeps
four tenths of its own colour. But its OWN colour changes with depth too (`Body.Deepen`, round
thirty-five, "when a creature swims deeper it gets darker, less color vibrant"): before the
water's colour is mixed over it, each pixel is pulled toward its own grey and darkened by how deep
it is (the murk is the dial: nothing at the surface, four tenths of its saturation and six tenths
of its brightness left at the limit of sight), the way water eats the warm end of the light first,
so an orange koi on the bed is a dull brownish grey where the murk mix alone had left it a bright
orange half-veiled in teal (worked through the painter's own sums: (121, 104, 65) before,
(82, 94, 75) after; a white koi 154 to 94 bright). The bed is UNEVEN too (the same day, "make the depths be uneven, like the pondbed has
deeper and shallower spots"): seven broad soft bumps, humps and hollows by turns, a fifth to a
third of u across, scale the depth by up to 18 percent either way, more the further from the
bank, in `PourWater`; and because everything reads depth from that one array (the water's colour,
the bed's clearness, the floor the swimmers feel, the sink and the haze), a hump is a real rise of
the bed to every fish, which climbs it as it climbs a stone's flank. The bumps roll their own dice,
seeded from the screen's size, so nothing else in the scene moved when they came. Deeper also
means lower on the screen (`Light.SinkPx`, as above). So a fish that dives goes smaller, hazier
and lower. The painted bed shows through the deepest water by two fifths (`PondPainter.BedClear`,
one curve for the pebbles and for a submerged stone), and the deep bed has small dark stones
lying in its silt (`Pebbles`, rolled by their own dice so the scene's hold), so the deep is a
place and not a flat colour. (Measured in a box of deep water, the earlier steps of that curve,
a twentieth to a twelfth, had moved the picture by about two grey levels: the bed's green-tinted
colour is close to the deep water's own, and the deep bed was silt alone, with nothing to see
through the water to. What the owner had been seeing change was the fish. Four asks in four
days; the lesson is to measure a step in the pixels before doubling it.) The koi go down there:
now and then a koi FORAGES (`Doing.Forage`), diving to the bed itself in the middle of the pond
(a twelfth of its length of water under it while it noses, a fifth between spots; three tenths
elsewhere) and cruising slowly from one deep spot to the next, pausing at each with its nose in
the silt; a puff of silt rises where it roots (`KoiSchool.DrawSilt`, a soft disc of the bed's
pale colour seen through the murk, drawn under every fish), billows out and settles over a few
seconds. The curious and the hungry forage more, and twice as readily since 2026-10-26 night
(on five-minute runs every fish on one seed and ten of thirteen on another went down, about a
sixth of their time); a goldfish half as often, and it may go to 0.8 of the pond now. Two koi rests
in five are taken ON the bed (0.85 to 1.0 of the pond, a tenth of a length of water under it) and a
forage lasts a minute or so with longer swims along the bed between its pauses (2026-10-27, "make
fish be willing to hang out and swim along the bottom sometimes"). And a JAM has a way out at last
(`Koi.BackOff`): a step refused for a second and a half in a row (a rest spot or a look behind a
formation held a koi nose-on against its collar for sixteen seconds, every step refused by a bed
it could not rise over, "fish still do some weird stuff around the rocks") ends the doing with a
three-second shy toward the deepest water two lengths out, the fish may rise at a slow set rate
while refused whatever the slide rule says, and for half a minute no goal, rest spot or look is
chosen within 0.3u of the spot (the first fix chose the same rock to look at and jammed again).
And a koi KEEPS OFF A STONE'S SHOULDERS (round thirty-six, "fish still arent handling the rock
well"): a koi could swim anywhere with any water at all under it, so it foraged, looked and crawled
over the pale shoulder of the island or a formation right up to the cap's collar and hung there at
the surface, bright, for seconds at a time, which to the eye is a fish lying on the rock (79 and 92
standstills of 1.5 s or more at the stones in five minutes on two seeds, most over a bed under half
depth, a forage and a slow wander among them, not only rests). `Pond.StoneShoal` marks every pixel of
a stone's hill with under 0.45 of the pond's depth of water over it, and the koi's `OnLand` and
`InWater` read it, so the step rule, the feelers, a push and every goal pick treat the shoulder as a
wall at once; the bank's own shallows are not in it (the prawns live there). The first run of that
wall found the oldest deadlock in a new place: the step is refused at the fish's CENTRE, the feelers
look ahead from the NOSE half a length on, so a fish skimming the shoal's edge at a shallow angle with
its nose clear was refused every frame and never asked to turn (20 s against the formation, five
shies, its heading within a tenth of a radian). Now a refused step probes round the centre with the
step's own tests for the nearest clear heading (`Koi.WayOut`) and the next frame turns to it hard.

*A fish has a height, and the shore is where the water is too shallow for it (round thirty-eight,* "the land
gradually rises underwater until it sticks out of the water and becomes the shore ... if fish swim close to the
shore, they have to swim up to follow the slope. if they get in too close their back fin will stick out of the
water and they'll make v shaped waves ... eventually they cant get any closer"*).* A carp's body is about 0.28
of its length deep (a wakin 0.36: `Koi._height`), and its spine runs through the middle of that, so the back is
half a height above the spine and the belly half a height below it. Three rules follow from one number. The
spine can be no deeper than the bed less six tenths of a height (`CapOf`: the belly keeps a tenth of clearance;
where that is more than the old nine tenths of the bed the old rule stays), in the easing of Update, in the
clamp of LaySpine and in the feelers' room (`RoomAround`). Update's cap reads the bed a length and a half
AHEAD as well as under the body (so a fish rises before a slope it could not climb at its speed), and that
look-ahead may now pull the cap down by at most three tenths of the depth below the bed under the body: the
shore's cap is nought, and unbounded it held a fish a length and a half from the bank at the surface with its
back fully out over 0.3 of the depth of water (the first visit sheet). Water under three quarters of a height is the SHORE
(`OnShore`): a step onto it is refused (cause 4 in the log) and the feelers call it no room, so a fish turns
before it, as it does before a rock. And where the bed lifts a spine point within half a height of the surface
its back is OUT of the water by that share (`_outAt` per point, `_out` the most of any): that part takes none of
the haze (the haze is what says "under"), a dorsal ridge is drawn along the backbone from a third of the way
back to the tail's base (dark, with a wet gleam beside it on the sun's side, both fading with the share out), and
the bow wave (`Ripples.Wake`) is up to two and a half times as strong: a fish ploughing the shallows. The bed's
slope to the bank widened for this from 0.24u to 0.36u (`PourWater`), so the back is out over the last few
hundredths of u of the approach and not the last few pixels; the water's colour, the bed's clearness, the
prawns' and the crayfish's shallows and the tadpoles' shore haunt all read the same depth, so they widened with
it (the same-scene gate shows the band and nothing else). The turtle keeps the same shore: a step onto a bed
under half its shell's height is refused. The minnows, tadpoles, frogs, snake and ducks already live at the
surface or climb out, and the bed animals walk the bottom, so nothing else needed a rule. The koi log's
`out=` token carries the share out and the bed under the body; `shore38.py` reads fish-seconds with the back
out, the shore refusals and the standstills over the shallows. The shore is in the refused step's own probe
too (`WayOut`), since a koi stood refused by it for 42 s in a shy on the first run (EP-166's trap once more:
a wall class added to the step test and not to the probe). And since the fish kept a length off the bank of
their own accord (no back but a leap's broke the surface in ten natural minutes), a koi now VISITS the
shallows (`Doing.Shallows`, a small face of the dice, likelier for a curious fish; not a goldfish, whose
"water about its height deep" lies within a few pixels of the land): a goal on the bank's slope where the
floor is 0.85 to 1.4 of its height (`NewGoal(shallow)`: that band is a few pixels wide, so the school lists
the shallows' spots once, every sixth pixel of water in view over the bank's slope off the stones' shoals,
`Koi.ShallowSpots`, and a visit draws from the list by its own height, within 0.9u first; a random point of
the screen found the band one try in sixty, and a walk downhill from a random point of the slope found it
on one seed and not the other, so most visits swam to a STALE goal, the last wander's, and "arrived" out
in the pond: a picker that fails must say so, not leave the old goal standing), a firm pull up the slope with the
feelers reading the bank as a wall all the way, the bed's cap lifting it and its back breaking the surface
over the last stretch, a hold of 3 to 6 s nosing there, then a wander off down the slope.

And the open pond's bed is no longer one depth ("make sure that the depths of the water are not the same
everywhere"): the humps and hollows of round thirty-three were laid on a bed already at the full depth and
clamped there, so half the water sat at exactly 255 in the floor mask and only the hollows showed. The open
pond is now 0.88 of the full depth where no bump is, a hump up to the full depth and a hollow down to 0.76,
with eleven bumps instead of seven, and `Pond.DepthU` rose from 0.54u to 0.6u so the typical bed is as deep
as it was (0.53u) with real humps (0.6u) and hollows (0.46u) either side. The three constants that were in
depths were re-expressed in u so they stayed put: a stone's shoal (under 0.243u of water), the deep bed's
floor (0.33u) and the minnows' dip floor (0.35u).

And the deep is a shade clearer and the fish in it dimmer (the same line: "when fish swim deeper, i want them
to look a little dimmer, i want the water a tiny bit more translucent at depth"): `Light.SeeU` 0.84 to 0.92
(the haze at the pond's full depth 0.62 to 0.57) and `Body.Deepen`'s darkening 100 to 140 (0.45 of the
brightness at the limit of sight, six tenths before). In the formula a white koi on the bed goes from 180 to
168 in its own colour and from 96 to 98 painted through the water: dimmer in itself, a shade less veiled.

*Three-dimensional steering and bumping.* The fish steer in all three directions with the engine's
steering kit (`Core/Minds/Space.cs`). Each animal keeps a `Body.Shape`, a `Chain`: a string of
beads along its spine, each with a radius, in ground pixels with z the depth in pixels. The fish
feels ahead with feelers, asking `Koi.RoomAround` "how much room is there here?": it counts land,
the bed, the surface, the room grid and every other body in the water (koi and turtles). A gap up
or down counts two and a half times a gap sideways, because swimming over another fish's back
needs less clearance than swimming beside a bank. The bed counts the same way: the water left
under a point is room (stretched), so a rock's flank or the bank's slope rising ahead shrinks the
room along a level feeler BEFORE any sample is inside it, and the fish turns or climbs early.
(Counted only as "inside or not", the feelers saw nothing until the fish was on the bed, and the
bed's clamp did the steering in hops.) When something is in the way the feelers pick the
smallest change of course: turn, climb over or dive under. When EVERY feeler is blocked (its nose
in a pocket between a rock and the bank; the search reaches a right angle either side and the way
out is behind it) it turns toward more open water by the room grid, as the turtle does; before
that fallback a koi stood nose against a rock for forty seconds. Feelers can miss, so as a last resort
`KoiSchool.Update` finds any two bodies that overlap (`Chain.Touch`) and pushes them apart as a
recoil, no faster than a body length a second, with a sideways push if the bed or the surface
refuses an up or down one. A turtle is the heavier and is never moved, and neither is the snake
while it swims (it is in the same list, so a koi steers under or round it and is pushed down and out
of it if they meet; the snake answers the touch its own way, by startling). Both fish are told, and one
not in a chase shies away for a second or two (the doing `Shy`). A rise or a sink is held to a share
of the fish's forward motion, by where its nose points on the screen (half with the nose across the
screen, none with the nose straight against the way the picture would move), so it is never seen
sliding backward or sideways. The recipe for giving another animal the same is
[MINDS.md](MINDS.md), section 8.

*A fish that tilts.* Depth is not one number for the whole fish. The trail the spine is laid along
remembers how deep the head was at each spot (`_trailZ` beside `_trail`), so each spine point is
as deep as the head was when it passed there, and `LaySpine` walks the trail in three dimensions.
A fish that dives is laid down its own slope: head deepest, tail still up near where the dive
began, and the body covers less ground than its length (foreshortened, the way a pencil pointed at
you looks short). But the body is STIFF up and down: a carp bends sideways and pitches as one rod,
so the depth along the body is a straight line from the head's depth to the depth the trail gives
the base of the tail. Laid point by point from the trail it carried the head's whole history of
depth, and a fish that had dived and then levelled was drawn with a hump (head level, middle on
the slope, tail level), which on the screen is a vertical S through the body: the review traced
95% of the owner's "worm" frames to it. Everything that goes by depth then goes point by point: the perspective shrink
(`Light.Scale`), the sink down the screen (`Light.SinkPx`), the murk (`Body.Paint` takes a murk per
spine point, `murkAt`, and slopes between them), the blur and the dimming of the markings (`Colour`
reads them by s). So a diving fish is smaller, hazier and lower at the head than at the tail, and
goes out of sight head first, the tail last; a rising one comes back the other way round. Two
rules keep it honest. A fish swims along its own length, so only a change of depth it can SWIM
(no steeper than 0.8 of its forward motion, the dodge's slope) is laid down its course; any change
beyond that is drifted, level, the whole body together (the trail's depths all move), the way a
resting koi settles. Without that a resting fish sinking in place was laid nose down at sixty
degrees. And a fish that is hardly swimming levels out over a second or two (the trail's depths
ease toward the head's), since a koi hanging in the water hangs level; one hung nose down, half
out of sight, for eight seconds before that rule. Its `Shape` chain carries the same depths, so
two fish meet where their tilted bodies really are. A fish none of whose points is in sight
(`Koi.Visible` false) is left out of `KoiSchool.Draw`'s order altogether: it painted nothing, but
still made a layer for the cores to wait on and was asked for shadows.

*Three small things a koi does that the eye expects.* Hovering (a rest, nosing at something), the
side fins are held out and scull BY TURNS, one sweeping back as the other comes forward, the slow
treading that holds a koi in place; swimming, the pair beat together. The sun gleams along the
rounded back in one soft warm band (`_gleam256`, `_gleamV` in `Koi.Draw` and `Colour`): down the
backbone when the fish heads toward or away from the sun, out on the sunward flank when it swims
across the light, so the gleam slides across its back as it turns; sharpest and brightest on a
metallic koi, fading with depth. And nothing passes through a floating petal: `Petals.PushAside`
takes a body's drawn spine and half widths and shoves every petal whose edge is inside the body's
edge straight out until it clears, with a little speed that way that dies over a second (the
petal coasts off and slows, as one on water does); against the bank, where straight out is land,
it slides along the body instead. The snake and a paddling turtle shove with their whole body; a
koi only with the pieces of it whose back is up at the surface (shallower than about its own half
width), the rest of it swims under the petals, which are painted over it. A petal pinned between a
body and the bank with no water either way stays put and is overrun: a few frames in ten thousand.

*What makes "over" read as over, not through.* Seen from above, two fish crossing at different
depths are two hazy pictures overlapping, and the eye reads that as one swimming through the
other unless the picture carries the cues a real pond does. Four of them, all in the painter:
the deeper fish is drawn a little SMALLER (we look from a few body lengths up, so the bed is
further off than the surface: `KoiSchool.Shrink`, 22% at full depth, and a fish rising to gulp
grows as it comes up); its markings are BLURRED and the whole fish DIMMER by depth (a second
skin sheet, blurred once when the fish is made, mixed in by depth); its haze mixes it toward
the WATER'S colour (the backdrop under it), never toward whatever is already on the frame
there, because the haze is the water above the fish, and mixing toward the frame made an upper
fish two thirds made of the lower one; and the shallower fish throws its SHADOW onto the back
of the deeper one (`Body.CastShadow`), a soft dark echo of itself just down and right of where
it is drawn, softer the bigger the depth between them. The first three cues are what carry the
read (the review's own renders showed a crisp fish over a soft, dim, water-coloured one reads as
over with the shadow switched off); the echo ties the two together where they overlap. It is
held to about a body width from the fish that casts it: thrown the whole way the sun would throw
it, a shadow for a deep crossing landed two body widths off, on a fish nothing was over, and read
as a blotch. The school draws each animal with a stencil of the pixels it painted (fins too),
throws every shallower overlapping animal's shadow onto that stencil only, and wipes it; animals
drawn at the same time on different cores have boxes that do not touch, so each works its own
patch. The bed shadow goes by the painter's depth or the floor, whichever is shallower, so a fish
pushed up over the island's shoulder throws its shadow on the stone. And the bed shadow STAYS
UNDER THE FISH: it is held within 0.012u of its fish whatever the gap to the bed, and goes faint
and soft as that gap grows. Thrown by the true gap (as it first was), a fish half the pond's
depth above a bed it could still be seen on had a bold crisp shadow a body length away, a second
dark fish shape, and the gap between a thing and its shadow is this pond's own cue for HEIGHT (a
petal in the air, a turtle up on a rock), so the owner read it as the fish floating above the
water. A fish right down on a clear bed still throws the bold close shadow. The koi and the turtles are drawn in one depth order, so a
turtle (usually near the top) is painted over the koi below it and under a koi that is gulping
above it; the bed shadows are painted first, under every fish, and fade to nothing in a branch's
shade. (Fading to NOTHING had a trap in it until round thirty-six: `Body.Paint` tells a shadow from
a body by the shadow's strength in 256ths being more than nothing, so a strength under a 256th, on
its way to zero under the blossom, painted the BODY, in full colour and crisp with no water over it,
at the shadow's offset: a second bright koi a few pixels down and right of the hazed one for a frame
or two, "under the branches ... the animals will flash brightly ... and then glitch". Found on frame
40 of seed 7 by cropping every deep fish at frames 39, 40 and 41: one fish changed, and its logged
spine, overlaid, sat above the bright copy by the shadow's offset. `PaintShadow` now paints nothing
when the strength rounds to nothing, for every animal's shadow.) The turtle reads the same floor and sinks on the screen by the same rule;
when it dives it follows the same rules as a koi (smaller, hazier, lower, and out of sight past `Light.SeeU`). The floating log is a wall to the koi's
steering, not something they swim under: what you may see is a deep fish's sunk picture slipping
under the log's edge, which is what a log floating over a fish looks like.

*The island rock is a hill with a dry top.* One big stone is placed out in the open water
(further from the bank than any lily pad cluster reaches, clear of the log, and not under the
blossom: the branches' shadow map is made before the island is placed and the spot must be open
sky). The stone is painted as any rock is, whole and bare (no moss: green survived the water as a
lime glow), the cap on top of it as a small rock of its own with faces, creases, grain and moss, and
THEN the water is laid back over both (`WrapIsland`). The cap keeps a wet band just inside its own
rim, the waterline every bank rock has, and no shadow on the water. The stone is ONE OBJECT with
its cap, in depth as well as in paint (2026-10-26 night, after the owner watched a turtle "swim
under the top, but over the bottom" and said "the top of the rock is just floating in the water"
and "making the rock one object might be the way to go"). Its depth at every pixel is the hill
the bed climbs under the swimmers (`Hill`): at the surface on a COLLAR round the cap (half a cap
radius wide, read from the cap's painted rim so it follows the lumps), then down to full depth a
little outside the stone. Nothing swims between the cap and its collar, because there is no water
there. The stone is painted through that depth: mixed toward the water around it by the bed's own
curve (`BedClear`) with a faster fade of its own below (gone by half the pond's depth, where the
painting ends), green-tinted, and darkening fast down the flank (red most, blue least), so the
collar is nearly as bright as the cap and the flank is a dark slope that is darker than the
water, which is what a boulder under murky water looks like; and every point of it is DRAWN SUNK,
down the screen by its depth exactly as a fish at that depth is (`Light.SinkPx`), the ground rows
stretched to fill and the nearest point winning at each screen pixel, so the far side of the
boulder slides in under the cap and the near flank trails below its place on the ground. A deep
forager, sunk a fifth of u, is therefore never painted across a flank that stands above it, and
a swimmer deeper than a quarter of the pond is drawn through a stencil that leaves out the
stones' collars and caps (`Pond.StoneTop`, the scene's deep water stencil), so the stone hides
what is behind it. (Three renders on the way: the stone through the bed's curve alone was a pale
slab lighter than the deep water; mixed toward the pond's colour for water of the mound's own
depth it was the pale green of the shallows, a ghost; a box of 1.5 radii left the formation's
drop shadow on the water as a dark wedge.) Along the cap's rim a dark line: the surface meeting
the stone.
The cap alone goes into the blockers: a wall to the koi, a thing for a turtle to haul out onto
(the turtle reads the cap's painted shape from the blockers, not a circle). `Pond.Floor` climbs the
stone's shoulders from full depth a little outside it to nothing at the collar, so a fish
crossing it rises up and over and goes round the top, with its shadow on the stone. `Pond.Island`
holds the stone and its cap; `Pond.IslandPerch` and `Pond.LogPerch` say who is sitting on them.

*The small rock formation (2026-10-23).* The owner's word: "remove the log entirely and instead give
me a very small island, or rock formation". The log is now a setting ("Fallen log", off by default;
the code stays), and in its place `ShorePainter.PlaceFormation` puts one small stone toward the lower
left of the pond (of several hundred random spots in the lower left half that are open water a fifth
of u from the bank, under open sky and clear of the island, the one nearest the lower left corner),
with two or three little caps evenly round its middle so no two touch. It is painted with the island's
own three steps (`IslandStone`, `IslandCap` once per cap, `WrapIsland` over the union of the caps,
its depth read from the NEAREST cap's rim, so the stone between two caps stays shallow and in sight,
one stone and not a dark cloud per cap), each cap goes into the blockers and into `Pond.Rocks` (so every
animal's obstacle list has it without a new list), and the bed climbs to each cap's rim (`Hill`, once
per cap; the lowest hill wins between them). `Pond.Formation` holds it. Placement is counted, never
assumed: the start log prints it, 12 of 12 seeds at 1920x1080. A SECOND formation (2026-10-27, "i
like the rocks we have. add one more") is placed the same way in the upper right half, nearest the
upper right corner, clear of the island and the first; it rolls its own dice (seeded from the first
one's place) so every roll after it is what it was, and `Pond.Formation2` holds it. The stone wraps
run their three distance maps inside the stone's box now, not over the whole frame (three stones,
nine whole-frame sweeps, had cost a second and a half of the start).

*The lawn is finished every other frame.* The carpet of blades and the tufts are the two dearest
things in the whole pond to draw, and grass moves slowly (a gust takes seconds to cross the
lawn). So the carpet is laid on even frames and the tufts are stamped on odd frames, each onto a
little picture the size of the lawn's box (the "sheet"), and every frame the finished sheet is
copied onto the screen. Each frame pays for one of the two jobs plus a cheap copy instead of both.
What the eye reads as a stutter is the slowest frame, not the average, and this halves the lawn's
share of it. The koi's body painter and the turned pictures of the dragonflies and swallows were
tightened in the same pass (whole-number blending, no edge tests inside the picture, a box that
is the turned rectangle rather than the circle round it), and a pixel-by-pixel comparison against
the previous build at the same seed proved those changes alter nothing but rounding (a difference
of at most 2 in 255). Dragonflies hover, dart and stop
dead, perch, and dip. Swallows sweep in long curves and skim the water to drink. A creature that
cannot be made right in all four ways (really visible there, drawn in this view's perspective,
turning by bending or turning as a whole, moving the way that animal moves) is left out: that
rule came from Cotswold Brook's trout.

A running river (`FlowingWater.cs`, in the engine for any photo with one) works differently from
the brook. In real rapids the big shapes hold still: the rocks, and the white water piled up
behind each rock, are made by the riverbed. What travels is the fine texture on top. So the photo
is split: a slightly blurred copy keeps the big shapes and stays put, and only the difference
(the flecks and wrinkles) is carried downstream along the channel, fastest in the middle, with
rocks left out entirely. It is carried on a "flow map": two copies of the texture slide
downstream half a cycle apart, each faded out as it resets, a trick games use for rivers.

## 9. High-DPI screens

A 4K laptop screen usually runs at 200% to 250% "scaling", so text isn't microscopic. A program
that doesn't declare itself "DPI aware" gets rendered small and then stretched by Windows, which
looks blurry. Each screensaver's `.csproj` sets `ApplicationHighDpiMode` to `PerMonitorV2`, which
tells Windows "I handle real pixels myself." Everything then draws at the screen's true
resolution, and sizes scale with screen height, so a 4K monitor gets things twice as many pixels
tall as a 1080p one.

The settings dialog does the same for its buttons and sliders: its layout is written for 100%
scaling (96 DPI, "dots per inch") and WinForms multiplies every position by the real scaling.

## 10. Where things live on your PC

| What                          | Where                                                         |
|-------------------------------|---------------------------------------------------------------|
| The installed screensavers    | `C:\Windows\System32\<Name>.scr`, for example `MatrixRain.scr`, `Sakura.scr`, `Halloween.scr` |
| Your settings                 | Registry: `HKEY_CURRENT_USER\Software\Peckworks\Screensavers\<name>` (open with `regedit`) |
| Which screensaver is selected | Registry: `HKEY_CURRENT_USER\Control Panel\Desktop`, value `SCRNSAVE.EXE` |

When Windows starts a screensaver for real, it runs it on a separate private "desktop" that
other programs can't see or screenshot. That's also why no taskbar shows over it.

The Screen Saver Settings dialog reads the `.scr` files in System32 **once, when it opens**. If
it was already open when you installed a new screensaver, it won't list it until you close and
reopen it. `install.ps1` does that for you and then checks the list.

## 11. Making the next screensaver

1. Copy the `src/Sakura` folder (or `src/SakuraDusk`) and rename the folder, the `.csproj`,
   and the names inside it.
2. Replace the scene with your own class that implements `IScreensaverScene`
   (`Update` + `Render`).
3. Declare your knobs in a settings class that inherits `ScreensaverSettings`
   (`Add("Speed", ...)` once per knob). The settings dialog builds a slider for each one.
4. Update the definition class in `Program.cs` (name, scene, dialog colors).
5. Add the project to the solution file (`PeckworksScreensavers.slnx`) next to the others, so
   `dotnet build PeckworksScreensavers.slnx` compiles it and a change to the shared engine that
   breaks it shows up right away.
6. Want falling petals over your own painting? Build a `PetalField` and call its `Update` and
   `Draw`, exactly as `SakuraDuskScene.cs` does. Set its `Shape` and `Colors` for leaves or snow.
7. Want something that flies or glows? Paint it once with `Sprite.Paint` (or `Sprite.Glow`) and
   stamp it each frame, as `HalloweenScene.cs` does for its bats.
8. Want small surprises that come and go? Follow [ADDING_HAPPENINGS.md](ADDING_HAPPENINGS.md).
9. Want an animal that decides for itself what to do next? Build its mind on `Core/Minds` and
   follow [MINDS.md](MINDS.md): dials, doings, weighted dice, reflexes that keep the last word.

`scripts/publish.ps1` and `install.ps1` find every screensaver folder under `src\` on their own.
Everything about being a screensaver (monitors, preview box, wake-up rule, fast drawing, glow,
saved settings, the settings dialog) comes from the engine for free.

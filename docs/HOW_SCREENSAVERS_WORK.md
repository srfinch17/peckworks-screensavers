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
| `/snapshot out.png [w h seconds]`     | Renders with no window at all, saves a PNG, and writes the time per frame to `out.png.txt` |

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
    Happenings.cs                  small random events: the base class, and the director that deals them out
    Sakura/PetalField.cs           the falling petals (or leaves, or snow), shared by every painted scene
    Sakura/Brushwork.cs            banks, hills, trunks, blossoms, a pine, and self-growing branches
    Sakura/Boat.cs                 the little boat that crosses the water in both sakura scenes
    ChimneySmoke.cs                puffs that rise, cool, spread and thin (Cabin by Stream, Cotswold Brook)
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

  CotswoldBrook/                   a real photo of cottages by a brook, brought to life
    Program.cs                     Main(), plus the definition
    CotswoldBrookScene.cs          what this photo holds: where its water, chimney and windows are
    Trout.cs                       trout just under the surface, holding and darting
    CotswoldBrookSettings.cs       declares its knobs
    brook.jpg                      the photo, packed inside the .scr
    brook_depth.png                its depth map (white = near), made once by scripts/depthmap.py
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

**Cotswold Brook** takes the next step: no painting at all. However much texture and light the
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

`scripts/publish.ps1` and `install.ps1` find every screensaver folder under `src\` on their own.
Everything about being a screensaver (monitors, preview box, wake-up rule, fast drawing, glow,
saved settings, the settings dialog) comes from the engine for free.

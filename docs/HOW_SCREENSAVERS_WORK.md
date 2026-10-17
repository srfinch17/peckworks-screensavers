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
| `/snapshot out.png [w h seconds]`     | Renders with no window at all, saves a PNG, and writes the time per frame to `out.png.txt` (average, worst second, slowest frame, garbage collections); with `PECKWORKS_SECTIONS` set to a file, also what each part of the frame cost |

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
    Sakura/Plants.cs               one Wind for a whole scene; plants as flip-books (tufts, cattails, irises); a Lawn whose carpet slides in the gusts
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
    Shore.cs                       paints the corner of land: the lawn's ground and carpet, the sand edge, rocks that look like rocks, the fallen log
    Body.cs                        draws an animal (koi, snake, turtle) along its spine
    Koi.cs                         the koi: swimming, steering, beat and glide, varieties
    Minnows.cs                     schools of tiny fish that move as one, and rush to the dragonfly's rings
    Turtle.cs                      the turtle: paddling, floating, diving, hauling out to bask
    Frog.cs                        the frogs: sitting on pads, rocks and the bank, leaping, kicking, floating, diving, climbing out
    Petals.cs                      petals falling from the blossom, landing, floating
    Ripples.cs                     rings spreading on the water
    Dragonflies.cs                 hover, dart, perch, dip, and the game with the minnows
    Swallows.cs                    swooping visits to drink
    Snake.cs                       a snake swimming across, now and then, feeling its way round things
    Lizard.cs                      a lizard dashing about the bank and basking on the rocks, now and then
    Mouse.cs                       a field mouse scurrying between cover in the grass, hiding from the snake and lizards
    TurningStamp.cs                a picture stamped at any angle (dragonflies, swallows, their shadows)
    Light.cs                       where the sun is and how we look at the pond
    SakuraPondSettings.cs          declares its knobs

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
"Draw width" setting (2048 by default) is the width it draws at on a wider screen, and Windows
enlarges the picture; on the owner's 4K laptop 2048 holds 60 frames a second where 2560 held an
even 30.

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
grows as the cube of the distance along the body and only two thirds of a wavelength fits on
it; a wave that grew as the square with nearly a whole wavelength threw the body into an S with a
second bend at the tail, "a super wiggle like a worm"); and the head's rate of turn is eased
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
is one number, the lean: a slow swell that comes and goes over half a minute, times two waves
travelling downwind, with the long wave's crests sharpened so the lawn is mostly calm with a band
of bent grass rolling through it. The lawn shows only where no branch is in front of it, and the
branches' dappled shadow darkens the moving grass as it darkens the ground. Taller tufts stand in
the carpet, each a flip-book (`Flipbook`) of 24 pages from upright to "flat downwind"; the blades of a page are
worked out in three dimensions (the bend angle grows from root to tip, each short step going a
little less up and a little more along the ground) and projected like everything else in an
overhead scene (`Overhead`: squash, lift, and where a shadow falls): height lifts a point up the
screen, and throws its shadow down and to the right. A tuft stamps the page nearest its lean. The
cattails and the iris clumps are the same machinery (`Clumps`): a few plants that each stand in
one place, one flip-book each, stiffer than grass. The lean itself comes from a coarse grid worked
out once a frame (a sine per pixel would cost more than the drawing), and the tufts are drawn in
horizontal bands on several cores, even bands then odd, since a tuft can only ever touch tufts in
its own band or the next. All of this lives in the engine for the next scene seen from above with
a meadow or a reed bed: it paints its carpet and its ground once, then makes a `Wind`, a `Lawn`
and a `Clumps`, and nothing of its own that moves. Two rules travel with them: the lawn is drawn
first each frame (half its rows are kept from the frame before), and the projection is overhead
(a side-on scene needs a different one).

*Rocks that look like rocks.* A rock is a lumpy ring of 9 to 13 points (two slow waves of radius
plus jitter), a touch wider than tall because we look down on a rounded lump at a slant. It is
painted in the order light builds a stone: a soft shadow on the ground to its lower right, a pale
rim on the water round its wet foot, the body with a gentle rounding toward the sun, then a lit
FACE (the rim points on the sunny side joined by a ragged inner edge, filled three times at three
sizes so its edge is a soft crease) and a dark face on the far side, short crack lines from the
rim part of the way in, a fine speckle of grain, moss and a spot of lichen on top, a light edge
where the rim faces the sun and a dark edge where it faces away. Stones are not one grey: warm,
cool, greenish, light and dark. Then the WATER IS LAID BACK OVER the stones: a rock in a pond is
not pasted on top of the water, its flanks slope into it. For every pixel of a rock on the water
side of the bank, a band just inside the rock's own rim is mixed toward the water darkened by a
third (the wet flank, and the dark line where the surface meets it), and a stone standing out in
the shallows takes on some of the water's colour all over, up to about half, its top still
breaking the surface. The waterline so runs round each stone, not straight across it.

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

*Rigid animals turn as a whole.* A dragonfly or a swallow does not bend, so it is a picture
painted once (several, for the wingbeat) and stamped at any angle every frame (`TurningStamp.cs`),
reading the picture smoothly between its pixels so a slow turn never steps. Never flipped. The
turtle is rigid too, but it lives in the water with the koi, so it uses the same spine drawing
as the koi with a STRAIGHT spine that turns as one piece: that way it gets the same haze with
depth, the same branch shadows and the same squash for free. Its legs are four small ovals
swinging from under the shell's rim, the diagonal pairs in step, as a pond turtle paddles. The
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
Approach, Climb, Bask, Slip) and weighted dice in `Decide()`. A haul-out is PLANNED first (a
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
of the shell drifts sideways and it reads as going tail first.

*The frogs sit, leap and kick.* A frog (`Frog.cs`, a `Frogs` holder like `Turtles`) is rigid, so
it has a STRAIGHT spine like the turtle's (the same `Body` drawing: haze by depth, branch shadows,
squash), with a few small ovals on top: two eyes with a pale rim, four legs, a pale throat. It
never bends: it turns by hopping round on the spot, or, before a leap, during the crouch. Every
quick motion is a HOP, a script from its start to its end: a crouch (legs folded, the body swinging
round to face the way it will go), then a flight at an even speed along the ground with the height a
parabola, so it lands where its shadow is. Height is a lift up the screen and a shadow down and
right (`Light.cs`, the same rule as the petals), and the shadow is painted on the pad or the bank
where it sits and over the water in a leap. The pose is one number, 0 (legs folded in a Z beside the
body) to 1 (stretched out behind), eased, and the hops and the swimming kick both drive it. A kick
is a quick push (the legs sweep out and back, the body surges) and a slow glide (they fold forward),
about one a second. Its mind is on the shared parts (`Core/Minds`): two dials dealt at birth (bold,
restless), a `Doing` (Sit, Snap, Leap, Swim, Float, Dive, Climb), weighted dice, a cooldown after a
fright. The PERCHES (a lily pad with bare green leaf to sit on, not its notch or a flower; a dry bank
rock; a spot on the bank's edge) are found once when the scene is made, and one frog holds one.
A swim to a perch goes first to a point out in the water in line with it and then in along the line,
so it arrives pointing at it, and it is never planned across a gap narrower than a frog. In the
water it is solid to the koi (its `Shape`, a chain of beads, goes into the scene's list of
surface bodies), it steers round the pads (a pad is a wall to a frog, which swims ON the water, and
no wall to a koi, which swims under), and a swim whose steps are refused for over a second (wedged
between pads) ends in a hop to open water. A dive is the koi's depth rule (`Light`): smaller, hazier
and sunk down the screen, so a pad can cover it. Frogs are built last in the scene, with dice of
their own, so adding them moved nothing else.

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
wriggle like worms, which the close-up strip showed and a still never would). The dragonfly's game
is the two halves meeting: a dragonfly asks where the nearest school is, flies over, hovers, skims
low tapping the water three times (three small rings), and the taps call the school; then it darts
off far and its timer runs before it plays again, taking the schools in turn.

*A lizard on land is the koi's method on the ground.* The lizard (`Lizard.cs`) is the same
bending spine, its head leading and its body and long tail laid along the trail behind it, with
a running S-wave that grows down the tail and dies away when it stops, and four legs as small
ovals swinging on opposite corners in step. What makes it a lizard rather than a small snake with
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
sideways when the doing changes.

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
closer the shadow has to pass). Now and then a koi leaps (the doing `Leap`): from an unhurried
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
and more and more with depth. `SeeU` (0.26u) is the depth where the murk reaches all the way: past
it the fish is not painted at all, it is lost in the water until it rises. Deeper also means lower
on the screen (`Light.SinkPx`, as above). So a fish that dives goes smaller, hazier, lower, and
then gone.

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
shade. The turtle reads the same floor and sinks on the screen by the same rule;
when it dives it follows the same rules as a koi (smaller, hazier, lower, and out of sight past `Light.SeeU`). The floating log is a wall to the koi's
steering, not something they swim under: what you may see is a deep fish's sunk picture slipping
under the log's edge, which is what a log floating over a fish looks like.

*The island rock is a hill with a dry top.* One big stone is placed out in the open water
(further from the bank than any lily pad cluster reaches, clear of the log, and not under the
blossom: the branches' shadow map is made before the island is placed and the spot must be open
sky). The stone is painted as any rock is, whole and bare (no moss: green survived the water as a
lime glow), the cap on top of it as a small rock of its own with faces, creases, grain and moss, and
THEN the water is laid back over both (`WrapIsland`). The cap keeps a wet band just inside its own
rim, the waterline every bank rock has, and no shadow on the water. The stone is faded by two
distances, whichever has it further gone: out from the cap's rim down the flank, and in from the
stone's own painted edge, so the fade ends exactly where the painting does on every side. Going
down, the stone gets darker (less light reaches it and less comes back) and bluer (the water takes
the warm colours first), and takes on more of the water's colour, until only water is left; it is
darker than the water everywhere but the thin wet band at the waterline, and the touch of extra
blue in the mix fades with the stone, so its edge ends as plain water with no ring. Along the
cap's rim a dark line: the surface meeting the stone.
The cap alone goes into the blockers: a wall to the koi, a thing for a turtle to haul out onto
(the turtle reads the cap's painted shape from the blockers, not a circle). `Pond.Floor` climbs the
stone's shoulders from full depth a little outside it to nothing at the cap's rim, so a fish
crossing it rises up and over and goes round the top, with its shadow on the stone. `Pond.Island`
holds the stone and its cap; `Pond.IslandPerch` and `Pond.LogPerch` say who is sitting on them.

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

# Minds: how the animals think, and how to give a new one a mind

The animals in these screensavers are not on rails. A koi decides what to do next, and so
does a lizard and the snake. The way they decide is the same for all of them, and the shared
parts live in the engine, in `src/Peckworks.Screensavers.Core/Minds/Mind.cs`, so any saver can
give its animals a mind, and anyone can improve the shared parts for all of them at once.

This page is the recipe. It is written for someone new to this, one idea at a time.

## 1. What a mind is here

Think of a very small animal brain as four things:

1. **Personality dials.** A few numbers from 0 to 1, rolled once when the animal is made: how
   bold it is, how energetic, how curious. A dial never forces anything. It only makes some
   choices more likely than others, the way a lazy person is more likely to sit down.
2. **One thing at a time.** The animal is always doing exactly one thing (we call it a
   "doing": wander, rest, hunt, bask ...), and it has a feeling for how long to keep at it
   (a timer).
3. **Weighted dice.** When the timer runs out, the animal rolls dice for what to do next. Each
   possible doing is a face on the dice, and the faces are not the same size: the dials and the
   situation set how big each one is. A curious fish's dice have a big "go and look" face.
   "Hunt" is a face only when there is something to hunt. A face of size 0 is not on the dice.
4. **Reflexes underneath, which always win.** The mind never moves the animal. It only hands
   the steering a point to go to and how keenly to go. The bank, the rocks, the other animals
   and the "never stuck" rule still decide every single step. And a few **interrupts** (a
   touch, a shadow, a tap on the water) can cut in at any moment, each with a cooldown so it
   cannot fire thirty times a second when the thing that caused it stays close.

That is the whole brain: a state machine, a few dials, and dice. It is enough for a pond that
people watch for an hour without seeing the same thing twice.

## 2. The shared parts (Core/Minds/Mind.cs)

| Part | What it is | How an animal uses it |
|------|------------|-----------------------|
| `Dice.Pick(rng, weights)` | The weighted dice. Give it one weight per face; it returns the index of the face that came up (or -1 if every face is 0). | `switch (Dice.Pick(_rng, [wHunt, wBask, wExplore, wCross]))` |
| `Traits.Roll(rng)` | One dial, 0 to 1. | The snake rolls two fresh dials for every visit. |
| `Traits.Slice(rng, k, n, reverse)` | A dial for the k-th of n animals, inside its own slice of 0 to 1, so a group never comes out as near-twins. `reverse` runs the slices the other way, to pair two dials against each other. | The lizards: the boldest is also the laziest. |
| `Traits.Deal(rng, n)` | n dials, one per slice, in a random order. | `KoiSchool` deals energy, sociability and curiosity to its eight koi. |
| `Mind<TDoing>` | One thing at a time, with a timer. `Begin(doing, seconds)`, `Tick(dt)` once a frame, `Over` when the timer has run out, `Left` for "how long still" (an animal may set it to stretch or cut a doing), `End()` to cut it short. | `_mind.Begin(Doing.Rest, 8 + 14 * roll)`; in Think: `if (_mind.Over) Decide();` |
| `Cooldown` | "Not again for a while". `Start(seconds)`, `Tick(dt)`, `Ready`. | A koi that flinched at a shadow cannot flinch again for fifteen to forty seconds. |
| `Trail` (Trail.cs) | The path the head has taken, and the walk back along it that lays a body: `Remember`, `RememberIfMoved`, `Straight`, `Shift` (move the whole body), `EaseDepths`, and `Begin(head, z, depthPx, heading)` giving a `Walk` whose `Advance(distance)` returns the point that far behind the head along the path. | The lizard lays its 16 spine points with one `Walk`; the koi's and the snake's hand-written walks are the same algorithm and are the next to move onto it. |
| `Ground` (Ground.cs) | The three walking rules for anything on a surface with an edge: `Step` (forward, or slide along the edge toward the goal when the way ahead is refused), `LineClear` (is the whole line to a goal ground?), `FleeRun` (the longest clear run away from a point). The surface is a `Solid(x, y)` question the animal answers. | The lizard's steps, roam goals, exits and flights; the mouse is built on the same three. |

A MOOD is a dial that moves: the koi's hunger (0 fed to 1 famished) rises over minutes and drops
when it eats, and loads the dice exactly as a dial does (the petal faces, the gulp timer, the
rest face). One number, read wherever a dial is read, and the same fish is a different fish an
hour on.

Each animal keeps its own `enum Doing { ... }`, its own dials, its own `Decide()` (the faces
and what loads them), its own `Begin()` (what starting each doing sets: the timer, a goal, a
depth, a partner) and its own `Think()` (what ends each doing). Those are the animal; the parts
above are only the machinery.

## 3. The recipe for a new animal

1. **Watch the real animal first.** Footage, not memory. Write down in plain words what it
   does, in the order it does it, and how it moves (a koi beats and glides; a lizard dashes and
   freezes; a turtle paddles in pairs). That paragraph becomes the class comment.
2. **Pick two or three dials.** Not more. Name what each one changes.
3. **List the doings.** Five to seven. Each is something a person could name while watching
   ("it is resting", "it is going to look at that petal").
4. **Write the dice.** For each doing, a weight: a base, plus what the dials add, times what
   the situation allows (a face is 0 when there is nothing to do it to). Put the weights in one
   array in a fixed order and switch on `Dice.Pick`. A face that is picked but turns out to have
   nothing to do (no petal in reach) falls through to a plain doing like Wander; say so in a
   comment, and make sure that path is really taken sometimes (count it).
5. **`Begin()` sets, `Goal()` hands over.** Starting a doing sets its timer and whatever the
   steering will need (a point, a pull, a depth, a partner). Every frame, one method hands the
   steering a point and a pull and nothing else. The steering is never told to ignore the bank.
6. **Interrupts with cooldowns.** Being chased, touched, shadowed. Each one is a method another
   actor calls (`Startle(x, y)`), tests a reach that the dials scale, and starts a `Cooldown`.
7. **Then watch it with instruments, not with your eyes.** See section 4. Every creature bug
   so far was found by an instrument and missed by a still picture.

## 4. Rules learned the hard way

These are in the code comments too; here they are in one place.

- **A creature that stops is the default bug.** A mind that sends an animal near scenery
  finds every weakness in the steering: a fish wedged between two rocks, a snake jammed on a
  lily pad, a lizard flickering between two doings thirty times a second. The first instrument
  for anything with a goal is the timeline: position and state every five seconds, seconds
  visible per minute, and the longest standstill WITH where it was and what it was doing (a
  forty-second rest is fine; a forty-second wander at one spot is a jam).
- **Feelers start closer than the smallest obstacle is wide**, and a step that is refused must
  itself force the avoidance turn, or the goal's pull holds the nose on the obstacle for ever.
- **Obstacles are per layer.** A lily pad is no wall to a koi under it and a wall to a snake on
  it. Hold obstacles as shapes and let each animal pick which ones count. The same goes for
  depth: a koi's depth is capped by the bed under every point of its spine and both edges of its
  body (`Pond.Floor`), which counts the rocks' underwater flanks, and it rises up the slope before
  the bank, so no fish is inside the ground or a stone; the water stencil hides the fins' soft
  edge where it still crosses a rim. And a rise must not outrun the fish: lifting a picture up the
  screen while the fish heads down the screen reads as swimming backward, so the rise is held to
  half of the forward motion.
- **A touch between animals is a point-to-spine test with a cooldown of seconds.**
- **Targets must be in view and partners must be near.** A fish nosing at a petal under the
  blossom is a fish nobody sees doing it.
- **Nose-first is measured at the neck for a bending animal** (head-to-neck direction against
  the velocity over a quarter of a second). The head-to-tail chord of an honest tight turn is
  seventy degrees off the velocity.
- **Deal the dials; do not roll them one by one.** Eight fish rolled independently gave two
  that nobody could tell apart. `Traits.Deal` spreads them out.
- **Timers are floats in seconds, phases are wrapped.** A screensaver runs for days.
- **Sizes and speeds are in u** (the screen's size unit), never "cross the screen in N seconds".

## 5. Who runs on it today

| Animal | Dials | Doings | Interrupts |
|--------|-------|--------|------------|
| Koi (`src/SakuraPond/Koi.cs`) | energy, sociability, curiosity (dealt); hunger (a mood) | Wander, Rest, Investigate, Follow, Chase, Flee, Flinch, Shy | being chased, a dragonfly's tap, a petal settling (when hungry), a swallow's shadow, a bump from another body |
| Goldfish (`Koi.cs`, `Koi.Kind.Goldfish`) | the same three dials, dealt over koi and goldfish together | the koi's, with Follow doubled toward another goldfish, no Chase at a koi, a shallower Rest | the koi's, with a flinch reach 1.5 times as far |
| Lizards (`src/SakuraPond/Lizard.cs`) | bold, energy (sliced, paired) | Hidden, Dash (Roam, Drink, Charge, Flee, Stalk), Freeze, Bask, Drink, Face | a meeting, the snake, a swallow's shadow |
| Mice (`src/SakuraPond/Mouse.cs`) | timid, greedy (sliced, paired) | Hidden, Scurry, Sniff, SitUp, Groom, Nibble, Drink | the snake's head, a lizard's nose, a swallow's shadow |
| Turtles (`src/SakuraPond/Turtle.cs`) | sunny, nerve (dealt) | Cruise, Float (and a petal nibble), Dive, Approach, Climb, Bask, Slip | the snake's head near a basking turtle, a body in its way (feelers), a touch (Nudge) |
| Snake (`src/SakuraPond/Snake.cs`) | bold, nerve (per visit) | Cross, Explore, Hunt, Bask | a touch from a koi, turtle or lizard |

The minnows' school mood (drift, rush, mill) and the dragonflies' doings are simpler clocks
and are not on the shared parts yet; they are the next candidates.

## 6. The instruments

The checks live outside the repo (a private kit) and are spliced into a COPY of the saver, never
the repo: a CSV log per frame of every animal's head, tail, state and depth, then scripts that
print the timeline, the longest standstill, nose-first, clearance to declared obstacles, the
depth cap (frames below the bed: must be 0), and a follow strip of crops centred on one animal
so the eye can judge the look last. Run three seeds for three minutes before anyone sees a new
animal; every creature bug so far was a standstill, a wrong direction or a jam that a frame
rate and a still could not show.

## 7. What is shared, what is not, and why

`Core/Minds` holds the DECIDING half of a mind: the dice, the dials, the timer and the cooldown.
Those were the same in three animals, so they are written once.

The MOVING half is now partly shared too. `Core/Minds/Space.cs` is the steering kit: `Chain` (a
bendy body's shape for touching), `Feelers.Along` (the room along a straight feeler) and
`Feelers.Best` (the smallest change of course that has enough room, turning, climbing or diving).
It knows nothing about ponds: the world is a question each animal answers (section 8 is the
recipe). What is STILL per animal, and where every creature bug so far has lived: the refused-step
rule (a step that would end inside something must itself force the avoidance turn), the touch
cooldown (without it a fish keeping pace re-triggers a bump every frame), and each animal's own
answer to "how much room is there here?", because each reads a different world: a koi asks land,
the bed, the surface, a coarse room grid and every other body in the water, and swims under pads;
the snake asks a list of circles and a capsule and goes round pads; a lizard asks a land mask and
slides along the shore. The koi and the turtle steer with the 3D kit (each with its own Room
answer); the lizard and the mouse walk with `Ground` (section 9); the snake still steers on its
own flat plane. A stranger giving a new animal a mind gets the
deciding half, the trail, the 3D steering and the walking rules for free, still writes the Room
or Solid answer and the refused-step rule, follows the rules in section 4, and should expect the
instruments in section 6 to find the first bugs there.

One rule lives in the saver, not the kit, because it is about how THIS pond is drawn:
`Light.HoldDepth` (Sakura Pond's `Light.cs`) holds any swimmer's rise or sink to what its own
forward motion can hide on the screen, since depth moves the picture up and down the screen
there. The koi and the turtle both call it; a saver that draws depth another way writes its own.
ONE EXEMPTION, on purpose: a FLOATING turtle is buoyant and rises at a fixed slow rate (0.03 of
the pond's depth a second, up to about 5 px a second at 720 lines) whichever way it faces. Held
to its own forward motion, which is nothing while it floats, it never surfaced (24 floats in 47
never breathed). The review measured the drift and judged it reads as surfacing, not sliding,
because the turtle grows and sharpens as it comes up; the koi's rule stands for everything that
swims. Two known turtle minors remain: a slip off a perch checks its landing only when the exit
is chosen, so a koi swimming into the spot during the 2 s slide is pushed out of the turtle (2 of
22 slips); and the see-through tail fin of a koi, outside the bed test and on the straight body
line, can sit below the bed on 0.03 to 0.05% of frames (the body never).

Add to `Core/Minds` only what two animals need in the same shape; a thing one animal needs stays
in that animal.

The first such piece arrived with the turtle: a `Claim`, one thing only one animal may hold (the
log, the island's cap). The snake's log bask and the turtle's log bask were written by two
authors who each believed the log was theirs, and a snake climbed onto a log a turtle was
sunning on. Now both ask `Pond.LogPerch` (and the turtles `Pond.IslandPerch`) before setting
out, take it, and release it every frame they are not using it. A `Claim` has no queue and no
timeout on purpose: an animal that forgets to release is a bug the timeline shows.

One exception to "the steering is never told to ignore the bank" (section 1, rule 4): the
turtle's Approach, once it is lined up beside its perch, swaps the bank reflex and the
"room to swim" rule for a plain "is the next step on water" test, because the perch IS a wall
to the reflexes and the turtle has to swim right up to it. It is the only place a mind quiets
a reflex, it holds within about three body lengths of the perch, and a refused step there still
ends the approach.

## 8. Moving in three dimensions (Core/Minds/Space.cs)

The idea in one picture: an animal is a string of pearls (a `Chain`), and the world is a question
it can answer ("how much room is there round this point?"). Space.cs knows no pond, no pixels and
no fish. It uses ONE unit for all three axes (Sakura Pond uses ground pixels, with z the depth in
pixels, growing downward), so the same pieces would serve birds in the air or fish in a tank.

**Step 1. Give the animal a Chain.** `new Chain(beads)`. Every frame, after the spine is laid,
fill `Points` (nose first) and `Radius` (half the body's width at each bead), then call `Seal()`
(it works out the box round the whole chain, the cheap "could these touch at all?" test). In Sakura
Pond that is `Body.Shape`, filled in `LaySpine` for every animal, koi and turtle alike. Fill it
from the spine that is DRAWN, with the sway and the perspective shrink in, not from an idealised
one, or the thing that touches and the thing you see will disagree. Give each bead its own depth
if the animal can tilt: the koi's trail remembers how deep the head was at each spot, so a bead is
as deep as the head was when it passed there, and a diving fish's chain is the tilted body, head
deep and tail still up.

**Step 2. Answer the Room question.** A method of the animal, `float Room(in Vector3 at)`, made
into a `Feelers.Room` delegate ONCE (a new delegate every frame is garbage). Return 0 for
"something is here" (land, the bed, the surface, inside another body) and otherwise the distance
to the nearest thing. Ask the cheap things first. For other animals use
`otherChain.Near(p, myRadius, roomSoFar)` (the box test) and only then
`otherChain.Gap(p, myRadius, zStretch)`. See `Koi.RoomAround` for a complete one: land, the
surface, the room grid, the bed, then every other body. THE BED IS A GAP, NOT A TEST: return the
water left under the point (0.9 of the floor, less the point's depth, stretched like the gap to a
body below) as room, read under a ball as wide as the animal (`Koi.BedUnder`: the point and four
more most of a half width out; a resting fish drifting along a rock with its nose heading clear
had its flank ride up the stone unfelt), so a bed rising ahead is felt before any sample is
inside it. A plain
"inside or not" test left the animal's depth clamp to do the steering, one floor step a frame,
which is a hop on the screen (the koi near a rock, the turtle on a dive). And make the animal WANT
to be a margin above the bed (the koi: 0.3 of its length of water under it; the turtle: 0.35),
the same gap its feelers call clear, or it sits on the clamp's boundary where its feelers flicker
between wall and clear. And refuse a step onto a bed with no water for the animal at its depth
(`Koi.Update`'s step rule), as a step onto land is refused: an animal that cannot rise as fast as
the bed comes up (a resting fish barely rises at all, by the slide rule) stops at the stone's foot
and turns, instead of being lifted over it by the clamp in one hop.

**Step 3. Call `Feelers.Best` when something is in the way.** `Feelers.Along(room, from, dir, look)`
samples the room at 0.12, 0.35, 0.65 and 1.0 of the feeler (the near sample is so a small thing
cannot slip between samples) and returns the least, the near ones counting for more. Use it as the
TRIGGER ("is there less than half a body length ahead?"). When it fires, call
`Feelers.Best(room, from, heading, look, enough, step, steps, climb, turnCost, climbCost)`: it
tries the course as it is, then `steps` headings either side, each one level, climbing and
diving, and returns the best (heading, climb, room). Keep the heading as a steering pull; keep
the climb as a depth change per unit forward (the koi stores it as `_dodge`). With a fish ahead at
nearly its own depth the koi goes over or under; with a bank ahead a climb clears nothing and it
turns, with no special case for either.

**Step 4. Resolve touches.** The feelers are the plan; the touch is the last resort.
`Chain.Touch(a, b, out push, out depth)` gives the way to push `a` (and `b` the other way) and how
far, from the closest points between line pieces; the deepest overlap wins, so two bodies crossing
come apart where they cross. `KoiSchool.Update` runs two passes over every pair of overlapping
bodies, moves each half the way out (a turtle is the heavier and is never moved, the fish takes the
whole push), then calls `Koi.Bumped` on both. A fish not in a chase, flee or flinch shies for a
second or two (the doing `Shy`), away and up or down as well as aside, on a cooldown.

**The rules learned, all of them from the kit's own self-check or the instruments:**
- **Do not normalise the feeler's direction.** A climbing course covers the same ground per step
  and changes depth on top of it, as the animal really moves. A shortened feeler "found" more room
  beside a wall simply by not reaching it, and a climb appeared to clear a bank.
- **Stretch the gap up and down in the WORLD, not in the search.** Passing over or under a body
  needs less clearance than passing beside a bank, so the Room answer counts a gap up or down
  2.5 times a gap sideways (`Chain.Gap`'s `zStretch`; `Touch` never stretches, bodies meet where
  they really meet). Then a climbing course simply has more room and wins on that. Scoring climbs
  by their own smaller "enough" inside `Best` was tried first, and a fish kept diving under another
  long after it was clear, because the search's idea of clear and the trigger's disagreed.
- **The trigger and the search must agree on what room is.** Both call the same Room answer, with
  the same stretch, and the same "enough". Two definitions of clear is a fish that dodges and then
  keeps dodging.
- **A recoil, not a jump.** Cap the push per frame to what the animal swims (a body length a
  second). Shoved out in one frame, fish hopped sideways, which reads as a slide.
- **Move the whole body.** `Nudge` shifts the trail the spine is laid along, not just the head;
  moving the head alone left crossed tails crossed for seconds.
- **When every feeler is blocked, the search returns the heading it has, which is no turn.**
  Its nose in a pocket, with the way out behind it (the search reaches a right angle either
  side), a koi stood against a rock for forty seconds with every step refused. Test `found <= 0`
  from `Feelers.Best` and turn toward `Pond.OpenWay` (uphill of the room grid), as the turtle
  and now the koi do. A push must not put an animal onto land either (`Nudge` tests it).
- **A refused push needs a fallback.** An up or down push the bed or the surface refuses is
  clamped straight back (two fish in the shallows did this 400 frames running); when still inside
  after a mostly vertical push, push sideways along the line between the middles.
- **Hold both rise AND sink to a share of the forward motion, by where the nose points on the
  screen.** Depth moves a picture up and down the screen (`Light.SinkPx`), so a fish rising
  faster than it swims is seen sliding up the screen whichever way its nose points, and one
  heading down the screen is seen going backward. The allowed rise is half the forward motion
  with the nose across the screen, none with the nose straight down it, all of it with the nose
  straight up it (then the rise only adds to the way it is going); the sink is the mirror. A rise
  the bed forces is held the same way (with the pond this deep, an exempt bed rise slid a fish
  50 pixels up the screen in a second); the clamp in `LaySpine` has the last word, and it caps
  the REMEMBERED depths with the head (every trail depth deeper than the cap comes up to it),
  never leaving a step in the trail: capping the head alone left a kink of depth that travelled
  down the body for seconds. (Lifting the whole body by the head's lift was tried: the tail,
  already shallower, rose too, and the fish slid up the screen.) The bed is read under the body
  only, along its unswayed line and both edges, never under the see-through tail fin (whose
  swaying edge sweeping a stone's rim yanked the whole fish to the surface every half beat). And
  the body is STIFF IN THE VERTICAL PLANE: its depth is a straight line from the head's depth to
  the depth the trail gives the base of the tail, so a fish that dived and levelled is a pitched
  rod, not a vertical S drawn from the head's whole history of depth. The review traced 95% of
  the owner's "worm" frames to that history; the straight line took the count from 5% of frames
  to 0.3%. The first version
  held only the rise, and only against the down-screen part of the motion: a resting fish with
  its nose sideways slid up the screen on a slow rise, and one sinking out of sight drifted down
  it with its nose pointing up.
- **A swum change of depth is laid down the course; a drifted one moves the whole body.** An
  animal that swims along its own length tilts into a dive, so the body follows the head down the
  slope; but it cannot swim its nose steeper than its dodge allows (0.8 of the forward motion for
  the koi), and a change of depth beyond that is a drift, level, the whole trail together. And a
  body that is hardly moving levels out over a second or two. Without both, a resting fish
  sinking in place was laid nose down at sixty degrees, and one hung nose down for eight seconds.
- **A push the bed would undo is refused.** A shove onto a rock's steep flank lifted a fish by the
  bed's clamp in one frame (a nine pixel hop); `Nudge` drops the ground part of a push that would
  lift it more than a little, and the other body still takes its half.
- **Test it as a timeline.** Frames with two bodies inside each other, the longest time stuck in
  a bump, and nose-first over a window that suits the animal (section 6).

## 9. The trail, and walking on a surface (Core/Minds/Trail.cs, Ground.cs)

**The trail (Trail.cs).** Every bending animal moves the same way: only its head is steered, and
the body follows the path the head has already taken, like train carriages following the engine.
`Trail` is that memory (a ring of points, each with a depth) and the walk back along it. Each
frame: `RememberIfMoved(head, minStep, z)` (a still animal does not fill its memory with one spot),
then `Begin(head, headZ, depthPx, heading)` gives a `Walk`, and `Advance(distance)` returns the
point that far behind the head along the path (and its depth), for distances that never decrease.
Past the end of the trail the body carries straight on back against the heading. `Straight` lays
a straight trail behind an animal that has just appeared; `Shift` moves every point (a shove
moves the whole body, or the tail stays crossed with the other fish's for seconds); `EaseDepths`
drifts and levels the remembered depths (a resting fish levels out). A throwaway check matched
`Walk` against the lizard's old hand-written loop on 200 random trails before the lizard moved
onto it, and a pixel diff of the saver before and after was 0 pixels.

**Walking (Ground.cs).** Space.cs is for things in a volume; `Ground` is for a thing on a SURFACE
with an edge it must not step off (a lizard on a bank, a mouse in the grass). The surface is a
`Solid(x, y)` question the animal answers from its own mask, in its own unit. Three rules, each
one a lizard bug first:
1. `Step(ground, ref x, ref y, ref heading, step, turnErr)`: forward if that is ground; if not,
   and the goal is mostly behind or to one side, the first of 35, 70, 105, 140 and 170 degrees
   round toward the goal that is ground, heading turned to match (`Slid`); else `Refused`. The
   animal decides what a refusal means (a dash ends; a leaving dash marks it stranded).
2. `LineClear(ground, a, b, every)`: a goal is worth running at only if the whole straight line
   to it is ground. Testing the midpoint alone let a lizard on a wet rock pick spots across the
   water and stand refused.
3. `FleeRun(clear, me, from, stride, most, least)`: the longest clear run away from a point,
   straight away or within about 90 degrees of it; a run must beat the best by half a stride to
   replace it, so the straight line wins ties and the animal does not swerve for nothing. Null if
   nothing reaches `least` (then take the way out of the screen).
Make the delegates once in the constructor (a new one every frame is garbage), one per unit the
animal uses (the lizard asks the land in ground units for steps and in screen pixels for lines).

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
| Koi (`src/SakuraPond/Koi.cs`) | energy, sociability, curiosity (dealt) | Wander, Rest, Investigate, Follow, Chase, Flee, Flinch, Shy | being chased, a dragonfly's tap, a swallow's shadow, a bump from another body |
| Lizards (`src/SakuraPond/Lizard.cs`) | bold, energy (sliced, paired) | Hidden, Dash, Freeze, Bask, Drink, Face | a meeting, the snake |
| Turtles (`src/SakuraPond/Turtle.cs`) | sunny, nerve (dealt) | Cruise, Float, Dive, Approach, Climb, Bask, Slip | the snake's head near a basking turtle |
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
slides along the shore. Only the koi steer with the kit so far (the turtle supplies a Chain so the koi can feel it; the snake and the lizards
steer on a flat plane and have not needed it). A stranger giving a new animal a mind gets the
deciding half and the 3D steering for free, still writes the Room answer and the refused-step
rule, follows the rules in section 4, and should expect the instruments in section 6 to find the
first bugs there.

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
one, or the thing that touches and the thing you see will disagree.

**Step 2. Answer the Room question.** A method of the animal, `float Room(in Vector3 at)`, made
into a `Feelers.Room` delegate ONCE (a new delegate every frame is garbage). Return 0 for
"something is here" (land, the bed, the surface, inside another body) and otherwise the distance
to the nearest thing. Ask the cheap things first. For other animals use
`otherChain.Near(p, myRadius, roomSoFar)` (the box test) and only then
`otherChain.Gap(p, myRadius, zStretch)`. See `Koi.RoomAround` for a complete one: land, the bed
(0.9 of the floor under the point), the surface, the room grid, then every other body.

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
  50 pixels up the screen in a second); the clamp in `LaySpine` has the last word, and the hop
  that leaves is a pixel or three a frame. The first version
  held only the rise, and only against the down-screen part of the motion: a resting fish with
  its nose sideways slid up the screen on a slow rise, and one sinking out of sight drifted down
  it with its nose pointing up.
- **Test it as a timeline.** Frames with two bodies inside each other, the longest time stuck in
  a bump, and nose-first over a window that suits the animal (section 6).

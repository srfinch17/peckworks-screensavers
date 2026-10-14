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
| Koi (`src/SakuraPond/Koi.cs`) | energy, sociability, curiosity (dealt) | Wander, Rest, Investigate, Follow, Chase, Flee, Flinch | being chased, a dragonfly's tap, a swallow's shadow |
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

The MOVING half, the steering reflexes under the mind (feelers along the heading, the smallest
turn that clears a thing, the refused step that forces a turn, the touch test between animals),
is still written per animal, and that is where every creature bug so far has lived. It is not
shared yet because each animal reads a different world: a koi asks a coarse "room to swim" grid
and swims under pads; the snake asks a list of circles and a capsule and goes round pads; a
lizard asks a land mask and slides along the shore. A stranger giving a new animal a mind from
this page gets the deciding half for free and must still build the steering, following the rules
in section 4, and should expect the instruments in section 6 to find its first bugs there.

The honest next shared piece is a steering kit (feelers over a mask, the smallest-turn search,
the refused-step rule, a point-to-spine touch with a `Cooldown`), to be added when the next
animal needs the same shape as one that exists. Add to `Core/Minds` only what two animals need
in the same shape; a thing one animal needs stays in that animal.

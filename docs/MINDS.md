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
| `Trail` (Trail.cs) | The path the head has taken, and the walk back along it that lays a body: `Remember`, `RememberIfMoved`, `Straight`, `Shift` (move the whole body), `Sweep` (turn the whole body about the head: a pivot on the spot), `EaseDepths`, and `Begin(head, z, depthPx, heading)` giving a `Walk` whose `Advance(distance)` returns the point that far behind the head along the path. | The lizard lays its 16 spine points with one `Walk`; the koi's and the snake's hand-written walks are the same algorithm and are the next to move onto it. |
| `Ground` (Ground.cs) | The walking rules for anything on a surface with an edge: `Step` (forward, or slide along the edge toward the goal when the way ahead is refused), `StepRound` (the same, but a thing straight ahead is gone round at 30 or 60 degrees either side: for obstacles on open ground, where a shore's rule refused and hung), `LineClear` (is the whole line to a goal ground?), `FleeRun` (the longest clear run away from a point). The surface is a `Solid(x, y)` question the animal answers. | The lizard's steps, roam goals, exits and flights; the mouse is built on the same three; the patch squirrels step round the pumpkins. |
| `Noticeboard`, `Notice`, `Sense` (Senses.cs) | The scene's board of what is going on: anything pins a notice (a kind: Loud, Movement, Threat, Food, Friend, Call; a place; how far it carries; how strong; who caused it) and it falls off after a second. | Thanksgiving's director pins each happening as it begins (the raccoon a threat on the near ground, a crow a movement); the baling tractor pins itself Loud as it passes and each bale it drops as Food; a tom's display is a Call (the curious hens come over); a story pins its scuffle; a squirrel on the move pins itself every third of a second. |
| `Attention`, `Reaction` (Senses.cs) | An animal's senses: two dials (timid, curious), a hunger mood, how keen, and a cooldown. `Check(board, me, self, out cause)` reads the board and says what the animal felt: Look, Freeze, Flee, Approach or nothing, the strongest first; a reaction starts the cooldown, and only something worse gets through it. The animal turns the reaction into its own doings. | A turkey's freeze is the head up facing it; a deer's flight is a brisk walk away that pins a Friend notice so the herd looks up; a mouse's is a bolt behind a pumpkin. |
| `Pace` (Mover.cs) | A speed that eases toward what the animal wants under an acceleration cap, a `Brake(dist)` that runs it down to nothing over exactly a chosen distance (so the walk ends on the rest page), `Halt`, `Resume(speed)` (hands a speed back outright: a turn that kept its momentum), and the odometer `Walked` with `Page(stride, pages)` and `ToRestPage(stride)` for paging planted feet by distance. `Tick(dt)` returns how far it moved. | The turkeys and the deer lean into every walk and run down onto the page with both feet planted; the patch's mice gather speed over a few steps and slow into a goal. |
| `Turn.Toward(heading, target, rate, dt)` (Mover.cs) | A heading that turns at a rate and eases to a third of it within the last fifteen degrees, so it settles instead of ticking across the target. | The patch's critters. |
| `Organic.Drift(t, seed)`, `Organic.Jitter(rng, mean)` (Mover.cs) | A slow wander between -1 and 1 made of three waves that never line up, and a timer with a natural spread (never under a third of the mean, sometimes well over). | A standing deer's head drifts a page or so; every doing's timer is jittered, so nothing happens on a metronome. |
| `Herd.FollowX`, `FollowError`, `Spacing` (Herd.cs) | Where a follower belongs (a gap behind its leader, on the leader's own side), how wrong its gap is beyond a slack, and the push from neighbours standing too close. | The fawn keeps her place behind the doe and only trots when the gap is wrong by more than she can bear. |

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
  Soft pushes summed are not that rule: the minnows' "turn from the bank" push lost to the school's
  pull and they nosed the island, tails beating (round twenty-three). Every animal that steps gets
  the refused-step rule, flockers included.
- **The way out is the way in.** Feelers see a body length; a pocket among rocks with the goal beyond
  them and the picture's edge for a wall has no way out that short, only ways along, and the snake
  paced it. An animal lays crumbs as it goes (where, and how open it was there), and a doing that
  begins in a tight spot walks the crumbs back to open water first, the path it knows is passable.
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
- **One owner of "clear".** Every function that answers whether a step may be taken (the step itself,
  the refused step's probe, a shove, a placement) calls the step's own test list, never a subset. A probe
  that tested the bed and the land but not the closing-room rule sent a koi into a pocket the step then
  refused (50 s); the shore added to the step and not the probe held one 42 s; a shove tested at the head
  alone carried one onto the bank (131 s). Three rounds, one class. The next wall class goes in through one
  `Allowed(from, to)` that all of them call.
- **A wall class is in every grid the animal steers by.** The koi's stone shoulders were in the step and
  in the feelers' samples but not in the room grid the feelers climb when nothing is clear (the grid is
  built before the hills, earlier in the same function), so a shoulder was a cliff with no slope and the
  fallback climbed INTO it against the probe's way out: a goldfish stood 115 s. When a wall is added for
  one animal, grep every grid and mask that animal reads, or give it a grid of its own (`Pond.KoiRoomAt`).
  Since round forty-three the room grid is STACKED BY DEPTH (`Pond.RoomAt(x, y, wallZ)`, sixteen floors; on
  each the bed shallower than that floor is a wall as the land is), so a wall that is a depth is in the
  grid too: a swimmer reads the floor its own step rule would refuse it at (`Koi.WallZ`, the cap and the
  shore turned round; the turtle nine tenths of its depth; the minnows their dip's rule) for its feelers,
  its search trigger and its "nothing is clear" fallback, and a mound ahead is falling room, not a cliff.
  BUT A WALL THAT IS A DEPTH IS ANSWERED BY A RISE, NOT BY FOLLOWING (round forty-five): the minnow's step
  refused on its depth's floor while its wall-following read the surface floor, where a stone's mound is
  open water, so the fish read "nothing to follow", kept its heading on the school's target past the mound
  and was refused on the spot every frame: five fish pinned at the island's foot for 55 s the moment the
  mound became a boulder's steep skirt. Reading the following on the depth's floor instead made the ridge
  rule flip its side every frame on the mound's bumps, and nine fish spun there for 45 s (seed 6, 1080p:
  spin36, side flips 300 to 500 each). The koi's rule held: a refusal by the bed is a slope, so a refused
  dipped minnow RISES (a tenth of the depth a second, a target a fifth above it, `Minnow.Refused` in the
  school's depth loop) and the step passes again; the following stays on the surface floor for the land,
  the caps and the edge.
  The koi's stack keeps the shoulders as walls; the snake still steers on its own flat plane (step 5).
- **A resident of a hole is hidden by the painter's depth test, not by a stencil; and what a threat can SEE is only
  what is out of the hole (round forty-six, the eel).** The eel inside the log is drawn at the bed's depth under
  the wood, and `Pond.Near` hides it; the depth the test is given is a tenth deeper than the body's, because the
  log's rounded end is low and the body's own depth showed through just behind the cut face. Its fright reads
  the koi over its head, middle and tail, but a part still inside the wood cannot be seen by a fish: a koi
  crossing the log's far end kept the first eel in for the whole run. And a shy animal's timers are measured
  by SECONDS OUT PER MINUTE in a natural run before any strip: three 180 s runs that never showed it were
  three runs wasted on the strips of a thing he would never see.
- **A probe that answers afresh every frame can answer two ways, and the escape must be COMMITTED (round
  forty-five).** The koi's refused-step probe tries headings a side at a time and answers the nearest clear
  one; re-run every refused frame, a marginal probe (a right angle to the left, clear or blocked by a pixel of
  the bed's byte raster as the heading wobbled a fiftieth of a radian) flipped its answer between "left a right
  angle" and "right three quarters", each answer undid the last through the eased turn rate (plus and minus
  0.29 a second, four times a second: the balanced-pulls tell below, at a slower beat), and a koi stood in
  the bottom corner by the formation for 32 s through six shies and wanders with its tail bent up into view
  (seed 2, 1080p; the new turn-parts columns of the koi log, `turn=goal:feelers:way:lean:most:final`,
  named it in one read). Now the first answer is KEPT and turned to until the heading is within 0.15
  radians of it or a step succeeds (`Koi._wayHeld`); then it probes again from there. The minnows' remembered
  wall side is the same rule. An escape is a state, never a per-frame opinion.
- **A turn of half a circle has no side, and a probe has a floor in pixels.** "Straight behind" as the
  answer to "nothing is clear" gave `Wrap` a different sign each frame, and the eased turn rate oscillated
  about nought at the clamp times the easing share (0.9 at 8 per second: 0.09), which is the TELL of two
  pulls balanced; answer with the grid's uphill, or a turn short of half a circle to one side. And a probe
  placed at a multiple of the step shrinks with a slowed animal to a fraction of a pixel and reads its own
  pixel: floor it (a pixel and a half).
- **A wall that is a depth is in the animal's own clearance test, and a body laid along a trail is laid
  in three dimensions over the bed (round forty).** The snake's feelers knew circles and land; only its
  step rule knew a stone's hill, so a dived snake swam straight at the hill, stopped, and pivoted on the
  spot at 2.2 radians a second (a right angle laid into its trail: "janky 90 turns around the rock"), and
  its body, laid at the depth the head had when it passed, was drawn under the hill it was now crossing
  ("swim through rocks") and longer than itself when the head rose ("stretch"), because the trail was
  measured along the ground alone. Now a point whose bed is shallower than the head is deep counts as
  inside something in `Snake.Clear` (a fifth of u per unit of depth); the trail's length counts its rise
  (`Seg`); every body point is capped by the bed under it and the caps are smoothed along the body at sixty
  degrees; and the head's turn rate has the koi's turning-circle floor (0.4 body lengths, 0.3 cornered).
  Measured over five minutes of dive stress: the fastest turn 9.6 to 1.5 radians a second, the head inside
  a hill 2.5 percent of frames to none, the longest blocked wait 5.0 s to 0.9 s.
- **A pivot sweeps a trail-laid body (round forty-one).** A body laid along the trail its head swam
  inherits every corner of that trail, and a head that turns ON THE SPOT lays the whole angle it turned
  into one corner: a refused stand (the step rule holds the head, the heading keeps turning toward the way
  out) or the cornered pivot (speed at an eighth of a length a second, the heading at its 0.9 rad/s floor:
  7.5 radians of bend per body length, where cruising lays 1.7 to 2.5). The corner then slides down the
  body as the fish swims off, one spine point a frame ("an unnatural looking kink that undulates throughout
  their body as they turn or swim away"): joints of up to 93 degrees, 158 frames over 45 in five minutes on
  one seed, 60 of 119 episodes within two seconds of a refusal. A fish has no hinge: when the head turns
  faster than its path bends, the body swings round with it and the tail sweeps the other way. So
  `Koi.SweepBody` compares the trail's direction over the eighth of a length behind the head with the
  heading and turns the whole remembered trail about the head by the excess over the sharpest bend a body
  holds (3 radians per length), after testing the swept body where it lands (land, the shoals, the bed at
  each remembered depth): blocked, half, a quarter, or none. Not over a shorter reach: a shove moves the
  head a pixel in a frame and read as a turn. The metric is the sharpest joint of the logged spine per
  frame (kit `motion/pond41/kink41.py`), and a depth change on a refused frame is all drift (`made`): judged
  against the step the fish meant to make, a rising stand laid a vertical trail segment at the head and the
  drawn head pointed wherever two coincident points said. THE SNAKE BENDS ITS NECK instead (`Snake.BendNeck`,
  his next line the same evening: "a snake on the bottom make a 90 turn ... near a rock"): a swallow on the
  bank stood six seconds with the steering turning the head at its floor, 97 degrees in all, laid as one
  corner when it moved off (kit `snakekink41.py`, the trail's own corners from the logged head: 493 frames
  over 30 degrees on one dive-stress seed, none after); the excess over the same bend fades out four tenths
  of a length back, and is judged against the way the head MOVED, not its heading, because the walk out of
  an obstacle steps along the clearance's uphill (a 43 degree corner at the picture's edge wall). Two corners
  the neck bend cannot see, found by logging the DRAWN spine's sharpest joint: an offset hung off the heading (the
  snake's weave, 18 px) swings round with a fast heading swing, a sideways hook of the head laid into the trail
  (105 degrees on the picture where the path turned 43): hang it off a direction that follows the heading at a
  rate (`_weaveDir`, 1.5 rad/s). And an exponential ease of depth is fastest at its first frame, so a dive's pitch
  came on in one frame and the sink drew the corner in the depth profile as a joint of 30 to 51 degrees sliding
  down the body: ease the RATE of descent (`_zRate`). And a body draped over a stone's shoulder by the bed cap
  at sixty degrees of pitch was a joint of 50 to 81 degrees on the picture (the sink and the squash sharpen it):
  forty-five degrees of pitch at most, laid and swum (the one that fits "a snake on the bottom make a 90 turn ...
  near a rock"). MEASURED (kinkB, 720p, 300 s natural): seed 3's sharpest joint 93 to 34.5 degrees, frames over 45 degrees 158 to 0, over 25 degrees 794 to 168, episodes within two seconds of a refusal 60 to 3, the drawn head against the steered heading while refused p90 35 to 13 degrees and max 99 to 19; seed 5 (no pivot corner before either) worst 40 both ways, all at the tail base in hard turns at cruise; no confined spell over 10 s on either seed (longest 5.0 and 5.9 s, resting).
- **A frightened small thing turns and accelerates at a rate, not in a frame (round forty).** The
  tadpole's flight turned at 30 radians a second (a whole-body snap of 29 degrees in one frame: one frame
  in a hundred) and went from nought to six lengths a second in one frame (nine hundred times in five
  minutes), and a flight refused by the bank was followed at once by another the other way: a tadpole
  cornered by a fish turned a full circle in a second and a half with its speed going nought, six, nought,
  six. Now the flight turns at 9 radians a second, the speed comes up over a tenth of a second, and a
  refused flight counts as the whole habituation (it hangs until it has been calm a while).
- **Sizes and speeds are in u** (the screen's size unit), never "cross the screen in N seconds".
- **A behaviour shared by many animals is a small object each one OWNS, not a doing in each.**
  The chance meal of a tadpole (`Snatch` in `Tadpoles.cs`) holds the dice, the claim on the prey,
  the patience and the rest for every eater; each animal keeps only its own steering toward
  `Prey` and its own mouth and reach. It takes its own `Random` (seeded from the dealt dials), so
  adding it moved no roll in the pond. Everything that can take the animal over calls `Abandon`.
  Since round thirty-three the prey is anything that is an `ISnack` living in an `ISnackField`
  (tadpoles, prawns and the deep dwellers today), so a second prey kind is a second field handed to the same
  `Snatch`, never a second chase inside the eater. The reach test is sized to where the prey
  LIVES: the prawns sit on the bed of the shallows, and an eater's tadpole test (deep water
  under it) found none of them, so on the first run no hunt ever happened. A stress hook
  (`PECKWORKS_SNATCHOFTEN`) makes every eater look oftener so a rare hunt can be seen at all.

## 5. Who runs on it today

| Animal | Dials | Doings | Interrupts |
|--------|-------|--------|------------|
| Koi (`src/SakuraPond/Koi.cs`) | energy, sociability, curiosity (dealt); hunger (a mood); a liking for every other fish (starts from the dials, grows with time close, fades apart: pairs and trios) | Wander, Rest, Investigate, Follow, Chase, Flee, Flinch, Shy, Leap, Snatch (a tadpole it came upon, or a prawn when hungry, or a dweller of the deep bed when it is one of the largest, hungry and already below half depth: `Snatch`; a caught prawn or dweller takes 0.4 off its hunger, a tadpole 0.1), Tease (a dash at a school of minnows for about a second that sends the school bursting apart, then a glide to a stop), Forage (down to the deep bed for a minute, spot to spot, nosing in the silt at each and stirring up a puff of it), Rest on the bed (a fish that rests low settles right down onto the bed for half a minute), Shallows (up the bank's slope to water about its own height deep, back and dorsal fin out, a few seconds nosing there, then away; round thirty-eight) | being chased, a dragonfly's tap, a petal settling (when hungry), a swallow's shadow, a bump from another body, a neighbour's splash, a jam (a step refused for 1.5 s ends the doing: `BackOff` toward the deepest water, and no goal is chosen near that spot for half a minute; a refused step also probes round the CENTRE for the nearest clear heading, `WayOut`, because the feelers look from the nose; a stone's shoulder with under 0.45 of the depth over it is land to a koi, `Pond.StoneShoal`; the SHORE, water under three quarters of the fish's own height, is refused and is no room to the feelers, and its back breaks the surface before it gets there, `Koi.CapOf`, `OnShore`, round thirty-eight) |
| Goldfish (`Koi.cs`, `Koi.Kind.Goldfish`) | the same three dials, dealt over koi and goldfish together | the koi's, with Follow doubled toward another goldfish, no Chase at a koi, a shallower Rest | the koi's, with a flinch reach 1.5 times as far |
| Catfish (`Koi.cs`, `Koi.Kind.Catfish`, round forty-four) | the same three dials, dealt from its OWN Random (seeded by the screen) after every koi and goldfish, so the knob moves no other fish | the koi's with Investigate, Follow, Chase, Dart, Shallows, Leap and the gulp nought; Forage 1.8 times a koi's; every depth asked for at least 0.75; its feelers read ONE floor of the stacked grid (0.6) and never choose a climb | the koi's, less a petal or a tap (a fish on the bed sees nothing at the surface) |
| Lizards (`src/SakuraPond/Lizard.cs`) | bold, energy (sliced, paired) | Hidden, Dash (Roam, Drink, Charge, Flee, Stalk), Freeze, Bask, Drink, Face, Dodge, Caught | a meeting, the snake (only if it is seen: ahead or beside, or on top of it), a strike (dodge or held), a swallow's shadow. No stopping spot under an iris clump's leaves or the cattails (`UnderLeaves`, round forty-seven: the clumps are drawn over it, "stuck in a rock"); a dash begun facing water pivots on the spot before it runs (`mustTurn`: the body swept round with the head by `Trail.Sweep`, the timer held, a refused first step no longer ends it); the log `PECKWORKS_LIZARDLOG` carries the step rule's answer |
| Mice (`src/SakuraPond/Mouse.cs`) | timid, greedy (sliced, paired) | Hidden, Scurry, Sniff, SitUp, Groom, Nibble, Drink, Jump, Caught | the snake's head (only if seen), a strike (a jump or held), a lizard's nose, a swallow's shadow |
| Turtles (`src/SakuraPond/Turtle.cs`) | sunny, nerve (dealt); hunger (a mood that creeps up over ten minutes from the last meal) | Cruise, Float (and a petal nibble), Dive (to the bed itself when the dice say so, 18 to 40 s), Approach, Climb, Bask, Slip, Snatch (a tadpole it came upon, or a prawn when hungry, or a deep dweller on a dive below half depth when hungry) | the snake's head near a basking turtle, a body in its way (feelers), a touch (Nudge) |
| Snake (`src/SakuraPond/Snake.cs`) | bold, nerve (per visit; the skin, the hunt's own dice and the rate of a HUNGER dial that is kept across visits come from them; hunger scales the visit clock, the prowl, the notice range, the tadpole look and, above 0.3, a real minnow lunge that takes a fish via `Minnows.Take`) | Cross, Explore (and Prowl toward prey on the bank), Hunt, Bask, Stalk, Strike, Swallow, Miss, Snatch (a tadpole or froglet at the top of the water right at its nose, from Cross or Explore only; a mouthful, not a meal); a Cross or an Explore dives half the time (the head eases down to 0.3 to 0.7 of the pond, the body follows along its own trail, and a deep snake neither hunts nor notices anything at the surface, but past 0.4 of the depth may take a crayfish or prawn of the deep bed at its nose, `LookOnBed`, a real meal, the head held at the prey's depth through the chase) | a touch from a koi, turtle, lizard or duck (not in a strike or a swallow, a fed snake only flinches); the ducks' bodies are in its clearance test |
| Crayfish (`src/SakuraPond/Crayfish.cs`) | bold, hungry (rolled per visit) | Hidden, Emerge, Walk (and a walk at a tadpole within a few steps, then a Feed on it), Feed, Threat, Back, Pivot, Flip, Hold, Retreat | a koi, turtle or duck over it (within 0.09u, bold 0.06u) |
| Prawns (`src/SakuraPond/Prawns.cs`) | bold, busy (dealt at birth by `Traits.Deal`, so no two are alike) | Feed (the biggest face), Walk, Swim (rare), Turn (a pivot, 0.3 s a quarter turn), Flick (2 to 4 backward tail flips), Settle (still, feelers up); and being eaten (an `ISnack`: a hungry koi or turtle that happens to spot one within reach takes it, and the prawn is reborn at a home 90 to 240 s later) | a koi, turtle or duck within 0.07u (bold 0.05u) while it feeds, walks or swims; 3 s between scares |
| Snails (`src/SakuraPond/Snails.cs`) | none (every snail is the same snail; its dice set only when it sets off) | Creep (a body length every twelve seconds, the heading drifting a little), Graze (still, the tentacles waving) | a refused step (the land, a standing rock, a pad's foot, the picture's edge): it turns a quarter to a half turn and tries again, right round after three seconds of refusals; nothing frightens it and nothing eats it, everything passes over it |
| Deep dwellers (`src/SakuraPond/DeepDwellers.cs`, a crayfish or a long-armed prawn by `DeepKind`) | bold, busy (dealt at birth) | Feed, Walk (short and brisk: the skitter), Turn, Flick, Settle, Threat (claws up at a fish over it), Bout (a fight with another dweller, played by one `Bout` object for the pair: face, display, close, grapple, break; the winner rolled from length, kind and boldness, a chance in five for the weaker), Lunge (a dash at a minnow of a school dipped to the bed within two and a half claws' reach, then the near claw shot out and snapped, `Minnows.Take`; a hit held and eaten over a feed); and being eaten (an `ISnack`, stages 6 and 7: one of the largest koi hungry and down on the bed, a hungry diving turtle, the diving snake; a newcomer walks in from the picture's edge minutes later) | a koi's head or middle whatever its depth, a turtle, a duck's head or the diving snake's head within 0.08u (bold 0.055u) while it feeds, walks or is settled; 4 s between scares; a fight handed out by the referee only while idle and free |
| Ducks (`src/SakuraPond/Ducks.cs`) | calm, fussy (per visit); a duckling has none, the director picks for it | Cruise, Look, Dabble, Preen, Flap, Wait, Herd, Snatch (duckling: Follow, Lag, Scramble, Bump, Ride, Peck, Chase, Dabble, Bunch, Scatter) | the snake's head near the mother (Herd), a straggler (Wait), the family's cute-thing dice (one at a time; a duckling's drawn heading is its steering heading plus a one degree paddle sway, so a metric on the drawn heading and one on the steering heading agree; the director of the cute things is the family's own, outside the kit). Snatch (a chance meal of a tadpole or froglet within a tenth of the screen of the bill, from Cruise only, a `Snatch` seeded from the dials) is ended by any of them, through `Duck.Start` |
| Tadpoles (`src/SakuraPond/Tadpoles.cs`) | none: a tadpole runs a small state machine of its own (`TadState`), not a `Mind<TDoing>`, and the clutch is its own director, outside the kit | hang, burst, scatter, graze, grow, dive, rise, leave as a froglet, or be eaten (`Exit.Eaten`, one `Hunter` at a time); the clutch moves house every 2 to 5 minutes between three haunts (`Clutch.Haunt`: Shore, Island, Open) and one tadpole ventures off alone about once a minute | every koi's head and middle whatever its depth, the turtles, the ducks' heads, the snake's head, the crayfish |
| Frogs (`src/SakuraPond/Frog.cs`) | bold, restless (dealt); wet (seconds since it last sat: past twelve it wants out) | Sit (with croak, shift, pivot inside it), Snap (at a dragonfly, or at a froglet or shallow tadpole: one hop, eaten only if the snout lands on it), Leap, Swim (to a NEAR perch, the next pad or stone, 20 s at most), Float, Dive (at most two in one spell in the water), Climb | the snake's head, a koi's head or a turtle near a swimming frog, a lizard's nose near one on a rock or the bank, a swallow's shadow |
| Christmas reindeer (`src/Christmas/ReindeerHerd.cs`, 2026-11-10) | timid, curious (dealt; the calf is more of both, the bull less timid); an `Attention` each | Graze (scan, paw three times, eat, chew), Creep (a drift forward: a step, a scrape, a step), Look, Walk (alone, or LED: the others fall in behind in file), Lie, Alert, Watch (the head follows Santa), Trot, Shake, Gambol (the calf's play), Nuzzle (two touch noses), Bellow (the bull) | any reaction cuts a graze after a scrape in progress is finished; a Flee gets a lying animal up fast; Gambol only by a danger; Watch by Santa leaving or a danger; each walk brakes onto the rest page first; nothing rests, lies or nuzzles in a no-stop place (behind the snowman, the bird table, the lamp post, the cabin's door), and each animal enters by walking in from the edge |
| Christmas hares and fox (`FoxAndHares.cs`) | hares: timid, curious, play (dealt); the fox is one visitor that comes and goes | hares: Sit, Look, Nibble, Groom, Go, Hop, Tall, Thump, Binky, Bolt, Land, Hidden (behind a cover, with its own peeks), Turn; fox: Away, Go, Trot, Stand, Look, Sit, Asleep, Mouse (listen, leap, dive), Stalk, Watch (a braced pull-up, ears on the hare), Dash, Chase, Skid, Nose, Dig, Leave | a Threat (the fox, the owl) and a Loud send a hare tall and then bolting; a thump (Friend) makes every hare in earshot bolt; the fox's chase ends at a piece of cover, which it noses round while the hare leaves by the other end |
| Christmas snowman (`YardSnowman.cs`) | one mind, no walking | Idle, Watch, HatTip, FixHat, Scan, Shiver, Snowflake, Yawn, Doze, WaveCabin, Shake, Jump, Search | most doings are cut by any reaction; a Loud makes him Jump, Santa overhead brings the HatTip, a Doze is cut only by a Loud or a Threat; HatTip, FixHat, Shake and Jump are never cut |
| Christmas feeder birds (`FeederBirds.cs`) | timid, bold, lazy (dealt, from the kind: chickadee, cardinal, at most one jay); hunger | Away, Startle, Fly, Perch, Peck, Hop, Squabble, Sing, Bathe | a Threat sends every bird up at once; the jay's arrival scatters the table; a flush is posted for the rest |
| Christmas log mice (`LogMice.cs`) | bold, curious, play (dealt); hunger (a mood that rises over about four minutes and sends a mouse to the bird table) | Home, Peek, Hop, Hold, Scurry, Rise, Sit, Wash, Fall, Dig, Behind, Box, Enter (these are PHASES; the mind rolls an ACTIVITY: Home, Emerge, Wander, Sit, Wash, Nibble, Forage, Hide (behind a cover), Tussle, Watch, Flee, Return, and one activity runs through several phases) | a Threat or Loud withdraws a peeking mouse and drops a sitting one to flee; a dash is never cut; a mouse behind a cover hears nothing; a story LENDS the mice and the resident draws nothing of them |
| Christmas otters (`Otters.cs`, `OttersMind.cs`) | playful, lazy, timid, curious (dealt) | Under, Surface, Haul, Rest, Gallop, Slide, Stand, Roll, Eat, Dive, Wrestle | a Threat or Loud sinks a surfaced otter at once and turns a gallop into a panicked run for the hole; Haul, Roll and Dive are never cut; a slide is cut only before the flop |

The minnows' school mood (drift, rush, mill, and since round thirty-seven a dip to the deep bed and a graze
there, the school's depth eased at a rate its pace can hide) and the dragonflies' doings are simpler clocks
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
the bed, the surface, a coarse room grid and every other body in the water (the turtles and the
swimming snake among them), and swims under pads; the snake asks a list of circles and a capsule
and goes round pads, and fills its chain so the koi and turtles treat it as solid; a lizard asks a land mask and
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
The share is never under a third (round 27): with the nose straight down the screen a rise still
moves the nose forward at two thirds of its speed, which does not read as backward, and at the old
"none at all" a fish heading down the screen toward the island's slope could not rise to clear it,
every step onto the bed was refused, and it hung nose-on to the stone for seconds on every seed.
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

"Arrived" is never smaller than the animal's own turning circle (round 25, the shoreline pass). Two animals orbited a
target that their arrival test sat INSIDE the circle of: a fed snake retreating along its crumbs (0.025u, 18 px, against a
creep of 27 px a second and a turn of a radian a second: a circle 27 px across) and a frog swimming in beside its perch (17 px
gate, 14 to 22 px circles, 30 s of swimming until the timeout). The arrival radius is now the larger of the old one and the
animal's circle (`1.3 x speed` for the snake; the frog's gate widened to 0.75 of a body length and 2 radians of heading,
since its climb is a hop that turns it anyway). Check any new "arrived" test with: speed over turn rate, against the radius.
A creature that is on the LAND takes the stalk's narrow margin (0.02u), not the swimmer's 0.035u (`Snake.HeadOnLand`): it got
in through the gaps between the bank rocks at the stalk's margin, and with the wide one every gap read as shut, so after a
meal it circled the lawn for one to two and a half minutes (stress seeds 5 and 21; 0 of 5 stress seeds after).

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
inside it; and read the room grid on the floor of its depth stack for the point's own depth
(`Pond.RoomAt(x, y, wallZ)`, round forty-three), so the same rising bed is falling room sideways as
well as a shrinking gap below, and a feeler that climbs reads a roomier floor. A plain
"inside or not" test left the animal's depth clamp to do the steering, one floor step a frame,
which is a hop on the screen (the koi near a rock, the turtle on a dive). And make the animal WANT
to be a margin above the bed (the koi: 0.3 of its length of water under it; the turtle: 0.35),
the same gap its feelers call clear, or it sits on the clamp's boundary where its feelers flicker
between wall and clear. And refuse a step onto a bed with no water for the animal at its depth
(`Koi.Update`'s step rule), as a step onto land is refused: an animal that cannot rise as fast as
the bed comes up (a resting fish barely rises at all, by the slide rule) stops at the stone's foot
and turns, instead of being lifted over it by the clamp in one hop. And a cornered animal must be
ABLE to turn: the turning-circle floor scales the turn rate with the speed, and a cornered fish
slows, so the slower it got the less it could turn and the longer it stayed cornered (68 frames
nose-on to the island at a quarter radian a second). Cornered, a koi pivots at up to 0.9 radians a
second and never slows below an eighth of a length a second.

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
bodies, moves each half the way out (a turtle is the heavier and is never moved, and nor is the
swimming snake, handed in as a surface body: the fish takes the whole push), then calls `Koi.Bumped` on both. A fish not in a chase, flee or flinch shies for a
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
  second). Shoved out in one frame, fish hopped sideways, which reads as a slide. Cap ALL the
  shoves of one frame together, inside the animal (`Koi.Nudge`), not each shove where it is
  made: two passes and a sideways fallback each took a full share. And hold the up-or-down part
  to what moves the picture about a pixel (a depth change shifts the whole drawn fish down or up
  the screen): a surface koi under a body nothing moves was pushed down and trimmed back up by
  the surface every frame, a five to ten pixel hop, up, down, up, while it hung nearly still.
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

## 10. Senses and movers: reacting, and moving like a living thing (Core/Minds/Senses.cs, Mover.cs, Herd.cs)

Two things make a creature read as ALIVE to someone watching, beyond choosing what to do next:
it reacts to what happens around it, and it moves with weight. Both are shared now, and both are
deliberately small.

**Senses.** The scene owns one `Noticeboard`. Anything that happens pins a `Notice` on it: a
kind (`Loud`, `Movement`, `Threat`, `Food`, `Friend`, `Call`), a place, how far it carries, how strong,
and who caused it; a notice falls off after a second. Each animal owns an `Attention` with its
dials (timid, curious; a hunger mood; how keen its senses are) and a cooldown, and asks it once a
frame: `Check(board, me, self, out cause)`. The one rule inside turns the notices it can feel
(closeness times strength, scaled by the dials) into a `Reaction`: `Look`, `Freeze`, `Flee`,
`Approach`, the worst first; a reaction starts the cooldown, and while it runs only something
WORSE gets through (a deer glancing at a squirrel still freezes when the tractor comes). The kit
never moves anything: the animal turns the reaction into its own doings, and a reaction that
should spread (a deer bolting) pins a `Friend` notice of its own, which is how an alarm runs
through a herd without any herd code. Thanksgiving's director pins every happening as it begins
(`HappeningDirector.Began`), a story pins its scuffle, and a squirrel on the move pins itself.

**Calm (optional).** A busy scene needs the board rationed, or every animal keeps glancing at every other
(Christmas: each reindeer reacted 15 to 32 times a minute and nothing finished). Three dials on the
`Noticeboard`, all off unless a scene sets them (the block marked CALM in `Senses.cs`): `LookRest`, the seconds an
animal rests after a mere Look; `Habituate`, the seconds for interest in the SAME source to come back (in
`Attention.Check`, `Fresh` dulls a Movement, Friend or Call note from a source the animal has just reacted to
to 0.3 of its strength, climbing back over those seconds, and `Remember` writes down who it reacted to; a Threat
or a Loud is never dulled); and `Looks` per `Window`, how many Looks the whole scene may begin (`GrantLook`
hands out the slots; a mere Look needs one). Fear is never rationed. Christmas sets 14, 45, 2 and 10
(`ChristmasScene.cs`) and a reindeer now reacts 2 to 3 times a minute. The plain-words version, together with
the other three parts of a managed scene (who stands where, one start for every resident, draw width), is in
`HOW_SCREENSAVERS_WORK.md`, "The scene is managed".

**Movers.** `Pace` is a speed that eases toward what the animal wants under an acceleration cap
(a deer leans into a walk over half a second), a `Brake(dist)` that runs it down to nothing over
exactly a chosen distance (so a walk ends on the page where both feet are on the ground: ask
`ToRestPage(stride)` for the distance), and the odometer `Walked` that a flip-book of planted
feet is paged by (`Page(stride, pages)`): a foot stays planted only when the page follows the
distance, never the clock. `Turn.Toward` turns a heading at a rate and eases into the target.
`Organic.Drift` is three waves that never line up, for a standing head that is never quite
still; `Organic.Jitter` spreads every timer so nothing happens on a metronome. `Herd` holds the
two sums a group needs: where a follower belongs behind its leader, and the push from neighbours
standing too close.

**Instruments.** `PECKWORKS_MINDLOG=<file>` makes Thanksgiving's flock, herd and patch critters write every
frame's place, speed and doing, and every roll and reaction (the patch's clock counts from the first frame,
not from its sheets being ready: a log that started when the build finished ran 0.4 s early and put a flying
bird on a sheet where the pixels showed it still standing); the kit's `followlog.py` crops a strip that
follows one actor by its own logged place, and `tg_mindlog.py` reads it back as the
largest frame-to-frame speed step per animal (a toy's is its whole speed; a deer's is 2 px/s a
frame at a 54 px/s walk) and the reactions in time order. `PECKWORKS_NOTICE=Kind;xf;yf;reachU;at`
pins one notice at a chosen second, for watching the whole field react to it on a sheet.

### The animal pass of 2026-11-09 (Thanksgiving, round 13)

His ask: "make them a little more special in various ways. more intelligent for example, or funnier". What each
resident gained, all built on the parts above (a new doing with its interrupters listed, a notice posted and its
listeners measured, a pace for every move):

- The pack's rabbits (`Pack.cs`): `Thump` and `Alert`. A rabbit that sees a coyote rears up and thumps a hind foot
  twice, then bolts; the thump is posted (a `Sense.Friend` with the rabbit as `Who`, since the kit has no Noise)
  and every rabbit within twelve coyote lengths sits up and bolts early. A coyote closer than 0.10 U skips the
  thump. The coyotes: `Howl` at the end of a bout (the boldest on screen; `Sense.Loud` at 0.5 U, full weight).
  Listeners measured: the deer look and the timid retreat; the yard's dog barks back (below).
- The deer herd (`DeerHerd.cs`): the fawn's `Gambol` (a `Leaping` gait: springs, a dash out along her row, a skid,
  a turn, the bounds back; the doe `Look`s at the end). Starts only when both stand quiet with nothing noisy in
  the fawn's reach for 2 s; ended at once by a Threat, a Loud that is more than a glance, or the doe going Alert.
- The turkey flock (`TurkeyFlock.cs`): a second tom when there are three or more birds; `Square` and `Slink`. The
  face-off posts the display's Call at full weight; a Freeze or Flee in either tom aborts it; never two in 60 s.
- The farm yard (`FarmYard.cs`): the dog's `Spin` and `Dizzy` (a playful dial), `Hunt`, `Pull` and `Slink` at the
  hens (it posts `Sense.Threat` at itself at 0.08 U while hunting and skips its own post when it listens; the
  hens scatter with a hop, the rooster `Look`s then `Run`s at it); the rooster's `Crow` (`Sense.Loud` at 0.09 U,
  weight 0.3: the hens look up, a sitting dog woofs or pricks up); a cow's `Fold`, `Lie` and `Rise`. The dog also
  hears a Loud of full weight from outside the yard within 2.0 U (the coyote's howl) and barks back, even from
  inside its house, and that hearing is gated by its own 12 s `Heard` clock, not by the rest after an ordinary
  bark: a stronger reaction breaks a weaker one's cooldown.
- The patch's birds (`PatchLife.cs`): `Branch`, a perch up on the corner oak branches (the scenery's
  `BranchTimber` and `BranchTopY` give the surface; the bird's feet land one pixel into the wood). The flight up
  is the bounding flight climbing, with a flare in the last 0.3 s; up there it looks, preens, pecks and hops
  along; a Threat within 1.0 U sends a grounded bird up and keeps a perched one up (capped at 45 s). Birds above
  the ground's crest are drawn in a second pass with no depth stencil, in front of everything but the leaves.
- A new story, `BranchRaid.cs` (the stories' deck): a third squirrel on a corner branch shakes acorns down
  (`NutField.Drop` from the leaf spots near it), then slips and hangs or eats one. It posts Movement at its foot
  (reaches nobody: the branches are far above the ground animals) and the nuts post Food as they land, which
  the patch squirrels and a turkey answered in the log.

The reach rule held twice in this round: the howl reached the deer but not the dog until the dog was given a
listener for it, and the dog's first listener missed the howl because the dog was resting after its bark at the
pack's arrival, then because it was inside its house (`Home`, a doing the listener had not named). Read the
listener's own state in the log before widening any reach.

- The possum (`Possum.cs`, `PossumArt.cs`, added later on 2026-11-09): its own `Mind<Doing>` and dice taken LAST in the
  scene's constructor. `Hidden`; `Drop`, `Hang`, `PullUp` (drawn through `OpenOfProps` so the tail's top vanishes into the
  crown's leaves; a Threat within 0.4 U pulls it up fast); `Descend`, `Dismount`, `Walk`, `Turn`, `Mount`, `Climb` (plain
  on the trunk, through the depth picture on the ground, with its own copy of the patch's `CanStand` test); `Take` and
  `Eat` with a wipe (a loose nut within 0.3 U); `Faint`, `Dead`, `Rise`, `Shake` (a Threat within 0.3 U on the ground; a
  45 s rest after). It posts `Sense.Movement` at its feet while it walks (0.15 U); it reads Threat only. It never starts a
  trunk trip while `PatchLife.TrunkBusy(1)` (a squirrel on the right trunk) or while a story plays, and waits at the foot
  if a squirrel comes down meanwhile; the squirrels do not yet know about it (the reverse case is open).

### Christmas (2026-11-10)

The Christmas saver was rebuilt the way Thanksgiving was, and every animal in it runs on the parts above. All of
them are `Resident`s (`src/Christmas/Resident.cs`): each has its own dice, reads the scene's one `Noticeboard`
and is drawn through the shared `DepthQueue`. What each does with senses and movers, what it pins, and who is
in reach of the pin:

- The reindeer (`ReindeerHerd.cs`) each own an `Attention` and a gait machine beside the doing; every walk is a
  `Pace` that leans in and `Brake`s onto the rest page, paged by the distance walked. Santa overhead is a
  Movement notice from the scene, and the calf always lifts its head to follow him. They pin Movement at a
  walking or trotting animal (reach 0.25 U, weight 0.3, only after it has walked more than 2 s, then a 6 s
  rest) and Friend when one bolts (reach 0.6 U, weight 1, once per alarm, never again within 5 s). In reach:
  the hares, the birds at the table, and the other reindeer. Besides the grazing doings they have a drift
  (`Creep`: the herd slowly crosses its lane over about five minutes, each animal with its own bias), a LED walk
  (the bull or a cow sets off for a place five to nine body lengths away, at most once in 70 to 110 s, and the
  adults behind it fall in after it in file, a gap of about three heights; where it stops they stop), a nuzzle
  (two herd-mates stand nose to nose) and a bellow (the bull, with a plume of breath,
  which the nearest of the herd may glance at). Four open stretches of snow are where they rest; the places
  behind the snowman, the bird table, the lamp post and the cabin's front are NO-STOP (a walk may go past,
  nothing stops there). Each animal makes an ENTRANCE, walking in from one edge (the bull first).
- The hares and the fox (`FoxAndHares.cs`) read Santa, Loud, Threat and each other's thumps. The fox pins Threat
  while it stalks or chases (reach 0.35 U, weight 1, every 0.5 s) and a weaker one while it trots (0.2 U,
  weight 0.4, every 0.8 s); a hare pins Friend at each of its two thumps (0.4 U, weight 0.6) and does not thump
  again for 20 s after a flight. The hares hear the fox, the fox is in reach of the hares, and the thump is how
  one hare's alarm becomes all of them bolting, with no herd code. A hare's Look is only the head (nine pages of
  the sit book, the body unchanged); a hare flees AWAY from the danger to a piece of COVER on the far side (within 60
  degrees of straight away), goes round its near end to the back and settles; the fox's stalk ends in a
  Watch (a braced pull-up) until the hare bolts or 4.5 s pass, and its sleep begins with a drawn curl. Every
  hare starts hidden behind a cover and comes out round its end one after another over the first 20 s. There
  are no holes in the snow any more (`ChristmasScenery.Covers`: shrubs, boulders, a stump, brush piles, each
  with a hiding place behind it, in the depth picture); the same goes for the mice below.
- The snowman (`YardSnowman.cs`) only listens: he reads Movement, Loud, Threat and Santa, and pins nothing, so
  nothing hears him. Every channel of his body (eyes, hands, mouth, squash) eases toward a goal the doing
  writes, which is why two doings that disagree about the head glide from one to the other. He is drawn
  with `DrawSmooth`, and his entrance is a wake-up (eyes shut for a second, then open, a small stretch).
- The birds (`FeederBirds.cs`) pin Movement when the whole flock flushes (0.4 U, weight 0.6, once per flush) and
  Food where a flung seed lands (0.3 U, weight 0.5, never within 8 s of the last). In reach: the hares, the
  mice, and the snowman if they sit within 0.4 U of the table. A bird's change of page is dissolved over
  about 0.06 s, so no change of picture is one frame.
- The mice (`LogMice.cs`) move in dashes of a whole number of strides, so a dash always ends on the page where
  all four feet are down. They pin Movement while they run (0.15 to 0.18 U, weight 0.3 to 0.35) and a Friend
  squeak (0.20 U, weight 0.4, at most once in 3 s); a Food notice sends a hungry mouse foraging. A mouse
  decides once per ACTIVITY (a wander, a sit, a wash, a hide behind a cover, a tussle), never per phase; a reversal is a
  drawn pivot on the spot; and a frightened mouse bolts for the nearest cover on the side away from the danger
  (or the log), goes round its end, and peeks out later.
- The otters (`Otters.cs`, `OttersMind.cs`) move in the flat plane of the ice and are drawn at nine yaws. They
  pin Movement at a belly slide (0.3 U, weight 0.4, once per slide), Friend when one bolts for the hole (0.5 U,
  weight 0.8, at most once in 15 s) and Food when one starts a fish (0.3 U, weight 0.8, once per fish). In
  reach: the mice that come to the near rim of the pond, and a hare at the strip left of the pond. Nobody is
  on the ice at the start: every otter is under it and its head comes up in the hole, the first about 1.5 s in
  and the others three or four seconds apart. A picture is a blend of up to eight stamped pages, so a turn
  between two yaw bins or a stop after a gallop is a dissolve, never a one-frame change.
- The squirrels (`Squirrels.cs`, `SquirrelsArt.cs`) are the shared library's squirrel (`Core/Animals/Squirrel.cs`, the
  body Thanksgiving's squirrels use) in a winter coat with a Christmas brain: down and up the pines in a spiral,
  the bird-table raid, caches in the snow, the log, stump and boulder tops. They pin Movement while they bound
  (reach 0.12 U, weight 0.45), a Call from a trunk (0.30 U, weight 0.35) and a Threat when one gives the alarm
  (0.3 U, weight 1); at a Threat they all run up the nearest pine. A squirrel never goes into the ground: its
  refuge is always a pine. Knob "Squirrels" (0 to 4, default 2).
- The predators (`Predators.cs`, `PredatorsArt.cs`, `PredatorsOwl.cs`, `PredatorsOwlArt.cs`): a wolf and an owl, VISITORS
  that are residents too and are constructed LAST, so they take the dice last and nobody else's luck moves.
  Nothing is on screen, updated or paid for until a visit begins. A visit is a script of steps (wolf: walk in,
  sniff, look, pick a prey, creep, rush, then a catch or a miss; or cross; or howl. Owl: cruise, perch, stoop on a
  mouse, a hare or a squirrel), and each step starts one doing, a run of drawn pages that starts and ends on a named
  shared pose (the transition table is the comment at the top of each file). NOTHING interrupts a predator; a visit
  ends early only if its prey goes away. They pin: the wolf stalking, rushing or carrying, Threat at itself (reach
  0.45 U, weight 1, every 0.6 s); walking in or out, Movement (0.25 U, weight 0.3, every 1.5 s); a howl, Loud
  (0.9 U, weight 0.6, once); the owl, Threat at its SHADOW's place (0.35 U, weight 0.9, every 0.6 s) once it is
  low, and at the foot of its perch while it sits on one. Knob "Predators" (0 to 300 percent, default 100: the gap
  between visits shrinks as it rises; 0 never). The prey being watched or stalked (`Stalked`) holds still: its mind's
  clock stops while it sits. No story begins while a hunter is out (`StoryDirector.Hold`). In this build the
  owl hunts mice and hares; the wolf crosses, howls or catches a hare (`WolfReady`; the hare its plan was made for is
  `Stalked` from the moment it sets out); the wolf's planned miss and the squirrel hunts are off. Hares: every choice of a landing place asks `PlaceTaken` (another hare sits there OR is on its way
  there), because the scene's crowd test only knows where bodies are, and two hares that chose one place half a
  second apart sat one inside the other.
- Santa (`SantaRig.cs`) is not a resident. His team is nine (Rudolph alone, then four pairs) on one set of
  pictures, each on its own gallop phase. The scene pins him as a Movement notice every 2.5 s while he is on
  screen (high in the sky, reach 1.6 U, weight 0.35), so the animals glance up rather than flee, and the calm
  board makes that one glance per animal, not one every few seconds. A flight comes every minute or two (a pause
  of 45 to 105 s after one). On the roof (`SantaDelivery`) only the sleigh and the rear pair land; Rudolph and
  the other three pairs hover in the air beyond the eave.
- WHO STANDS WHERE (`ChristmasScenery.Occupy`, `Crowded`, `NearestOther`, `TurnPage`): every resident writes its
  feet each frame and asks the finished list of last frame before it picks a place or steps, so nobody stands
  inside anybody; the herd keeps to `HerdBandTop` to `HerdBandBottom` and the small animals to `SmallBandTop`
  and below, with the snowman in the gap between. Any new animal on the snow calls `Occupy` and `Crowded`.
- ONE START (`ChristmasScene.Update`): no resident begins until `ResidentsStart` (6 s) has passed and every
  resident is `Ready`, and then all begin on the same frame; in a test (a seed is set) the start is pinned to
  `ResidentsStart`. Every resident makes an entrance. See `HOW_SCREENSAVERS_WORK.md`, "The scene is managed".

`Story`, `StoryDirector` and `DepthQueue` now live in `Core/Stories.cs`, for any saver. Thanksgiving still uses
its own older copy in `src/Thanksgiving/Stories.cs`: that is a known leftover, not a design. Two Christmas stories
(`src/Christmas/Stories/`) borrow residents the way Thanksgiving's fawn story does: `MiceTrimTree` LENDS the log
mice (they are inside the log and the resident draws nothing of them while it plays), and `SantaDelivery` takes
Santa's reins (`SantaRig.Take` and `Release`). While a story plays the scene sets `Lull` on every resident, so
each finishes what it is doing and keeps to quiet doings. `CarrotHeist` is a placeholder that never plays.

Rules for a silky scene (each came from a sighting of a tick or a pop; the plain-words story is in
`HOW_SCREENSAVERS_WORK.md`, "Silky"):
- A body that moves is stamped with `Sprite.DrawSmooth` (at a fraction of a pixel; `ChristmasScenery.Stamp` does
  it), never at rounded feet.
- A part that drifts slowly between flip-book pages (a head, an antler) is drawn with `Sprite.DrawBlend`, a
  dissolve between the two nearest pages, not the nearer page.
- Every pair of doings has a drawn way between them (a turn, a rise, a settle, a dive, a climb out); a doing never
  ends on one pose and the next begins on another.
- A transition of T seconds carries about 30 times T pages, so a page is held two frames at most at 60 a second.
- A glance is the head alone, and a resident's reaction is rationed by the calm board (section 10).
- Judge smoothness with the frame-by-frame pop detector before any look: a smooth motion changes the picture by
  about the same amount each frame, a pop by far more in one frame than in the frames either side.
  `PECKWORKS_NOSNOW=1` leaves out the falling snow and the glitter so only the animals change the picture.
BODIES TRAVEL TOO: minds are shared in `Core/Minds`, and so are approved bodies in `Core/Animals` (today the squirrel).
An animal the owner has approved moves there, and the next saver reuses it with a new coat and a new brain, never a
new drawing.

Recipe: a HUNTER AND ITS PREY (Christmas's wolf and owl; any saver can use it). The hunter knows nothing about a
prey kind except two questions, and the prey kind offers three things:
- The hunter asks `InOpen(i)` (is prey number i out in the open where it can be taken now? not hidden behind a
  cover, up a pine, on a perch or already taken) and `FeetOf(i)` (where are its feet, so the hunter aims at them and
  is stamped in the right row).
- The prey kind offers `Seize(i)` (take it: it leaves the field, true if it was still takeable), `Restore(i, after)`
  (bring a newcomer back after that many seconds, so the field refills) and `Stalked(i)` (the hunter is creeping on
  it: it may notice, rear up and flee). A hare, a log mouse and a squirrel all offer these.
A new prey kind adds those five members and one line in the hunter's prey switch; a new hunter uses only them.
The hunter pins on the noticeboard (Threat while hunting, Loud for a noise) and never reaches into a prey's mind.
Test aids: `PECKWORKS_PREDATOR` forces a visit at 6.5 s: `wolf` (a kill), `wolfmiss`, `wolfhowl`, `wolfcross`,
`owlmouse` (a catch), `owlhare`, `owlmiss`, `owlperch`, `owlcross`; `many` makes natural visits every minute or so
for whole-field runs, and `off` turns them off. `PECKWORKS_SHEET=predators;<file>` writes the pose sheet.
Test aids: `PECKWORKS_STORY=1` starts the first story at 4 s (0 never plays one), `PECKWORKS_STORYPICK=<name>`
plays one story every time, `PECKWORKS_STORYLOG=<file>` writes a line per story begun, and `PECKWORKS_MINDLOG=<file>`
makes every Christmas resident and the Santa rig write their places and doings.

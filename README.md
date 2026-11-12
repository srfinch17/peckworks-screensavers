# Peckworks Screensavers

Native Windows screensavers written in C# (.NET 10, Windows Forms), plus a small reusable engine
for making more of them. So far: **Matrix Rain**, **Sakura**, **Sakura Dusk**, **Sakura Pond**,
**Halloween**, **Christmas**, **Thanksgiving** and **Cabin by Stream**.

## Matrix Rain

A close replica of the "digital rain" from *The Matrix* (1999): mirrored half-width katakana,
near-white flickering leading characters, fading green tails, characters that mutate in place,
drops at varied speeds and brightness, and a soft phosphor glow.

![Matrix Rain](docs/images/matrix-rain.png)

Settings: speed, density, character size, glow.

## Sakura

White and pink cherry blossom petals drifting down like snow over a painted scene of Mount Fuji
across a lake, framed by branches in full bloom. Stylized rather than photorealistic.

![Sakura](docs/images/sakura.png)

- Petals tumble, spin, sway, and ride a breeze that rises and settles.
- Depth: near petals are bigger, faster, and more opaque than far ones.
- Some petals let go of the blossoms on the branches; light glints on the lake.
- Trees stand on two banks of land in front of the lake, with a pine behind the grove.
- A small wooden boat with a roofed cabin, a boatman in a straw hat and a red lantern drifts
  slowly across the lake, passing behind the banks and trees.
- Branches and trees are grown randomly, so each run looks a little different.
- Twenty small **happenings** come and go at random, one every 5 to 12 seconds:

  | Where | What happens |
  |-------|--------------|
  | Fuji and the sky | A lens-shaped cap cloud (kasa-gumo) forms on the summit. The mountain blushes red, like Hokusai's Red Fuji. The sun crests the summit and blazes into a "Diamond Fuji". Origami paper cranes glide past. A painted kite dances on its string. A rainbow arcs over the lake. Cloud shadows drift across the mountain and the water. |
  | The far shore and the lake | A shinkansen races along the far shore. A sun shower glitters down and dimples the lake. A raft of fallen petals (hanaikada) drifts by. A mother duck leads her ducklings across. A kingfisher dives for a fish. Swallows skim the water. |
  | The banks | A heron stands in the shallows and strikes. A Shiba Inu trots along the hill, sits and smiles. Butterflies dance around the blossom trees. |
  | The branches | A pair of white-eyes (mejiro) visits the blossoms. A squirrel scampers along a branch. A glass wind chime (furin) swings and rings. |
  | Everywhere | A gust tears a blizzard of petals (hanafubuki) off the branches and sweeps it past you. |

Settings: number of petals, fall speed, breeze, petal size, and how often a surprise happens
(0 turns them off).

## Sakura Dusk

The same slowly falling petals over a different painting: a pond at sunset, with a low sun
setting behind hazy hills, a pagoda on the far shore, an arched wooden footbridge, a stone
lantern with a glowing window, and cherry trees on both banks. A small flock of birds flaps
slowly across the sky, each bird on its own rhythm, gliding now and then.

![Sakura Dusk](docs/images/sakura-dusk.png)

- Petals are tinted by the evening light, and golden glints twinkle in the sun's reflection.
- The nearest hills, the pagoda, and the bridge are mirrored faintly in the still water.
- The same boat as Sakura's, here a small silhouette far out near the opposite shore.
- Twenty happenings of its own, none shared with Sakura:

  | Where | What happens |
  |-------|--------------|
  | The sky and the sun | The sun's top edge flashes green. Rays of light fan up from the sun. The first star of evening appears and twinkles, then others. A crescent moon sinks behind the hills. Golden clouds drift by. The light deepens into a richer "magic hour". A crane flies across the face of the sun. |
  | The hills | A procession of fox lights (kitsune-bi) winds along the hills. |
  | The pond | Paper lanterns float across the water (toro nagashi). Koi glide under the surface. A lotus opens. Mist gathers over the water. A breeze ripples across the pond. A frog leaps in, as in Basho's haiku. |
  | The bridge and the banks | A cat crosses the bridge and sits to watch the sunset. Red paper lanterns light up along the rail. A tanuki waddles along the bank and tries to catch a petal. The cherry trees light up from below (yozakura). |
  | The air | Fireflies rise from the banks. Red dragonflies dart over the pond. |
- The same settings as Sakura (petals, fall speed, breeze, petal size, surprises), saved separately.

Sakura and Sakura Dusk share one petal engine and one set of painting brushes (banks, trunks,
blossoms, branches) in the Core project, so they have the same illustrative hand.

## Sakura Pond

A koi pond seen from above, the way you see one leaning on the rail of a bridge. The pond is
bigger than the view: still water fills the screen under cherry branches in full bloom, and the
only shore in sight is a corner of lawn at the bottom (left or right, the seed decides). Rocks
of every size sit along the water's edge, each with a sunny face, a shaded face, moss and lichen,
a dark wet flank just inside the rim where the water meets each one and a dark thread in the
water round its foot, so the water goes round every stone and stops at the ground (a stone on the
bank is never tinted along the bank's own curve); between the stones the water LAPS at a strip of
sand and pebbles, a swell travelling along the bank that runs up the sand a finger's width with a
thread of foam at its edge and drains back slowly, leaving a dark wet band that dries in a few
seconds, climbs the foot of each stone the same way, and washes further up when a ring on the
water (a koi's gulp, a frog's splash) reaches the bank; a lizard or a mouse drinking at the edge
backs off when a swell reaches its nose; clumps of iris leaves stand on the lawn behind the
stones, arching out over them toward the water; a clump of cattails stands in the shallows, brown
heads nodding; lily pads and water lilies float in the calm water nearby, each on its own stalk
like a balloon on a string: a fish, a turtle, the snake or a duck that swims into one shoves it
aside a little and it eases back over its root, and a pad with a frog or a dragonfly on it stays
put; and the branches throw dappled shadows on the water. The pond bed is ground with a shape:
humps and hollows lit on the side that faces the sun and shaded on the other, seen through water
that darkens with depth but never hides the bottom; the island and the rock formations are
boulders standing on it, each a smooth mound lit the same way, a solid grey stone going down into
the murk the way a deep fish does, darker and bluer the deeper it goes. The lawn is a meadow corner in a breeze, different on every seed:
lush and sun-baked patches, a worn spot or two where the earth shows, drifts of clover and
velvety moss, tufts of grass in three heights and three greens with dry tawny ones among them,
taller grass gone to seed along the edges, and drifts of wildflowers (daisies, buttercups,
dandelions and their seed clocks) that nod on their stalks. A gust crosses it as a soft, patchy
sheen of pale bent blades, each gust its own shape, and the tufts lean over and spring back as it
reaches them. There is ONE wind: the lawn, the tufts, the flowers, the cattails, the irises and
the falling petals all read the same gust at the same moment, so a gust crosses the scene as one
travelling wave (the cattails and irises sway with it stiffly; each iris leaf shows a lit half, a
shaded half and a pale midrib, the way a sword leaf folds). The cherry boughs sway in it too, the
tips swinging and settling like springs while the thick wood near the trunks barely moves, and the
petals in the air follow the eddies: a petal curves one way, then the other, spins faster in a gust
and drifts in a lull, so no two fall the same way.

![Sakura Pond](docs/images/sakura-pond.png)

- **Koi** of eight real varieties (kohaku, sanke, showa, yamabuki ogon, chagoi, asagi, tancho,
  plain red-orange) cruise the whole pond: a few tail beats, then a long glide. Each body bends
  through its turns along the path its head swam, never flipped or slid. They swim at real depths:
  the pond has a bed that slopes up to the bank and to every rock, a fish is never deeper than
  the water under any part of its body and rises up the slope before the shallows (and keeps off
  a stone's shoulders, where the water would be under half the pond's depth; since round thirty-nine it feels those shoulders coming by a room grid of its
  own, so it turns before a stone instead of nosing against it, creeps up a stone's flank instead of freezing
  on it a frame at a time, and lets its tail go quiet when it is held; since round forty-three that grid is
  stacked by depth, so every slope of the bed and every mound is felt the same way, at the depth the fish is
  at). The fish has a HEIGHT
  (a carp's body is about three tenths of its length deep, a goldfish a little more), and the bed
  rises gently to the bank over a third of the screen's unit, so a fish heading for the shore swims up
  the slope, then its back and dorsal fin break the surface (drawn clear of the water's haze, with a
  dark ridge and a wet gleam) and it ploughs a real V of bow waves, and where the water is under
  three quarters of its height it can go no further: that is the shore to it, as to the turtle. Now
  and then a koi goes up the slope on purpose (a visit to the shallows, a few seconds nosing in the
  sand with its back out, then away). And the open pond's bed is not one depth: humps and hollows
  take it a fifth either way of its usual depth, so the fish on the bottom are deeper in some places
  than others. THE BED IS A CARPET OF THINGS, not a painted floor (round forty-four, from photographs of real pond
  bottoms): olive silt with a fine grain, gravel bars of fist-sized stones each lit on top and shaded under, filmed
  with algae, a heap of rubble round the foot of every boulder, last year's leaves lying flat in five browns, the
  odd dark twig, and soft patches of algae; and the water itself is a pond's green, olive in the shallows to bottle
  green in the deep, not a swimming pool's teal. A
  deep fish
  sits lower on the screen than the water above it (we look at the pond at a slant) and comes up
  the screen as it rises, and in the shallows it throws a shadow on the bed, a soft echo right
  under it that fades as it rises off the bed (never thrown far: a shadow a body length away
  reads as a fish hovering above the water). A deep fish is
  smaller, softer, darker and duller than one at the top (water eats the warm light first, so a
  red koi on the bed is a dull brown-red and a white one a pale blue-grey, both still plainly
  fish: the water's own haze over them is slight), so when one passes over another you
  can tell from above which is over which, and the near one leaves a soft dark echo of itself
  on the deeper one's back where they overlap. They slip under the lily pads, and now and then
  come up to gulp at the surface, leaving a ring. A fish swimming with its back at the surface
  pushes a small bow wave ahead of its nose, a thin V of faint ripples trailing behind it (the
  turtle, the snake and a swimming frog leave one too): the one thing in a calm pond that says
  where the surface is, so the topmost fish read as just under it, not lying on it. Even at the
  top a fish takes a little of the water's colour. The pond is deep enough to hide a fish: the
  fish swim at any depth the bed allows, go smaller and hazier the deeper they are, and then out
  of sight; the deeper, the dimmer its own colours (darker and greyer) while the water over it veils
  it a shade less than it did. They steer over, under and round each other in three dimensions, and if two do touch
  they bump (a short recoil, then a shy swim away) rather than pass through. The 3D navigation
  kit behind this lives in the engine (`Core/Minds/Space.cs`) for other savers to use. A diving
  fish tilts: every point of its body is as deep as its head was when it passed there, so the
  head goes smaller, hazier and lower on the screen before the tail does, and a fish diving out
  of sight goes head first, tail last; one hanging still levels out. Hovering, its side fins
  scull by turns, one sweeping back as the other comes forward, the slow treading that holds a
  koi in place; swimming, they beat together. The sun gleams along its back, down the backbone
  when it heads toward or away from the sun and out on the sunward flank as it turns across the
  light, brightest on the metallic varieties and fading with depth. Nothing in the pond passes
  through a floating petal: whatever has its back up at the surface (a koi gulping or brushing
  it, a paddling turtle, the snake) shoves the petals in its way aside, and they glide off and
  slow; a fish swimming under them leaves them be.
- **An island rock** stands out in the open water: a boulder on a mound of the bed, darker and
  bluer down its side until the shoal round it takes over (never see-through: it covers the bed), with a small dry cap breaking the surface:
  the cap's rim is wet, the stone just under the surface round it is a lit grey shoulder,
  and the stone is drawn sunk by its depth the way every fish is, so the dry top is the top of the
  same boulder and nothing swims between them; a fish deep behind the cap is hidden by it. Two
  small rock formations stand in the water as well, one toward the lower left and one toward the
  upper right, each a stone going under with two or three little dry caps. The pond is a little
  deeper than it was (and a tiny bit more again), and its bed is uneven: broad humps and hollows that the water's colour, the
  bed's clearness and every swimmer's depth all follow. Anything that swims deep loses its colour
  with the depth, darker and greyer the further down it is, before the water's own colour veils it.
  The koi swim up and over its shoulders and round the cap, and the water is clear enough now
  that the bed and its stones show through the deepest part.
  The turtle rows as a pond slider does (the diagonal pairs together, a long push, a folded leg
  on the way forward) and coasts between bouts; the koi, the goldfish and the turtle go down to
  the bed to forage and dive far more often. Once in a while one rises slowly until its back brushes the
  surface from below, and three thin, slow rings slip off it as it glides on.
- **Goldfish** (three by default, a "Goldfish" knob from 0 to 6) share the pond with the koi: a short, deep-bodied
  wakin under half a koi's length, orange-red, red and white, or a pale calico, trailing a long see-through forked tail
  that flutters behind it. It is the same fish in the same water (same body, steering, depth and bump rules), but it keeps to
  the sunlit top of the pond, beats quicker, nibbles at the surface twice as often, flinches from a swallow's shadow from further
  off, follows other goldfish far more readily than a koi, never chases a koi, and idles in the neighbourhood of a shoalmate,
  so three of them drift about as a loose little shoal. Now and then one DARTS: two to four quick zig-zag bursts near the
  top, the way a goldfish in a bowl does, and pairs of them chase each other twice as readily as the koi do; and every
  goldfish keeps a little under the surface with a touch more of the water's haze on it, so it never looks to be
  sitting on top of the water. Every fish's idle drift reaches deeper than it did.
- **A catfish** (one by default, a "Catfish" knob from 0 to 3; round forty-four) lives on the deep bed: a bullhead
  as long as a koi, broad and flat with a wide head, dark olive mottled with darker blotches and paler patches, four
  whiskers that sway as it swims, and no bright colour anywhere. It is the koi's body and mind a third time, with a
  bottom fish's nature: it starts deep and asks for no depth shallower than three quarters of the pond's, never
  gulps at the surface or leaps, never comes up to look at a petal or to visit the shallows, follows and chases no
  one, and cruises slowly along the bottom (half a koi's pace), foraging in the silt more than any koi and resting on
  the bed for long spells. Its feelers see as wall only what it can never ride over (bed under six tenths of the
  depth: the bank's slope and the boulders), and it never answers a wall with a climb; the bumps and the mounds'
  skirts it follows with the bed. Its dice are its own, so the knob moves no other fish.
- **Each koi has a mind of its own.** Three personality dials are dealt when it is born (spread
  across the school, so near-twins are rare): how
  energetic it is (how much it beats rather than glides, how fast it cruises), how sociable, and
  how curious. From those it decides, one thing at a time, what to do next: wander, rest deep and
  still, go down and forage along the deep bed (half a minute of slow cruising from one deep spot
  to the next, a pause at each with its nose in the silt and a puff of it rising, a hazy shape
  doing something down where the pond is darkest), go and nose at something (a floating petal, which it may gulp down, the rim of a lily
  pad, the foot of the cattails, the side of the floating log, or the spot where a dragonfly just
  tapped the water), swim along behind and beside a friend, or now and then burst after another
  fish, which speeds up and swerves away until the chaser loses interest. Only one chase runs in
  the pond at a time, and a curious fish answers a dragonfly's tap far more often than a dull one.
  Each koi also keeps a liking for every other fish: it starts from how alike their dials are, grows while
  the two spend time close (faster when one follows the other) and fades with a half-life of twelve minutes
  when they are apart, so pairs and trios form and drift over an afternoon. A fish with a friend follows it
  far more often, for a minute or two instead of ten to thirty seconds, keeps pace with it, idles toward the
  friend's destination and rests beside it. Two fish may follow each other, each keeping its slot beside and
  behind the other, which is how a pair keeps station for half a minute or more; never more than three keep
  company, and the bump and shy rules are unchanged.
  When a swallow skims low over a fish, its shadow makes the fish dart away and dive, and it sulks
  deep for a while; a bold fish (energetic and sociable) barely notices, a timid one flinches at a
  shadow some way off. And now and then a koi LEAPS: a burst up from an unhurried cruise, half a
  second clear of the water (it rises up the screen with its shadow left on the water, the way
  a falling petal or a perched turtle shows its height), a splash of rings and flung drops, and
  the fish nearby flinch from it; then it dives to get over itself. About one a minute or two
  somewhere in the pond, more from an energetic fish, never from a goldfish. Once in a few minutes a big koi
  near a school of minnows, with the school ahead of its nose, TEASES it: a real dash of about a second at the
  school (half as fast again as any other rush), and as its head reaches them the school EXPLODES, every minnow
  bolting straight away from the head so the shoal bursts into a ring, then gathers itself again a little way
  off and drifts on; the koi glides to a stop, turns slowly away and rests or wanders on;
  it never catches or eats anything, and only one fish plays at a time. The lizards and the snake think
  the same way, on the same shared parts
  (`Core/Minds`); how to give any animal in any saver a mind is [docs/MINDS.md](docs/MINDS.md).
- Two **schools of minnows**, a dozen or so tiny fish each, hang just under the surface and move
  as one: all facing the same way, fidgeting in little darts, flashing silver when the whole
  school turns, and scattering like thrown rice when a koi cruises underneath. Each minnow is a
  bending body on its own trail, like the koi in miniature. A minnow that meets the island, the log
  or a bank rock between it and its school turns and swims along the stone to get round it (its
  tail only beats when it is actually moving), instead of nosing the rock. About one drift in sixteen is a
  DIP: the school swims briskly down to a spot on the deep bed (half the time near one of the deep dwellers
  below), sinking no faster than its swim can hide, grazes there ten to twenty-five seconds (drawn under every
  koi, hazed and shrunk by its depth) and comes up on its next drift.
- **The dragonfly's game**: every so often a dragonfly flies over to a school, hovers, then skims
  low and taps the water three times in a short line, each tap a small ring. The minnows rush to
  the rings in a body and circle under the spot, nipping at the surface where the dragonfly
  touched (a fizz of tiny rings). The dragonfly darts off across the pond, wanders, and comes back
  a little later to do it again, taking the schools in turn.
- A **turtle** (a red-eared slider, the turtle of nearly every park pond in Japan: an olive-green
  shell with pale seams between the plates, an olive head with thin yellow lines and the red patch
  behind each eye, and legs with webbed, clawed FEET, not a sea turtle's flippers) paddles just
  under the surface, front left leg with back right and then the other pair, with a small surge
  on each stroke, the toes spreading on the push, turning as one rigid piece in wide slow arcs, its head leading each turn on
  its neck. It steers in three dimensions like the koi: its feelers take it over, under or round
  a fish in its way, and where the two still meet (about ten times in three minutes) the fish is
  pushed out of it; its rise and sink while it paddles are held to what the paddling can hide,
  so it never slides up or down the screen. Now and then it stops, spreads
  its legs and floats up (a buoyant rise at one slow rate whichever way it faces: it grows and
  sharpens as it comes, which reads as surfacing), pokes its nose up for a breath (one small ring), drifts over to a petal
  and nibbles it, then paddles off, or
  dives to the bed for a while. A tadpole it comes upon while cruising or diving it may chase, half again
  as fast, and eat if its nose gets there. And it hauls out: it swims up to the island rock or the fallen
  log, climbs onto it, sits with its legs spread in the sun for fifteen seconds to a minute, and
  slips back in (sooner if the snake comes near, and then on the side away from it). Each turtle has its own dials: how much it loves the sun
  and how jumpy it is.
- **Frogs** (tonosama-gaeru, the Japanese pond frog) sit on the lily pads, the dry bank rocks and
  the bank's edge, olive green with brown blotches and a pale line down the back, two bulging
  eyes on top of the head. One of them is the grown frog; any others are young ones, two thirds
  its size, that croak only now and then. A frog sits still for half a minute at a time, its pale throat
  fluttering and now and then swelling in a croak (no sound), with the odd shuffle or hop round
  on the spot to face the water; a bold one snaps at a dragonfly that settles close, or at a froglet (or
  a tadpole up near the top) that comes within a snap of its perch, and eats it if the snout lands on it. Then it
  leaps: a crouch that gathers the legs in under it, the spring with the hind legs straight out
  behind, an arc a few body lengths long with its shadow left behind on the pad and then the water
  (the gap is the height), a splash, and a skim into the water with the legs still out, slowing,
  until the first kick. It swims with real frog kicks (the thrust that surges the body, a glide
  with the legs stretched out behind, then the legs drawn forward for the next; it steers in the
  kick; the arms stay tucked along its sides), floats with its legs hanging and its arms spread,
  treading water with a lazy half-kick now and then, steers round pads and the bank, and climbs
  out onto another pad or the bank with a short hop, settles, and turns to face the water in a
  hop or two. Frightened by any part of the snake, a koi's head, a turtle, a duck, a lizard or a
  swallow's shadow, a sitting frog leaps in and a swimming one dives (hazy and smaller as it goes
  down, back up with a tiny ring, and never up under the snake or a duck). A frog in the water is
  solid to the koi (they steer round it and a fish that meets it is pushed out), is pushed clear of
  the snake, the turtles and the ducks, and shoves the floating petals aside. Each frog has its
  own two dials: how bold and how restless. A frog in the water is IN it: only its head is clear
  of the surface, its back, rump, legs and arms are under the water's haze and go darker and
  greyer for it; its kick is a whip (the feet sweep out wide and close as the legs straighten) and
  each thrust leaves a small ring from each foot; a landing is a real splash (a big ring, foam,
  drops flung up that fall back with rings of their own). Frogs live on the bank and visit the
  water: a trip in lasts a dozen seconds or so, to the next pad or stone, not across the pond.
- **Tadpoles** hang in a loose cluster over the bed in the warm shallows near the cattails, tails
  sculling lazily, some grazing at a rock's flank, the bank or a pad's rim. Now and then one bursts
  (curls into a C, whips its tail and shoots a few body lengths, then stops dead); anything big
  coming near scatters them and they drift back together. One at a time, minutes apart, one grows
  up: buds, hind legs, forelegs, the tail shrinking, then a froglet that rises, swims off with frog
  kicks across open water (shoving petals aside) and leaves the screen for good. When the cluster
  has thinned, a new hatch of tiny ones appears at the cattails or a pad's rim. The cluster does not
  stay in one spot: every two to five minutes it drifts, as a loose cloud at a lazy pace, to another
  haunt (the shore shallows half the time, the island's shoulders a third, the outer shallows the
  rest; the way to the island is across water too deep to see the bed, which they cross in the
  sunlit upper water, so the koi may well find them there), and about once a minute one tadpole
  ventures off on its own, a few bursts out to a spot 0.1 to 0.2 of the screen away, a pause or a
  graze, and back. The knob "Tadpole
  clutches" (0 to 3, default 1) sets how many clusters. Tadpoles are too small to be solid to
  anything: they pass under fish and pads and flee them; a froglet at the surface is solid to petals.
  Everything under the water (tadpoles, a rising froglet, koi, minnows) is painted under the floating
  petals and under the heap of petals drifted against the bank. And they are food: an animal that
  happens to come near the clutch and sees them (a koi, a turtle, a frog on its pad, the snake, a duck,
  the crayfish) will sometimes single one out, chase it for a few seconds and eat it, or lose it; nothing
  goes looking for them, and each animal rests for minutes between tries. A thinned clutch hatches more.
- A **crayfish** (amerika-zarigani, the red swamp crayfish of every Japanese ditch) lives under a
  bank rock or the log and now and then comes out into the shallows where the bed shows: it edges
  out claws first, walks slowly on eight legs rippling in a wave, antennae waving, stops to feed
  (a claw reaching to the mouth over and over), puts both claws up in a threat, backs up a little,
  and, when a koi or a turtle passes over, escapes with the tail flip: the abdomen snaps under and
  it shoots backward two or three times, then sits with its claws up and walks home and slides
  back under the stone. It is drawn on the bed (sunk, hazed and shrunk like the fish above it)
  and is solid to nothing: everything swims over it. A tadpole within a few steps on the bed it walks
  at, briskly, and eats if its claws reach it; then it stands and feeds. Each visit has its own two dials, how bold
  and how hungry. The "Crayfish" knob sets how often it comes out (0 for never).
- A few **freshwater prawns** (the sujiebi, the see-through striped shrimp of a Japanese pond; the "Prawns"
  knob sets how many, three by default) live on the bed of the shallows near the stones that stand in
  the water. Each is about two thirds the length of the crayfish, nearly transparent with a faint
  grey-green cast so the bed shows through it, with a dark thread of a gut down its back, a few thin
  oblique stripes on its tail segments, two eyes on stalks, and two antennae twice its length that are
  never quite still. It stands and picks at the silt with its front legs for ten to forty seconds,
  walks a few body lengths on five pairs of legs rippling in a wave, now and then lifts off the bed and
  swims forward slowly before settling again, and, when a koi, a turtle or a duck comes near, escapes
  backward with two to four tail flips (the abdomen snaps under, a quarter second apart), sits still
  with its feelers up, and walks back toward home. It is drawn on the bed (sunk, hazed and shrunk like
  the fish above it), is solid to nothing, and each has its own two dials, how bold and how busy.
  A koi that is at least half hungry, or the turtle when it has not eaten for ten minutes, may spot a prawn within
  reach and go for it (the same chance meal as a tadpole); the prawn flicks away as it would from any fish, most
  chases end in a miss, and a prawn that is taken is back at its home from under the stones a few minutes later.
- A few **pond snails** (tanishi, the river snail of a Japanese pond; the "Snails" knob, four by default, 0 for none)
  creep over the bed: a coiled brown shell about a hundredth of the screen across with a paler whorl line winding
  round it, a soft grey-tan foot showing ahead of it with two short tentacles that wave as it goes. A snail's pace
  is a body length every twelve seconds, with stops of ten to forty seconds to graze, so one is noticed only by
  someone who looks, and then it is seen to be moving. It rides the bed (its depth is the bed's under it, so it
  climbs a submerged stone's mound and comes down the far side, drawn sunk, hazed and shrunk by that depth like
  everything else down there), turns at the land, a standing rock or a pad's foot, and is solid to nothing: every
  fish and every bed animal passes over it, and nothing eats it yet. On the deep bed, among the leaves and the
  cobbles, lie a fish skeleton or two (skull, vertebrae, swept-back ribs, a fanned tail, in old bone), what the
  catfish and the eel leave behind: painted into the bed once, like the rest of its litter.
- **The deep dwellers** (the "Deep dwellers" knob, four by default: every third from the first a crayfish, the
  rest prawns) live on the bottom of the deepest water, where the pond's haze leaves them about four tenths of
  their own colour: barely visible, on purpose. The crayfish is a signal crayfish (uchida-zarigani): brown-olive,
  bigger than the red one of the shallows, with a chalk-white patch at the hinge of each claw. The prawns are
  oriental river prawns (tenagaebi, "long-armed prawns"): see-through olive-brown with dark freckles, half again
  the sujiebi's size, with their second pair of legs grown into two thin arms longer than the body, each ending
  in a small pincer. They forage (standing, picking at the silt, a crayfish bringing a claw to its mouth), skitter
  in short brisk walks between stops, raise their claws at a fish that comes over or flick backward from it, and
  now and then two of them fight: they face off, lift and spread their claws with the feelers whipping, close until
  the claws touch, lock and shove to and fro with the stronger gaining ground, and the loser flips away while the
  winner stands tall. A school of minnows that dips to the bed near one can lose a fish to it: the near claw shoots
  out and snaps, and a hit is held in the pincer and eaten over a quarter of a minute (the school bursts away from
  the claw either way). And they are food themselves: one of the largest koi, hungry and already down on the bed,
  a hungry turtle on a dive, or the snake when it dives may single one out and take it, and a newcomer of its kind
  walks in from beyond the picture's edge a few minutes later. Each has its own two dials, how bold and how busy;
  they are solid to nothing and pass under everything.
- **Ducks** (off by default for now; the "Ducks" setting brings them back) swim by now and then: usually a mother with three to six ducklings, sometimes a drake and
  a hen, sometimes a lone drake, of one of four species (the eastern spot-billed duck, mallard,
  mandarin, common teal), each with its own colours seen from above (the mandarin drake's two
  orange sails, the mallard drake's green head and white neck ring). A duck comes in from beyond one
  edge over water, crosses by a few stops in open water, and leaves beyond another, in a minute or
  two. It paddles with its head rocking in time and a V of rings off its breast, looks about (the
  head turns, the body does not), dabbles (tips up, head under, tail clear of the water), preens
  (head back over the shoulder), flaps (the wings open from the shoulders, flung wide three times, a little spray, a tail wag),
  and stops and looks back for a straggler. The ducklings follow her trail in a line (two abreast
  when there are many), each pointing a body length ahead of its own place on her trail and
  matching her pace with a gentle closing on the gap, never more than a third faster than her
  cruise (each sways a degree or so with the paddle, once every 0.7 s or so: a fast four degree
  wag read as the ducklings shaking), and now and then one of them does something cute, never two at once: one
  lags to peck at something and scrambles to catch up, two bump and spin, one pecks a petal, one
  dabbles where the minnows flash, all dabble bottoms
  up, all bunch under her chin, or she flaps and they scatter and come back. If the snake comes near
  she herds them away. Every duck and duckling is solid to the koi, shoves the floating petals
  aside, and is a head the minnows, tadpoles and frogs keep clear of; a duck goes round the turtle,
  the snake's body and a swimming frog, the snake swims round the ducks and is startled by a touch
  from one, a swimming frog dives from any part of a duck, a duck that the snake, a frog or a
  surfacing turtle still gets onto recoils off it, and the two adults of a pair keep off each
  other. A grown duck paddling past the tadpoles' shallows may, rarely, single out a tadpole or froglet near
  its bill, swing round at it a third faster than a cruise and tip down for it with its neck stretched
  forward (eaten only if the bill gets there; a duck never goes looking for them, and not again for minutes).
  What may still overlap for a few seconds: a frog and the snake, a frog and the turtle, the
  snake and the turtle (none of them moves for the other), a duckling and a floating frog. The "Ducks" setting scales
  how often they come (100% is every four to eight minutes).
- **Petals** let go of the blossom, sway and tumble down, their shadows sliding in to meet them,
  touch the water with a tiny ring, then float, drifting slowly and gathering against the banks.
- **Dragonflies** (shiokara tombo, the pale blue dragonfly of Japanese ponds) hover, dart and stop
  dead, perch on lily pads, and the golden female dips to touch her tail to the water.
- **An old sunken log** on the deep bed, its bark still on: dark ridges running along the trunk, broken and
  wandering, cross cracks, a patch or two where the bark has fallen off and the paler wood shows, a knot, and at
  the near end the sawn face with its growth rings round the black hollow. It is part of the bed itself (the fish
  rise over it, the shadows fall on it, a stone in front hides it), and the hollow is the first deep dweller's den,
  so a crayfish is usually found at its mouth. The hollow is lit the way the sun would light it: the inner wall away
  from the sun catches it, the rest falls to black.
- **An eel in the log** (the "Eel" knob, 1 by default, 0 for none): a Japanese freshwater eel lives inside the hollow,
  lying with its head at the mouth, its gills working, withdrawing when a koi or the turtle passes over the door. Now
  and then it peeks further out, looks about, and slides out head first to forage in an S along the deep bed with its
  nose in the silt; a prawn or a minnow of a dipped school within a head's length gets a sideways snap. Then it swims
  back to the mouth, turns its tail to the hole and backs in, the body sliding into the dark until only the head shows.
  Alarmed in the open it flees for the hollow in a fast tight S. It is drawn inside the log by the pond's own depth
  test: what is under the wood is hidden, what reaches past the cut face shows.
  A rock in the water is a rock: the water meets its edge, and below it a short shaded foot of its own side shows
  through the water, the light net playing on it. A petal that floats up to a lily pad is held at its rim, and one that lands on a pad stays.
- **Sunlight and the surface** (the "Sunlight" knob, 100 percent by default, 0 for none): what a sunlit pond shows from
  above. A CAUSTIC NET on the bed: the ripples are lenses, and they focus the sun into bright branching lines, a net of
  cells that slides and merges all the time, sharp in the shallows, faint on the deep bed, gone in the boughs' shade,
  under everything that swims. A SHIMMER: everything under the surface is seen through the ripples, so the bed, the
  stones and the log sway by a pixel or two at a ripple's pace, which is what says there is water between you and the
  bottom. SPARKLES on the net itself: where the lines of the net cross, the surface flashes white for a moment, and
  the flashes come and go as the net slides, which is what the little flashes on a real pond are (the sun caught on
  one small wave, in the one spot your eye happened to be). Nothing drifts and nothing piles up. The open sun is a
  little brighter than the shade under the boughs, so the flashes belong to the day they are in, and the whole day, sun
  and shade alike, is a shade brighter again (round forty-seven). And every swimmer that breaks the surface (a turtle paddling with its shell awash, the snake, a koi with
  its back out) leaves a V-shaped bow wake, the cross waves held inside the wedge behind it. Light is solid to nothing.
  (The beams and the bed pools of 2026-10-30 were taken out on 2026-11-02: seen from above the water they moved the
  picture by under two grey levels, and what read as "light rays" was the bed's own slope lighting.)
- **Swallows** visit to drink: low, fast, sweeping curves with bursts of wingbeats and swept-back
  glides, skimming the surface with a line of rings, their shadows racing below them.
- Now and then a **snake** (one of four Japanese kinds, a different one each visit: the shimahebi,
  olive with four dark stripes; the aodaisho, olive green with soft darker blotches and a pale head;
  the hibakari, plain brown with a cream line along the neck; the karasuhebi, near black with a blue
  sheen down the back; drawn with a real head and neck, a rounded body lit in rows of scales, soft-edged
  marks, and only the head riding clear of the water) comes out of the grass, swims across the pond with its body weaving behind its head and a
  small wake, and slips away. It is
  not on rails: it feels its way, goes round the lily pads, the rocks, the log, the island's cap, the cattails and
  the iris clumps rather than over them, pushes the floating petals out of its way, and if a
  koi, a turtle or the lizard touches it, it startles: a burst of speed, a swing away, rings off its head, then it calms. Half of
  its swims about the pond are dives: it goes down head first to a third to seven tenths of the pond, the body following, drawn
  smaller and hazier under the fish, and comes up again before it hunts or basks. Down there a stone's
  underwater hill is a wall to it as the bank is: it feels the hill coming and turns early and smoothly
  round it, never tighter than its body can follow, and its body is laid over the bed under every point of
  it, bending up a flank as a rope would, never through the stone and never stretched. A turn on the
  spot bends its neck, in the water and on the land alike (on the land the bend was refused and each
  turn was laid into the body as a corner that slid down it, round forty-seven); hemmed between two
  bank things with no room on any side it probes a ring of headings with the step's own test and
  crawls out between them instead of standing there. The koi and
  turtles treat its body as solid (they steer under or round it, and a fish that meets it is pushed out), and the minnows keep
  clear of it as they do of the koi. Each visit is a different snake: it rolls a boldness and a
  nerve, and then chooses what to do by weighted dice, a few things per visit. It may swim out
  to a spot in open water and then another (explore), creep up on a school of minnows and
  lunge at it with a ring off its head while the minnows scatter (hunt), or, when the log is
  afloat, swim to the log's end, climb on, lie along it for 10 to 30 seconds in a gentle S and
  slip off the far end (bask). A bold snake hunts and basks more and calms sooner; a nervous one
  just crosses, startles at a longer reach and stays frightened longer. It always ends by
  leaving across the far side. A snake that has gone in among the bank rocks or onto the lawn (after
  a prey, see below) comes out the way it went in: it remembers its path as crumbs and walks them back
  to open water before setting off for its next goal, so it never paces the pocket between the rocks
  and the picture's edge. And on the bank it HUNTS: a lizard or a mouse that comes face to face
  with it (within 0.14u, ahead of it, while it is on the bank or at the water's edge) may be
  stalked at a creep, then it draws its neck back into an S and strikes. Sometimes it lands: the
  prey thrashes in the jaws, goes in head first over a few seconds, and a lump the size of the
  meal travels down the snake and fades; sometimes the prey was already gone (a lizard flings
  itself aside, a mouse jumps straight up) and the jaws close on nothing. A snake strikes at most
  twice a visit, and one that has eaten is slow and would sooner bask. An eaten animal is replaced
  by a new one (new temper) two to five minutes later. A snake that is only swimming across, with a
  tadpole or froglet at the top of the water right at its nose, may single that one out for a few
  seconds' chase and a lunge (never looked for, and it rests four to eight minutes after any chase).
  The snake also gets HUNGRY: a dial from full to famished that rises over eight to fourteen minutes of
  pond time and drops with a meal (a lizard or mouse to nothing, a tadpole a fifth, a minnow an eighth).
  A hungry snake comes out sooner, goes to look at prey from further off and creeps up on a school of minnows
  and really lunges: about six in ten catch one (the school goes on one fish fewer, and a newcomer swims in from
  the picture's edge three to six minutes later), with a quick gulp, a small ring and no bulge. A famished one
  may strike three times in a visit; a full one does none of it.
- Two **lizards** (kanahebi, the Japanese grass lizard with the very long tail) each come out on
  the bank every couple of minutes: a dash, dead still, another dash, over the grass and up onto a
  rock to bask, its body and tail throwing an S-wave as it runs and its sprawling legs (a knee bent
  forward, five long clawed toes spread like a hand) stepping on opposite corners, then gone into
  the grass again. Each has its own temper, rolled once: a BOLD one stays
  out longer, basks longer and wins a standoff, an ENERGETIC one makes longer dashes with shorter
  rests. A lizard may run through the irises' leaves but never stops under them (the leaves are drawn
  over it, and one basking on a rock beside a clump showed as legs sticking out of the stone), so
  every rock it climbs, spot it runs to and place it drinks is out from under a bank plant. A dash
  begun facing the water turns on the spot first, then runs (on a bank stone's top every dash had
  taken its first step into the water and ended there).
  They also run to the water's edge and drink (one tiny ring in front of the nose), creep
  up on a dragonfly sitting on a bank rock (it takes off a body length short), bolt for cover
  when a swallow's shadow sweeps over them, and if
  both are out and meet, they square up nose to nose, then the bolder one charges and the other
  bolts away and leaves. If the snake comes close they run for the nearest edge, but only if
  they have SEEN it: one that was basking, drinking or facing away is caught up with, and some
  freeze instead of running. A strike that lands holds the lizard in the snake's jaws; one that
  misses makes it fling itself sideways in a single leap.
- A **field mouse** (akanezumi) lives in the bank's grass. It comes out at the screen's edge for
  half a minute or so, then goes: a plump brown oval with a round head, two pink-lined round ears,
  bead eyes, four tiny feet and a thin tail nearly as long as itself. It moves in scurries of a
  third of a second, tail streaming, and between them stops dead to sniff (the nose twitches), sit
  up on its haunches, groom (a quick shiver) or nibble with its head down. It keeps to cover: it
  prefers the grass and the foot of a rock or the log, crosses bare sand only when it must, now and
  then runs along the log, and goes to the water's edge for one tiny sip. It is prey: the snake's
  head, a lizard's nose or a swallow's shadow sends it bolting for the nearest grass, where it very
  often vanishes (a short fade) for 10 to 40 seconds and comes back out at the same spot. Each has
  two dials rolled once: a TIMID one flees sooner and from further, freezes more and leaves
  sooner; a GREEDY one stays longer, nibbles more and drinks more.
- A small **rock formation** stands out in the water toward the lower left, different every time:
  one small stone going under, painted and wrapped like the island's, with two or three little dry
  caps breaking the surface, each cap a rock to every animal (the fish rise up and over its
  shoulders and round the caps).
- A fallen **log** can lie at the lower left instead (the "Fallen log" setting, off by default),
  different every time: beached across the water's edge with one end in the grass and the other
  in the shallows when the land is in that corner, or drifting out on the water when it is not.
  Bark streaks, cracks, a knot or two, moss on top, and the cut end's rings where it faces us.

Settings: draw width (sharpness against smoothness), petals, number of koi (ten by default), number of goldfish (three by default), number of catfish (one by default), how playful the koi are (0 means they only cruise and rest),
number of minnow schools (0 means no game), number of turtles, number of frogs (two by default),
number of dragonflies, how often the swallows visit, how often the snake crosses, how many
lizards there are (0 to 3), how often the lizards come out (Lizard visits), how many field
mice live in the grass (0 to 3), how many clutches of tadpoles wriggle in the shallows (0 to 3,
one by default), how often the crayfish comes out from under its rock, how many crayfish and long-armed
prawns live on the deep bed (four by default), how many snails creep over the bed (four by default), and how
often a duck family swims by (never, by default).

On a screen wider than the "Draw width" setting (1920 pixels by default) it draws at that width
and Windows enlarges the picture: the scene is soft and painterly, so the enlarging loses little,
and fewer pixels is what keeps a steady 60 frames a second on a 4K laptop (2560 is sharper and
runs at an even 30 there). If a frame cannot be drawn and shown inside one screen refresh, the
engine holds an even 30 frames a second instead of juddering between 16 and 33 ms gaps. Since
2026-10-20 it holds 60 from the first second: every method is compiled at full strength before
the first frame (tiered JIT off, and a start-up pass that compiles the whole programme on a
helper thread), nothing is compiled or sorted on the frame an animal first appears, and the
ripples are painted on several cores. On the 4K laptop at the default width that is 298 of 300
on-screen frames at one refresh, where it was 153 before.

## Halloween

Autumn leaves in gold and red drifting down over a moonlit graveyard hill: a big full moon, a
haunted house with lit windows on the far hill, a dead tree, tombstones, a waving skeleton, autumn
trees, and bare branches clawing in from the top corners.

![Halloween](docs/images/halloween.png)

- Bats flap across the sky in three sizes, swooping as they go.
- The jack-o'-lanterns flicker like candles, and the skeleton waves.
- The leaves are the Sakura petal engine with a leaf outline, and they let go of the autumn trees.
- Eighteen small **happenings** come and go at random, one every 5 to 12 seconds, shuffled like a
  deck of cards so you see them all before any repeats:

  | Where | What happens |
  |-------|--------------|
  | The sky | Halloween fireworks (purple and orange, or green) burst and sparkle. A shooting star. A bolt of lightning. Stars join up into a constellation (a bat, a pumpkin, a ghost...). Three little ghosts float past in single file. |
  | The moon | The moon turns blood red. A witch on a broomstick flies across it. |
  | The far hill | The haunted house's windows flicker out and come back green. A cloud of bats pours out of its tower. Something huge with glowing eyes peeks over the hill. |
  | The graveyard | A ghost rises from a grave and says boo. A zombie hand claws up out of the ground. Will-o'-the-wisps drift among the stones. The skeleton's eyes glow red. A jack-o'-lantern changes its expression. A black cat trots through. |
  | The trees | A spider spins a web in the dead tree, strand by strand. Another lowers itself from a branch on a thread. Pairs of eyes open in the dark. |

Settings: number of leaves, fall speed, breeze, leaf size, number of bats, and how often a
surprise happens (0 turns them off).

Adding more happenings, here or to another screensaver, is a recipe:
[docs/ADDING_HAPPENINGS.md](docs/ADDING_HAPPENINGS.md).

## Christmas

Christmas Eve at a log cabin in the north woods, rebuilt the way Thanksgiving was: a painted world with
animals living in it. Snow falls on a low horizon with a village glowing down in the valley and snowy
mountains behind it. A railway line with sleepers and telegraph poles runs across the valley floor. A big log cabin with warm windows and a smoking chimney stands among pines strung with
pastel lights that twinkle. In front lies a snowfield with a frozen pond (and a hole in its ice), a lamp
post, a fallen log, a bird table, a tiny fir tree and holly bushes, with big bare branches hanging in close from the
top corners, strung with fat pastel bulbs (the branches are the nearest thing in the picture, so they and their bulbs are painted 1.7 times the size of everything else). Scattered over the snow is COVER for the small animals to hide behind: red-twig shrubs under
snow, huddles of boulders, a stump and brush piles. There are no holes in the snow. Everything near is hidden correctly by whatever stands nearer than it, the same depth trick
Thanksgiving uses for its pumpkins.

![Christmas](docs/images/christmas.png)

- **The night is live.** The brighter stars twinkle, every lit window in the village wanders and now and then
  goes dark for a while (somebody went to bed) and comes back, about two village houses in three wear a string of
  tiny twinkling holiday bulbs along the eave and roof, the cabin's windows glow like firelight, smoke
  drifts from the chimney, the lamp post's flame gutters inside a halo, the snow glitters in flashes, and every
  bulb (pink, mint, baby blue, lavender, butter, peach) brightens and dims on its own rhythm. The snow is the
  Sakura petal engine drawing soft white dots, and a flake that falls past a bulb takes that bulb's colour
  for a moment, then fades back to white as it drifts away (the scene keeps a small map of where each bulb's
  light reaches, and each flake looks itself up on it).
- **The trees move in the wind.** The pines lean and the big corner branches bob, driven by the very same
  breeze that blows the snow sideways: when the flakes stream across the screen the trees bend with them, and
  when the snow falls straight they stand up again. The higher up a pine, the more it leans (like a fishing rod),
  the right grove feels each gust a moment after the left, and the corner branches never quite stop stirring.
  The bulbs' glows ride along with their bulbs, and a bird perched on a corner branch is carried up and down
  with the bough. (The test setting `PECKWORKS_WIND` set to `still`, `nopines`, `nobranches` or `count` holds
  the air still, leaves out one set of trees, or writes how many milliseconds each costs.)
- **Santa's sleigh crosses the sky** behind the overhanging branches, pulled by eight reindeer in four pairs
  with Rudolph alone in front, his red nose glowing. Every reindeer gallops on its own beat (no two are in
  step), a gang line runs down the middle of the team from the sleigh to Rudolph and sags and tightens with
  the beat, a gentle sparkle trail of stardust is left behind the runners, the team rises and dips along its path, and where it crosses the
  moon it turns into a silhouette. Rudolph's nose throws a faint red pool of light on the snow that slides
  across the field beneath him as he passes. A flight is an event, not a constant: the first comes about nine
  seconds in, and after each one there is a pause of 45 to 105 seconds (40 to 90 after the delivery story),
  so he comes by every minute or two. The flights vary (along the top or a long diagonal, left to right or
  right to left). The animals glance up as he goes over.
- **Reindeer** (a bull with a wide rack, cows, and from three animals up a calf) walk in from one edge of the
  screen when the scene begins (the bull first, the calf trotting to keep up), then graze on the snowfield:
  they paw the snow with a foreleg and then nose down into the hole, drift slowly forward a step and a scrape
  at a time, walk, look up, lie down together, shake the snow off, follow Santa across the sky with their
  heads and bolt for a short trot at a danger. Now and then the bull or a cow sets off for a place a few body
  lengths away and the other adults behind it fall in after it in single file, then spread out and graze
  again. Two of them touch noses, and the bull lifts his head right up and bellows, a plume of breath rolling
  off his muzzle. Four stretches of clear snow are where the herd lives: they walk past the snowman, the bird
  table, the lamp post and the cabin's door but never stop, graze or lie there. The calf plays, and now and
  then tries to fly like the ones overhead.
- **Snowshoe hares and a red fox.** The hares start out hidden behind the cover and come out round its end
  one after another over the first twenty seconds, then sit with twitching noses, wash their faces, hop, stand
  tall to look, and now and then spring into the air for joy. A glance is only the head coming up: the body
  does not change. The fox is a
  visitor: it trots in, listens for a mouse under the snow, leaps and dives in head first (a drawn dive, and a
  drawn climb back out with a face full of snow, leaving a soft dent, never a hole), sits, curls up to sleep
  under a thickening dusting of snow (a drawn curl), and goes away again. When it stalks a hare, the fox pulls
  up into a low watch pose with its ears on the hare, and holds it until the hare bolts or four and a half
  seconds pass. The hare rears up, thumps twice (the other hares hear it and bolt too) and zigzags AWAY from
  the fox to a piece of cover on the far side and goes round its end to the back. The fox gallops after it,
  trots up and noses round the cover (snow shaken off a shrub falls on its head) while the hare slips out of
  the other end and bolts for the next one. The hare always gets away. Hidden, a hare lies flat behind a low
  stone or sits behind a shrub, and every few seconds it peeks: ears first, then the head, a look each way,
  and down again.
- **A snowman** (1.8 m tall by the field's own metre, so he is in scale with the reindeer) stands in the near snow and never walks, but he watches. He wakes as the scene begins (eyes shut for a second, then open, a small stretch). He jumps at a bang and his hat flies
  off, tips the hat to Santa, shivers, catches a snowflake on his carrot, yawns, dozes off and wakes with a
  start, waves at the cabin, and shakes the snow off his arms.
- **Birds at the bird table**: cardinals, chickadees and at most one bossy blue jay. They land on the tray, the
  roof, the lamp's crossbar, the log, the tiny fir and the corner branches, peck, hop, sing, bathe in the snow,
  squabble, scatter when the jay comes and all go up at once at a threat. Every change of picture dissolves into
  the next over a few frames, so nothing flickers.
- **Mice in the fallen log** come out slowly (a nose, whiskers, a head that looks both ways), run in dashes
  with frozen stops between, sit up, wash their faces, fetch seeds, box nose to nose, and when frightened bolt for the nearest
  cover away from the danger (or back to the log), round the back of it, where they stay safe until they
  peek out and carry on. A mouse works through whole activities (a wander, a sit,
  a wash, a hide behind cover, a tussle) rather than a string of unrelated moves, and every change of facing is a drawn
  turn on the spot.
- **Otters on the frozen pond** come and go through the hole in the ice: they surface, haul out over the rim,
  gallop, run up and belly slide, stand up like periscopes, roll on their backs, eat a fish and wrestle. They arrive one at a time, each head coming
  up in the hole a few seconds after the last, and their pictures dissolve into each other, so a turn or a
  stop never snaps.
- **Red squirrels in the pines.** The same squirrel Thanksgiving uses (it lives in the shared animal library,
  `Core/Animals/Squirrel.cs`) in a winter coat. Each comes down a pine trunk head first, spiralling round it,
  hops off onto the snow and bounds away; going home it climbs back up into the boughs, and the bough it enters
  dips and sheds a little snow. On the snow it sits up, looks round, goes round the cover (never through it)
  and hops up onto a stump or boulder to eat a pine cone. It buries a cone in a shallow dish in the snow, and
  another that watched may dig it up and be chased up a pine. It raids the bird table, climbing the post onto the tray
  while the birds scatter. At a danger every squirrel runs up the nearest pine and scolds from the trunk.
- **A wolf and an owl visit now and then.** Nothing about them is on screen, or costs anything, until a visit
  begins. The wolf trots in, sniffs, looks about, and either creeps up on a hare or a squirrel and rushes it
  (it can catch or miss), crosses the field, or sits and howls. The owl glides across, perches and looks
  around, or stoops on a mouse, a hare or a squirrel, and it too can miss. A catch is told in a storybook way,
  with a tumble in a cloud of snow, and the creature taken is replaced much later by a newcomer. The rest of
  the field reacts: a stalking wolf is a threat the animals near it notice, and a howl is a loud noise that
  the animals nearby notice. Visits are rare and come on their own schedule, and no story begins while a
  hunter is out. (In this build the owl hunts mice and hares. The wolf crosses, howls, or creeps up on a hare and
  catches it, and when no hare sits where it can be reached the wolf looks about and leaves. The wolf's planned
  miss does not play yet, since a hare beside cover freezes where it should bolt, and the squirrel hunts are
  switched off.)
- They notice each other and the happenings. Fireworks and the toy train are loud, the owl is a threat, a
  sliding snowball is something to glance at; a fox stalking or a bolting reindeer is read by the animals near
  it. Nobody reacts to a kind of animal, only to what a notice says. The field is kept calm: after a glance an
  animal gets on with life for about a quarter of a minute, it gets used to a source it has just looked at,
  and only two glances may begin in any ten seconds across the whole field. Fear (a bang, a fox) is never
  held back.
- **Two stories** play now and then (one about every minute, dealt like a deck so each comes round before
  any repeats): *Santa delivers* (the sleigh and the rear pair land on the cabin roof, which is short, while Rudolph and the other pairs hover in the air beyond the eave; Santa climbs out
  with his sack and goes down the chimney, the windows flicker, he comes back up sooty and pats himself clean,
  and the team leaps off in a burst of stardust) and *the mice trim the tree* (three mice carry a bauble, a
  coil of lights and a gold star to the tiny fir; the lights come on from the bottom, and the trimmed tree
  stays lit for a few minutes). A third, *the carrot heist* (a hare steals the snowman's nose), is only a
  placeholder so far and never plays.
- Seventeen small **happenings** come and go at random, as in Halloween:

  | Where | What happens |
  |-------|--------------|
  | The sky | A shooting star to wish on. The northern lights ripple. Stars join up into a snowflake, a star, a tree, a candy cane or a bell. A firework show launched from the village (below). A gust of wind whirls snow across. A drift of golden fairy dust swoops through. |
  | The moon | An ice halo with a faint rainbow forms around it. |
  | Santa | A present tumbles off the sleigh and lands in the snow. |
  | Far off | A toy train with lit windows crosses the valley on its track, slowly, with a chuffing plume of smoke that hangs along the sky. A reindeer stands on the mountain ridge. |
  | The snow | A snowball rolls across, growing. A clump of snow slides off a pine. |
  | The cabin and the lights | Smoke rings (and a heart) from the chimney. A wave of brightness runs through every light. The star on the tree flares and showers sparkles. |
  | The corners | Frost crystals grow across a corner of the glass. A snowy owl perches on a branch and blinks. |

  (The fox, the rabbit and the snowman used to be happenings. They live in the scene all the time now.)

  **The firework show** lasts about 27 seconds and always has the same arc, with different shells each time:
  an opening of three single shells, one at a time; a middle (a snowflake, a pair of rings, the Christmas tree
  on its own so it can be read, then three overlapping bursts); a quiet beat of two seconds while the last
  smoke drifts; and a finale of volleys (two or three shells bursting at the same instant) in a rush, ending
  in one huge gold willow. Every shell climbs from the village in the valley as a faint trail, pops, and
  bursts. The kinds of shell are the peony (a round ball, usually red and green), the pastel (a ball of pink
  and violet, ice blue or gold), the willow (long gold stars that droop), the chrysanthemum (mostly white
  stars with some gold or silver), the ring (red, gold or green), the double ring, the five-pointed star, the
  six-armed snowflake, the Christmas tree (green, with baubles that turn red, gold or ice blue) and the cross
  (gold stars that each split into four). Each burst lights the sky, the mountains and the valley with a
  faint wash of its colour, and a wide, fainter wash of the same colour lies on the snow below it for about
  half a second.

Settings: amount of snow, fall speed, breeze, flake size, how often a surprise happens (0 turns them off),
how often a story plays (0 turns them off), how many reindeer (0 to 6, default 3), hares (0 to 6, default
3; no hares means no fox), birds (0 to 10, default 5), mice (0 to 6, default 3) otters (0 to 3, default 2), squirrels (0 to 4, default 2) and predators (0 to 300 percent, default 100; how
often the wolf and the owl visit, 0 means never),
and "Draw width" (1600 to 3840 pixels, default 1920): the widest the scene is drawn, enlarged by Windows on a
wider screen.

## Thanksgiving

Maple leaves drifting down over a farm at sunset: a sky from violet through orange to gold, the sun
sinking into the far hills, a big white two-storey farmhouse (a cross gable over the door, five lamplit windows with green
shutters, two chimneys, one smoking, and a full-width porch with a lamp by the door), a clothesline in its
yard with the washing left out and flapping, a doghouse past the scarecrow's garden, a dirt road curving
from the porch steps along the hill to the barn door, a pond off on the far slope holding the sunset sky
with cattails along its bank, a red barn with three silos (a tall one behind its corner, the old one
against the wall, and a squat grain bin with a cone roof) and a steel water-pump windmill whose wheel
turns with the gusts, a reaped field
with hay bales, a scarecrow in the garden beside the house and a small orchard past it, two rows of round
trees in their own autumn orange (the fruit long picked), a split rail fence, a corn shock, a
pumpkin patch that covers the whole foreground (pumpkins of three tints, gourds and broad leaves joined
by vine runners, one small one at the back carved into a jack-o'-lantern with a candle guttering inside it,
in homage to Halloween and kept subtle), maples in full colour, and oak branches with the year's last leaves in the top corners, two a side (a high one from the corner and a lower, flatter one), their wood handed to the scene as geometry so birds and squirrels can stand on it.

![Thanksgiving](docs/images/thanksgiving.png)

- A V of Canada geese crosses the sky, each goose on its own wingbeat, passing behind the corner
  branches.
- **The farm is alive up on the hill.** Two horses in a rail paddock past the silos graze, stand and
  swish, walk about and now and then trot the length of the rails; three cows graze among the trees
  left of the house and stand chewing; three pigs root, trot about and lie in the wallow of their pen
  between the orchard and the windmill; a rooster and four hens peck and scratch round the scarecrow's
  feet and scatter when the dog comes through. The farm dog lives in the doghouse: it sits in the yard,
  trots the road to the barn and back, and barks at the coyotes, the raccoon and the owl (the hens
  scatter, the turkeys look up). The dog has games too: it chases its own tail in a tightening spin and
  sits dizzy afterwards, and now and then it runs at the hens, who scatter with a hop while the rooster
  stands his ground and then charges it, and the dog slinks home with its tail down. The rooster crows
  every minute or two (neck up and back, beak open, a quiver), the hens look up and the dog woofs back
  or pricks up its ears; when the coyotes howl on the far lane the dog barks back from its kennel
  doorway. One cow at a time folds down onto the grass (front knees first) and lies chewing for a while,
  then rises hind end first. Every one of them has a mind from the shared kit (a doing with a timer
  and weighted dice, a pace that eases, a pen it keeps to), jointed legs on a proper walk cycle paged by
  the distance walked, a turn through five narrowing pages, a head that eases down to the grass and
  nods, and a tail on its own clock; and every one is sized by the hill's one scale (a 7 m house, a
  2.4 m horse, a 1 m dog, a 0.4 m hen), stamped behind the field and in front of the hill, inside its
  fence (the near rails are stamped over the animals). The cows' pasture is fenced like the paddock now (the back rails in the backdrop, the front rail stamped over the cows), and the pond past the paddock reads as water: no rim, the far edge fading into the grass, the hill's colours reflected in the far part and the sky's gold near, a soft muddy crescent at the near bank, reeds standing in the edge, and three ducks with little wakes.
- A flock of wild turkeys lives in the field behind the fence: they peck at the stubble, wander a
  few steps, stand and look about, and the tom fans his tail now and then; when he does, the
  curious hens walk over and stand watching him, and a bale dropped by the baler draws them over
  to scratch at the hay. With three or more turkeys there are two toms, and when one displays with the
  other near they square up: both fan and quiver out of step, then the smaller folds his fan, turns and
  slinks off with his head low while the winner stands fanned a moment longer. The fawn in the deer herd
  gambols when all is quiet: stiff-legged springs with her back arched and tail up, a dash out along her
  row in low bounds, a skid, a turn and the bounds back, then she stands and looks at her mother, who
  lifts her head and looks back.
- **The field has one scale.** Metres to pixels run from the tractor at the far lane (its rear wheel is
  1.7 m) to the fence posts at the front, and every animal on the field is sized from that and its real
  height or length, so a deer is half a tractor wheel tall and a rabbit a tenth of a tractor long,
  wherever it stands.
- **The field is being worked.** A red International Harvester of about 1980 (the long square hood with
  the black band along it, the boxy glassed cab with the white roof, the chrome stack, lamps in the black grille)
  drives slowly along the far lane towing a round baler on its drawbar. The two are hitched, not welded: the
  tractor heaves and trembles on its own, and the baler rocks about the hitch pin on its own wheel, the tongue
  staying pinned between them. Every so often the baler's tailgate swings up and a finished bale rolls
  out the back and is left standing on the field. It goes off one side, comes back the other way a
  while later and bales the next row, and when half a dozen bales stand loose it comes back towing
  the hay wagon and picks them up as it passes: as the wagon's tail clears a bale the loader swings it
  up whole onto the bed, and the stack grows three, two and one. The deer lift their
  heads as it goes by and the timid ones trot off; each bale it drops draws the turkeys over to
  scratch at the loose hay. The lane has bumps (four to seven humps and a dip or two, the same every pass): the tractor rides them on its own wheels, rising and tipping as each wheel crosses, and the baler or wagon behind it meets the same bump about a second later on its own wheel and tips about the hitch pin, so the two move as two machines joined at one point.
- **A pack of coyotes, and the rabbits they hunt**, on the far lane at the very back of the field. The
  rabbits are always there, tiny, sitting and grazing and hopping about. Every few minutes the pack
  trots in: the coyotes roam, stop, sit, dig at the ground, and now and then one gives chase at a
  gallop while a rabbit flees in long hops. Most chases fail; occasionally one is caught, and then the
  catcher shakes it, drops it, and the whole pack plays with the carcass for a minute, pouncing on it,
  tossing it in the air, tugging it between two of them, carrying it a few steps, before they trot off
  with it. And there is fake blood, because that is the joke: a bright red cartoon splat on the lane
  where the rabbit is caught, droplets flung off the catcher's mouth at every shake and toss, each one
  landing as a speck, all of it fading away a couple of minutes later. And sometimes, while they play, a big turkey
  vulture spirals down out of the sky, glides in low, snatches the rabbit off the ground and labours away with it hanging
  from its feet, leaving the coyotes standing with their heads up watching it go, then looking at each other, one sitting
  down, one shaking its head, before the howl and the leave. A tiny, hilariously horrifying
  thing way in the background of a serene fall scene, to reward the viewer who keeps watching. The
  rabbits are not fools: one that spots a coyote coming sits up tall, thumps a hind foot twice (a puff
  of dust each time) and bolts, and every rabbit in earshot sits up and bolts before the coyote is on
  them, so the whole lane empties at once. When a bout ends the boldest coyote sits, lifts its muzzle
  and howls at the sunset for three seconds before the pack leaves; the deer look up and the farm dog
  barks back. The whole field knows the pack is through: the deer lift their heads and the timid trot
  off, the turkeys hurry. Knob "Coyotes" (0 to 4; 0 keeps the rabbits).
- A windbreak of small trees in autumn colour stands along the far edge of the field, where the hill
  meets it: nearer than the hills' treeline, behind everything that moves on the field.
- A white-tailed doe, her fawn and a buck with antlers graze at the far side of the field, lift
  their heads to look about, and move off a few steps at a time, the fawn trotting after her.
- The turkeys stand on turkey legs: the drumstick up in the feathers, the hock (the joint that looks
  like a backward knee, really the ankle) just under the belly, a thin pinkish shank and long toes.
- **They have minds and senses.** The turkeys, the deer and the patch's small creatures run on the
  same shared kit as the koi pond's animals (Core/Minds): personality dials dealt at birth, one
  doing at a time, weighted dice for the next, and now SENSES. Whatever happens on the farm is
  pinned on a noticeboard (the tractor is loud as it passes, the raccoon a threat on the near ground,
  a scuffle a noise, a squirrel bounding past a movement) and every animal makes its own sense of
  it by its nature: the bold look up, the timid freeze with the head high, the fawn turns and
  trots off, a bolting deer sets the whole herd looking, a mouse dives behind a pumpkin. They move
  with weight too: every walk leans in over a few steps and runs down so the last stride lands
  with both feet planted, a standing head is never quite still, and no timer runs on a metronome.
- **The acorn caper**, every minute or two on the near ground: a tawny squirrel peeks out of the hollow
  in the left maple's roots, hops out, picks an acorn from the pile under the oak branch, climbs its
  tree to the fence rail, bounds along the rail (behind the corn shock), drops down, digs a hole with
  the earth flying, buries the nut, pats the mound flat, looks round, and goes home up the tree. A
  second squirrel comes head first down the right maple, sniffs its way along the rail, digs the nut
  up and sits holding it. The first squirrel sees from its tree, freezes, races down and over, and
  the two chatter at each other, tails lashing, then roll in a cloud of dust with tails and paws
  sticking out of it. The acorn is flung out of the scuffle, and while they are still rolling a mouse
  creeps out from the corn shock, sniffs it, takes it and scurries home. The dust settles; the two
  squirrels sit, look at where it was, look at each other, and each goes back up its own tree.
- **Four more little scenes** take turns with the caper, one every minute or two, dealt so that every
  one plays before any repeats. *The branch raid*: a third squirrel comes in along one of the oak branches
  in a top corner, runs out over the patch, sits up, and scrabbles at the wood until acorns shake loose
  from the leaves and patter down for the patch's squirrels; then it either loses its footing and hangs
  under the branch by its forepaws for a second, scrambles back up and looks about as if nobody saw, or
  sits and eats one it kept, before it runs back out the way it came. *Hide and seek*: the squirrel bursts out of its hollow after a mouse,
  which bolts down through the patch and dives into the jack-o'-lantern's grin; the squirrel pulls up
  beside it, sits up and looks about, sniffs along the pumpkin's foot and gives up, while the mouse's
  head rises in one carved eye, watching, and ducks; once the squirrel has gone it looks out of the other
  eye, creeps out of the mouth and scurries home. *The fawn and the apple*: an apple rolls out of the
  left edge of the picture and its last bounce takes it under the fence rail into the field; the fawn trots down to
  the fence, puts her head through the rail and eats it in three bites, then walks back to her mother.
  *The mouse parade*: four mice file across the patch with acorns in their mouths until a sparrow drops
  out of the maple and swoops low over the line; they scatter behind the nearest pumpkins, noses showing
  past the edges, while the sparrow sits on a pumpkin and looks about, then fall back into line.
- **A possum lives in the right maple.** Most of the time it is out of sight in the crown. Now and then it
  drops out of the leaves on its tail and hangs head down, swaying, looking at the patch and at us, opening and
  shutting its hands, then curls up and pulls itself back into the leaves. Sometimes it comes head first down the
  trunk and waddles about the patch, and if an acorn lies loose it walks over, takes it, sits up and eats it and
  wipes its face; then back to the foot and up. A threat on the ground (the raccoon, the coyotes' alarm) makes it
  do the famous thing: it keels over on its side with its legs stiff, mouth open, tongue out, lies there a while,
  peeks with one eye, gets up and shakes. It never shares the trunk with a squirrel going to or from its hole. It
  bends in the middle to climb (the front half rears up the bark with its hands reaching, then the hind end follows)
  and comes down the same way in reverse, and its tail is its own: a live chain of a dozen points that trails and
  swings behind it on the ground, hangs when it climbs, and drapes from the branch when it hangs by it.
- **The birds use the branches.** A junco or sparrow in the patch sometimes flies up to one of the oak
  branches in the top corners, flaring onto the wood with its feet forward, and sits there six to
  twenty-five seconds looking about, preening, pecking at the bark and hopping along the branch, then
  tips off and glides back down into the patch. A threat on the ground (the raccoon, a squirrel bounding
  past a timid bird) sends them up early and keeps them up longer.
- **The patch's own life**: field mice, juncos and sparrows and two squirrels live among the pumpkins,
  each with a small mind of its own (the koi pond's shared kit). Mice scurry between the pumpkins,
  sniff, nibble at one, hide behind one and peek; birds get about by FLYING (a few hops at most): a
  burst off the ground, a low bounding flight across the patch, wings beating then shut into a dart
  and beating again, a flare onto the ground or a pumpkin's shoulder, where they peck and look about
  on proper legs (a hock bent backward, a thin shank, long toes); the squirrel (drawn the way cute flat
  vector squirrels are: a rust-red pear of a body with a cream front, a round head with a big eye and tall
  tufted ears, and a smooth plume of a tail as tall as itself that hooks forward over its head when it
  sits and streams in an S behind it when it bounds) bounds through, sits up,
  scratches at the earth, and sinks into its run and sits up out of it
  through the crouch (never a one-frame pop); its bounds pulse (fastest in flight, slowest as it lands and
  gathers), every stop is a slow-in to the sit, a long run may pull up midway to sit and look before going
  on, and the head comes up into a look over a few pages; a mouse a squirrel bounds past bolts. Everything on the ground is hidden by the
  pumpkins nearer than it (a depth picture the painter makes), which is what puts them IN the patch.
  Knob "Patch critters" (0 to 12, default 6); they keep out of the way while a scene plays.
- **The squirrels are individuals.** Each wears its own coat (red, grey or fox squirrel) at its own size, and
  rolls four dials at birth: timid, busy, curious, playful. A playful one runs out and curls straight back at
  speed for the fun of it, hops up onto a pumpkin to sit on its top and look about, and romps after the other
  squirrel, which bolts a little way and is chased again, a few legs of the game, and then both sit up. A
  timid one flicks its tail more and dens more. A squirrel never hides behind a pumpkin: it hides in a TREE.
  The two inner maples are big old trees, thick tapered boles with bark and limbs dividing into the crown. Every maple's
  crown is a ring of leafy lobes, each shaded on its own: deep crimson underneath, orange on its body and gold where the
  low sun catches its top, with the lit side facing the sun (so the left trees are lit from the right and the right trees
  from the left); a long shadow lies on the turf away from the sun, and the roots run out from a rounded buttress. The
  squirrel runs to the foot, pitches up onto the bark by BENDING in the middle (the front half rears up the trunk with the
  hind feet still planted a stand-off behind it, then the hind end jumps the gap as the body swings vertical, half a
  second in all), and climbs the way squirrels climb: SPIRALLING round the trunk, out past the bark on the near side,
  behind it with only the tail showing past the edges, out again on the far side higher up, and up INTO THE LEAVES, which
  hide it. (It used to go into the hole in the trunk; a flat body sliding up a flat trunk and vanishing behind a line
  read as "levitate up into a dimensional portal", so the hole is just a hole now.) It stays a while out of sight in the
  crown, then comes head-first down out of the leaves, round the trunk the other way, and bends level onto the
  ground. On the move it never flips and never slides:
  a step into a pumpkin's foot goes round it on the open side; it never runs steeper than about 35 degrees on
  the screen (a side-view body going straight up or down the screen slides), so a goal far up or down the patch
  is reached by a diagonal to that row and a run along it; its heading bends rather than corners; and a change
  of direction is a curl at once (the front end plants, the back end comes round it with the tail whipped over,
  through the front view and off the other way, momentum kept, over half a second); from a standstill it rises,
  turns through real three-quarter views to a front view that is the same picture both ways round (a round body with a
  broad cream bib, both thighs splayed, two eyes; it used to narrow to a pillar, a card turned edge on), and sinks into its run.
- **The nuts stay on the ground.** Every acorn that falls from the oak branches lies where it landed
  until a squirrel comes for it; the squirrel sits up and eats it, or carries it off a little way, digs
  it in and pats the mound flat, and that stash is its own. A squirrel that finds another's stash may dig
  it up, and if the owner is about it comes running while the thief bolts with the nut. When the strong
  gust blows through it shakes a dozen nuts loose at once and three or four more squirrels come bounding
  in from the edges for the frenzy: fetching, eating, burying, raiding and chasing; when nothing is left
  loose they leave the way they came and the patch is quiet again.
- The windmill's wheel turns and the washing on the line flaps, both swelling and easing with the same gusts
  as the scarf (each a flip-book, the page read off a phase that only ever runs forward).
- Smoke drifts from the chimney, the windows' lamplight wanders, and the scarecrow's red scarf streams
  in the breeze.
- The leaves are the Sakura petal engine with a leaf outline, and they let go of the maples and the
  corner branches.
- Twenty small **happenings** come and go at random, as in Halloween and Christmas:

  | Where | What happens |
  |-------|--------------|
  | The sky | A shooting star. Stars join up into a turkey, a cornucopia, a maple leaf, a slice of pie or an acorn. Harvest fireworks. A gust whirls a swirl of leaves across. A hawk soars in slow circles. A flock of blackbirds wheels over the far hills as one long ribbon that streams along its way, bends, folds with a wave running down it, bunches and thins, and swings round as its path turns, then fades away. |
  | The farm | The porch light comes on and moths gather. Silhouettes pass the farmhouse windows, one carrying the bird to the table. A barn owl flies out of the loft. A wild turkey flaps in low over the hill, lands on a silo cap and roosts there a while, looking about and preening, before it drops off and flies away. |
  | The field | A doe and her fawn, half the time led by a buck, come up over the far ridge: tiny silhouettes, about fifteen pixels at 1080p, because the far ridge is the farthest thing in the picture. |
  | The near ground | A wild turkey struts through, stops, fans its tail, gobbles and struts on. A squirrel bounds along the fence rail and sits up with an acorn. A crow lands on the scarecrow and caws. A raccoon comes sniffing at the pumpkins, rears up for a guilty look about, and waddles off; if an acorn lies in its way it stops at that instead and comes up with the nut in its paws, nibbling it through the guilty look. A pheasant bursts out of the corn shock. The scarecrow blinks and winks. |
  | The props | An apple comes bounding in from beyond the left edge. Acorns patter down from the oak branch and stay on the ground for the squirrels. A cardinal lands on a twig and sings. |

Settings: number of leaves, fall speed, breeze, leaf size, number of geese, number of turkeys in
the field, number of deer (a doe, a doe and fawn, or a doe, fawn and buck; three by default), and how often a surprise
happens (0 turns them off).

Halloween, Christmas and Thanksgiving are built from the same shared pieces as the Sakura pair (the
falling engine, the ground, trees, hills and branches), plus one new one: sprites, small pictures
painted once and stamped each frame, which is how the bats, the sleigh, the geese and every moving
light (candles, bulbs, Rudolph's nose, the farmhouse windows) are drawn.

## Cabin by Stream

A flowery meadow at dusk, painted to look as much like a photograph as a painted scene can: a
small thatched cottage with lit windows and white smoke rising from its chimney, a stream winding
toward you with trout holding in the current, squirrels running between the trees and up them,
and fireflies blinking over the grass. The last of the sunset glows low on the left; the first
stars are out overhead; mist lies at the feet of the far forest.

![Cabin by Stream](docs/images/cabin-by-stream.png)

- The grass is textured pixel by pixel (coarser near you, hazier far away), with thousands of
  small wildflowers in drifts, blades and seed heads in the near meadow, long soft shadows that all
  fall away from the glow, and a camera's vignette and grain over the whole picture.
- The smoke is a crowd of puffs that rise fast while hot, slow as they cool, spread, lean with the
  breeze and thin away.
- The trout face upstream, drift slowly back and dart forward to hold their place, seen through
  the water. Light slides down the stream's surface.
- Each squirrel follows a plan: sit, run to the next tree (sometimes stopping to sit up and look
  about), climb, sit on the trunk, come down head first, run back.
- Fireflies blink on their own slow cycles and are mirrored in the stream when they cross it. The
  windows and the door lamp flicker like firelight; the brighter stars twinkle.
- No happenings yet.

Settings: fireflies, number of fish, number of squirrels, chimney smoke, breeze.

## Retired: Cotswold Brook

A real photograph of Cotswold cottages by a brook, with ripples, trout, smoke, a falling dusk,
fireflies and mist animated over it. Retired: its fish were flat stickers that only faced left
or right, flipped to turn and slid about, swimming under a mirror-bright pond where no fish
could be seen. The code stays in `src/CotswoldBrook/` as a reference for what not to do; it is
no longer built. Its `RETIRED.md` says what went wrong and the rules that came out of it. The
engine pieces it pioneered (fitting a photo, day for night, fireflies, mist, and the depth map
that lets a moving thing pass behind nearer scenery) stay in `Core/Photo/` for photo savers to
come. `scripts\depthmap.py` makes a depth map with
[Depth Anything V2](https://github.com/DepthAnything/Depth-Anything-V2) (Small model, Apache 2.0).

## All screensavers

- Run on every monitor, at native resolution (4K included).
- Live preview in Windows' Screen Saver Settings.
- A settings dialog with its own live preview.
- Each is one self-contained `.scr` file. The PC does not need .NET installed.

## Install

```powershell
.\scripts\install.ps1                          # build all, copy to System32 (one admin prompt), open Screen Saver Settings
.\scripts\install.ps1 -Saver Sakura -Activate  # build and install one, and select it with a 5-minute wait
```

Then pick one in the Screen Saver Settings list. The install script closes any Screen Saver
Settings window that was already open (an open one keeps showing its old list), opens a fresh one,
and confirms the new screensaver actually appears in its list.

No-admin alternative: run `.\scripts\publish.ps1`, then right-click a `.scr` in `dist\` in File
Explorer and choose **Install**. (Windows then uses the file where it sits, so leave it there.)

Remove with `.\scripts\uninstall.ps1` (all) or `.\scripts\uninstall.ps1 -Saver Sakura` (one).

## Try one without installing

```powershell
.\scripts\publish.ps1                                  # builds one dist\<Name>.scr per screensaver
.\dist\Sakura.scr /s                                   # full screen; move the mouse to exit
```

For development, run the plain build output (not the `.scr`; see the gotcha in the docs):

```powershell
dotnet build -c Release
$exe = ".\src\Sakura\bin\Release\net10.0-windows\Sakura.exe"
& $exe /window                                         # resizable window, Esc to close
& $exe /c                                              # settings dialog
& $exe /snapshot out.png 1920 1080 8                   # render 8 s off-screen, save a PNG + timing
```

## Verify

```powershell
.\scripts\verify.ps1                     # every .scr in dist\: snapshot speed, preview box, settings dialog
.\scripts\verify.ps1 -WindowsLaunch      # also let Windows start each one for real (your setting is restored)
```

Prints PASS / FAIL per check and saves pictures to `snapshots\verify\`. Look at the pictures too:
a check can pass while the picture is wrong.

## How it works

Start with **[docs/HOW_SCREENSAVERS_WORK.md](docs/HOW_SCREENSAVERS_WORK.md)**: a plain-language
guide to what a screensaver is, how Windows talks to it, and how both effects are built. Then read
`src/MatrixRain/MatrixRainScene.cs` or `src/Sakura/SakuraScene.cs`, both commented for a
first-time reader.

## Layout

```
src/Peckworks.Screensavers.Core/   the engine (command line, windows, drawing, glow, settings)
src/MatrixRain/                    the Matrix Rain screensaver
src/Sakura/                        the Sakura screensaver
src/SakuraDusk/                    the Sakura Dusk screensaver
src/SakuraPond/                    the Sakura Pond screensaver
src/Halloween/                     the Halloween screensaver
src/Christmas/                     the Christmas screensaver (the world, Santa, the residents, the stories)
src/Thanksgiving/                  the Thanksgiving screensaver
src/CabinByStream/                 the Cabin by Stream screensaver
src/CotswoldBrook/                 retired: kept as a reference, not built (its RETIRED.md says why)
scripts/                           publish / install / uninstall / verify / happening (render one happening)
                                   / depthmap.py (a photo's depth map, made once)
docs/                              the how-it-works guide, the happenings recipe, the minds recipe
```

## Performance (measured, i7-8850H laptop)

Time per frame (update + render):

| Resolution | Matrix Rain | Sakura      | Sakura Dusk | Halloween   | Christmas   | Thanksgiving | Cabin by Stream |
|------------|-------------|-------------|-------------|-------------|-------------|--------------|-----------------|
| 1920x1080  | about 10 ms | about 4 ms  | about 4 ms  | about 4 ms  | about 11 ms | about 3 ms   | about 2 ms      |
| 3840x2160  | about 29 ms | about 11 ms | about 12 ms | about 15 ms | about 11 ms (drawn at 1920 wide) | about 12 ms  | about 9 ms      |

Under about 16 ms means the full 60 frames per second. Each frame waits for the screen's next
refresh before the next one is drawn, so frames reach the screen evenly, one per refresh (an
earlier timer ran slightly faster than the screen and showed a frame twice now and then: a
small judder you could see in anything moving steadily). Matrix Rain at 4K runs at about 34, above
the film's own 24.

Sakura Pond: about 10 ms at 1920x1080. At 4K it draws at 2560x1440 and Windows enlarges the
picture; over 60-second runs on three seeds it averaged 13.8 to 16.0 ms per frame (the machine's
own state moves that by 2 ms between runs), with a worst second of 17.6 to 20.9 ms and a slowest
frame of 30 to 37 ms. The worst seconds are bursts: a flurry of ripples from landing petals, or a
swallow stamped large as it comes close. Where an ordinary frame goes, measured inside one run
with the engine's section timer (set `PECKWORKS_SECTIONS` to a file and run `/snapshot`): the koi
2.9 ms, the tufts 1.8, the carpet of blades 1.5 (slid and lit by the wind on alternate rows each
frame, the other rows kept from the frame before, in whole-number arithmetic with two colour
channels blended per multiply), the flyers 1.4, the backdrop copy 1.2 (the memory's speed, not
the processor's: splitting it across cores gained nothing and added a hitch), the ripples 0.9 (a
lookup table in place of two exponentials per pixel), everything else under 0.7.

Halloween's happenings cost almost nothing on average: four minutes of ordinary play at 4K
averaged 12.5 ms, against 13.0 to 13.2 ms for the same scene with nothing on, measured in the same
minutes. Three are dearer while they last. At 4K each adds about 5 to 6 ms per frame in its worst
second, which is about 50 frames per second for that moment: the blood moon for its 12 seconds, a
firework for the half second of each burst, and a lightning flash for under half a second. The
hill monster adds about 2 ms. At 1920x1080 none of them drops a frame (the worst second of ten
minutes of play was 5.7 ms). `scripts\happening.ps1 -Sweep` prints the average, the worst second
and the slowest single frame for each one.

The sakura pair's happenings, measured the same way at 4K (each alone, next to a quiet baseline
in the same minutes; the quiet scene's worst second was about 14 to 15 ms): most add 1 to 3 ms in
their worst second. Six are dearer while they last: Red Fuji (about 7.5 ms more), cloud shadows
(about 4.5) and the petal blizzard (about 3.5) in Sakura; magic hour (about 8.5), the god rays
(about 7) and the lit trees (about 4) in Sakura Dusk. All six share one claim ("costly"), so the
director never lets two of them run at once and their costs never add up. At 1920x1080 none of
them drops a frame.

Christmas, measured at 1920x1080 with every resident, the stories and the happenings on: 10.6 ms on average
over 120 seconds, and 14.5 to 16.3 ms in its worst second. On a 4K screen it is drawn at 1920 wide by default
(the "Draw width" setting) and Windows enlarges the picture, so it costs about the same there: drawing all
eight million pixels of a 4K screen took 21 to 23 ms a frame, more than a frame has. Each resident's
flip-books are painted on a helper thread in the first seconds while the snow is already falling. Every
resident then begins together about six seconds in, once all their flip-books are painted, and makes an
entrance.

Measure your own with `/snapshot`; the timing lands in `out.png.txt`.

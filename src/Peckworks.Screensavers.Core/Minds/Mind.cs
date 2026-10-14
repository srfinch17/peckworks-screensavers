namespace Peckworks.Screensavers.Core.Minds;

// ============================================================================
// MINDS: the small parts every animal "brain" in this repo is built from.
//
// FEYNMAN VERSION. An animal in these screensavers has a very small mind:
//
//   1. A few PERSONALITY DIALS, rolled once (how bold, how energetic, how
//      curious), each a number from 0 to 1. A dial never forces anything;
//      it only loads the dice below.
//   2. ONE THING AT A TIME (a "doing": wander, rest, hunt, bask ...) with a
//      feeling for how long to keep at it (a timer).
//   3. WEIGHTED DICE: when the timer runs out, the animal rolls for what to
//      do next. Each possible doing is a face on the dice, and the dials and
//      the situation set how big each face is (a curious fish's dice have
//      more "go and look" faces; "hunt" is a face only when there is prey).
//   4. REFLEXES underneath that always have the last word: the mind only
//      ever hands the steering a point to go to and how keenly; the bank,
//      the rocks, the other animals and the "never stuck" rule still decide
//      every step. And a few INTERRUPTS (a touch, a shadow, a tap on the
//      water) that cut in with a cooldown so they cannot fire every frame.
//
// This file holds the parts that are the same for every animal: the dice,
// the dials, the one-thing-at-a-time timer and the cooldown. What the faces
// ARE, what each doing does, and what interrupts it is each animal's own
// business, in its own file. The recipe, with the instruments that check a
// mind actually works on screen, is docs/MINDS.md.
// ============================================================================

/// <summary>Weighted dice: picks one of several things, each with its own chance.</summary>
public static class Dice
{
    /// <summary>
    /// Rolls once and returns the index of the face that came up. A face's
    /// chance is its weight divided by the total; a weight of 0 (or less) is a
    /// face that is not on the dice at all. Returns -1, without rolling, if no
    /// face is (so keep one face that is always on, as every animal here does,
    /// or handle -1).
    ///
    /// Think of a wheel of fortune: each face gets a slice as wide as its
    /// weight, the pointer lands somewhere round the rim, and we walk the
    /// slices until we find the one it landed in.
    /// </summary>
    public static int Pick(Random rng, ReadOnlySpan<float> weights)
    {
        float total = 0;
        foreach (float w in weights) if (w > 0) total += w;
        if (total <= 0) return -1;
        float roll = (float)rng.NextDouble() * total;
        int last = -1;
        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i] <= 0) continue;
            last = i;
            roll -= weights[i];
            if (roll < 0) return i;
        }
        return last;                     // rounding left the pointer a hair past the rim: the last face
    }
}

/// <summary>Personality dials: numbers from 0 to 1 rolled once per animal.</summary>
public static class Traits
{
    /// <summary>One dial, anywhere from 0 to 1.</summary>
    public static float Roll(Random rng) => (float)rng.NextDouble();

    /// <summary>
    /// One dial for the k-th of n animals, rolled inside its OWN slice of 0 to
    /// 1 (the first animal in the lowest slice, the next in the next ...), so
    /// a group never comes out as near-twins that nobody can tell apart.
    /// With reverse, the slices run the other way, which lets two dials be
    /// paired against each other (the boldest lizard is also the laziest).
    /// </summary>
    public static float Slice(Random rng, int k, int n, bool reverse = false)
    {
        n = Math.Max(1, n);
        k = Math.Clamp(reverse ? n - 1 - k : k, 0, n - 1);
        return (k + (float)rng.NextDouble()) / n;
    }

    /// <summary>
    /// n dials, one per slice of 0 to 1 (see Slice), dealt out in a random
    /// order: a spread of personalities with no two alike, and no pattern
    /// between an animal's number and its nature.
    /// </summary>
    public static float[] Deal(Random rng, int n)
    {
        var dials = new float[Math.Max(0, n)];
        for (int k = 0; k < dials.Length; k++) dials[k] = Slice(rng, k, dials.Length);
        for (int k = dials.Length - 1; k > 0; k--)                    // shuffle
        {
            int j = rng.Next(k + 1);
            (dials[k], dials[j]) = (dials[j], dials[k]);
        }
        return dials;
    }
}

/// <summary>
/// One thing at a time, with a timer: what the animal is doing now and how
/// long it means to keep at it. The animal's own enum names the doings.
/// </summary>
public sealed class Mind<TDoing> where TDoing : struct, Enum
{
    /// <summary>What it is doing now.</summary>
    public TDoing Doing { get; private set; }

    /// <summary>Seconds left before the doing is over. Never counts down from Forever. An animal may set it to cut a doing short or stretch it.</summary>
    public float Left { get; set; }

    /// <summary>A doing with no timer: it ends only when something else ends it.</summary>
    public const float Forever = float.MaxValue;

    public Mind(TDoing start = default) { Doing = start; Left = Forever; }

    /// <summary>Starts a doing and sets how long it may last.</summary>
    public void Begin(TDoing doing, float seconds = Forever)
    {
        Doing = doing;
        Left = seconds;
    }

    /// <summary>Counts the timer down. Call once a frame, before asking Over.</summary>
    public void Tick(float dt)
    {
        if (Left != Forever) Left -= dt;
    }

    /// <summary>Has the timer run out?</summary>
    public bool Over => Left <= 0;

    /// <summary>Ends the doing now (the next Over is true).</summary>
    public void End() => Left = 0;

    /// <summary>The doing's name, for logs and tests.</summary>
    public override string ToString() => Doing.ToString();
}

/// <summary>
/// A cooldown: "not again for a while". An interrupt (a fright, a chase, a
/// shadow) starts one, and nothing can fire it again until it has run out,
/// so a thing that stays close cannot set it off thirty times a second.
/// </summary>
public sealed class Cooldown
{
    private float _left;

    /// <summary>May it fire now?</summary>
    public bool Ready => _left <= 0;

    /// <summary>Seconds still to wait (0 or less when ready).</summary>
    public float Left => _left;

    /// <summary>Blocks it for this many seconds.</summary>
    public void Start(float seconds) => _left = seconds;

    /// <summary>Counts down. Call once a frame.</summary>
    public void Tick(float dt) => _left -= dt;
}

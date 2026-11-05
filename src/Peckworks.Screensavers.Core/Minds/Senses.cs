namespace Peckworks.Screensavers.Core.Minds;

// ============================================================================
// SENSES: how an animal notices what happens around it, and what kind of
// animal it is when it does.
//
// FEYNMAN VERSION. A mind on its own only rolls dice for what to do next.
// What makes a creature look ALIVE to someone watching is that it reacts:
// the turkeys look up when the tractor comes, the fawn freezes when a
// raccoon crosses the ground, a mouse bolts when a squirrel bounds past, and
// the whole herd looks where one of them is looking. Three parts do that:
//
//   1. A NOTICEBOARD the scene owns. Anything that happens (a tractor, a
//      squirrel's bound, a scuffle, an apple rolling) pins a NOTICE on it:
//      what kind of thing, where, how far it carries, how strong. A notice
//      stays pinned for about a second and falls off by itself.
//   2. A NOTICE is a kind plus a place: Loud (a noise, carries far), Movement
//      (something moving nearby), Threat (something to get away from), Food,
//      Friend (one of its own kind). The kinds are deliberately few and plain
//      so a bird written for one saver reacts sensibly in another.
//   3. ATTENTION is the animal's own: two dials (how timid, how curious), a
//      cooldown so one thing cannot startle it thirty times a second, and
//      ONE rule that turns the notices it can feel into a REACTION: look,
//      freeze, flee, approach, or nothing. The animal then turns the reaction
//      into its own doings (a deer's "freeze" is the head up and the ears
//      forward; a mouse's "flee" is a bolt behind a pumpkin). The kit never
//      moves anything; it only says what the animal felt.
//
// This is the same shape as Mind.cs: the shared part is small and dumb, the
// animal keeps what makes it that animal. The recipe is docs/MINDS.md.
// ============================================================================

/// <summary>What kind of thing was noticed.</summary>
public enum Sense
{
    /// <summary>A noise: a tractor, a scuffle, fireworks. Carries far; everyone looks, the timid freeze.</summary>
    Loud,
    /// <summary>Something moving close by: a squirrel bounding past, a deer walking through, a bird swooping. The curious look.</summary>
    Movement,
    /// <summary>Something to get away from: a raccoon, a fox, a hawk's shadow. The timid flee, the rest freeze.</summary>
    Threat,
    /// <summary>Something to eat: an apple, a dropped nut. The hungry approach.</summary>
    Food,
    /// <summary>One of its own kind doing something: a herd-mate's alarm, a flock-mate's display. Spreads a reaction through a group.</summary>
    Friend,
    /// <summary>One of its own kind calling: a tom's display, a chirp from a perch. The curious come over, the rest look.</summary>
    Call,
}

/// <summary>One thing that happened: what, where, how far it carries (pixels), how strong (0 to 1), who caused it, and when.</summary>
public readonly record struct Notice(Sense Kind, PointF At, float Reach, float Weight, object? Who, double Posted);

/// <summary>The scene's board of recent notices. Post what happens; the animals read it once a frame.</summary>
public sealed class Noticeboard
{
    private readonly List<Notice> _live = new();
    private double _time;

    /// <summary>How long a notice stays pinned, seconds (long enough for every animal to read it on the frame after it was posted, short enough that a passing tractor is "now", not "a while ago").</summary>
    public const float Life = 1.0f;

    /// <summary>The scene's clock, as the board knows it.</summary>
    public double Time => _time;

    /// <summary>Counts time on and drops notices past their life. Call once a frame, before the animals read.</summary>
    public void Tick(double dt)
    {
        _time += dt;
        for (int i = _live.Count - 1; i >= 0; i--)
            if (_time - _live[i].Posted > Life) _live.RemoveAt(i);
    }

    /// <summary>Pins a notice: kind, where (pixels), how far it carries (pixels), how strong (0 to 1), and who caused it (so it does not startle itself).</summary>
    public void Post(Sense kind, PointF at, float reach, float weight = 1f, object? who = null)
    {
        if (reach <= 0 || weight <= 0) return;
        _live.Add(new Notice(kind, at, reach, Math.Min(1f, weight), who, _time));
    }

    /// <summary>Everything pinned right now (oldest first).</summary>
    public IReadOnlyList<Notice> Live => _live;

    /// <summary>Drops everything (a scene reset).</summary>
    public void Clear() => _live.Clear();
}

/// <summary>What an animal felt about the board this frame.</summary>
public enum Reaction
{
    None,
    /// <summary>Look that way, carry on.</summary>
    Look,
    /// <summary>Stop still and watch (head up, ears forward).</summary>
    Freeze,
    /// <summary>Get away from it.</summary>
    Flee,
    /// <summary>Go toward it (food, a friend).</summary>
    Approach,
}

/// <summary>
/// An animal's attention: its temperament dials, its cooldown, and the one
/// rule that turns the notices within its reach into a reaction. Make one per
/// animal, deal the dials (Traits.Deal), and ask Check once a frame.
/// </summary>
public sealed class Attention
{
    /// <summary>0 bold to 1 timid: how readily it flees rather than freezes, and how far a threat reaches it.</summary>
    public float Timid;
    /// <summary>0 dull to 1 curious: how readily it looks at a movement or a friend.</summary>
    public float Curious;
    /// <summary>0 fed to 1 famished: a mood dial the animal moves; food is approached only when it is up.</summary>
    public float Hungry;
    /// <summary>How much more of the world it feels: 1 is the notice's own reach; a deer with big ears might be 1.3, a mouse in the grass 0.7.</summary>
    public float Keen = 1f;
    /// <summary>"Not again for a while": a reaction starts it; nothing is felt until it has run out.</summary>
    public readonly Cooldown Rested = new();

    /// <summary>The strongest thing it felt last Check (for logs), or None.</summary>
    public Reaction Last { get; private set; }
    public Notice LastNotice { get; private set; }

    public Attention(float timid, float curious) { Timid = timid; Curious = curious; }

    /// <summary>Counts the cooldown. Call once a frame.</summary>
    public void Tick(float dt) => Rested.Tick(dt);

    /// <summary>
    /// Reads the board from "me" and says what the animal feels: the strongest reaction among the notices it can
    /// feel (fleeing beats freezing beats approaching beats looking), with the notice that caused it. Nothing posted
    /// by "self" counts. A reaction starts the cooldown: a look for a second or two, a freeze for a few seconds, a
    /// flight for longer and longer the more timid it is, so the same tractor does not startle it every frame and a
    /// bold animal gets over things sooner.
    /// </summary>
    public Reaction Check(Noticeboard board, PointF me, object? self, out Notice cause)
    {
        cause = default;
        // While the cooldown runs, only something WORSE than what set it off gets through (a deer glancing at a
        // squirrel still freezes when the tractor comes; the same squirrel cannot keep it glancing).
        int floor = Rested.Ready ? 0 : Rank(Last);
        if (Rested.Ready) Last = Reaction.None;
        Reaction best = Reaction.None;
        float bestStrength = 0;
        foreach (Notice n in board.Live)
        {
            if (n.Who != null && n.Who == self) continue;
            float dx = n.At.X - me.X, dy = n.At.Y - me.Y;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            float closeness = 1f - d / (n.Reach * Keen);
            if (closeness <= 0) continue;
            float strength = closeness * n.Weight;                 // 1 right on top of a full-strength notice, 0 at the edge of its reach
            Reaction r = n.Kind switch
            {
                Sense.Threat => strength * (0.5f + Timid) > 0.30f ? (Timid > 0.4f ? Reaction.Flee : Reaction.Freeze) : strength > 0.12f ? Reaction.Look : Reaction.None,
                Sense.Loud => strength * (0.6f + 0.8f * Timid) > 0.55f ? (Timid > 0.7f ? Reaction.Flee : Reaction.Freeze) : strength > 0.15f ? Reaction.Look : Reaction.None,
                Sense.Movement => strength * (0.4f + Timid) > 0.45f ? Reaction.Freeze : strength * (0.3f + Curious) > 0.15f ? Reaction.Look : Reaction.None,
                Sense.Food => Hungry * strength > 0.25f ? Reaction.Approach : strength * Curious > 0.3f ? Reaction.Look : Reaction.None,
                Sense.Friend => strength * (0.4f + Curious) > 0.25f ? Reaction.Look : Reaction.None,
                Sense.Call => strength * (0.3f + Curious) > 0.55f ? Reaction.Approach : strength * (0.4f + Curious) > 0.25f ? Reaction.Look : Reaction.None,
                _ => Reaction.None,
            };
            if (r == Reaction.None || Rank(r) <= floor) continue;
            if (Rank(r) > Rank(best) || (Rank(r) == Rank(best) && strength > bestStrength)) { best = r; bestStrength = strength; cause = n; }
        }
        if (best != Reaction.None)
        {
            Last = best; LastNotice = cause;
            Rested.Start(best switch
            {
                Reaction.Look => 1f + 1.5f * (1 - Curious),
                Reaction.Freeze => 2.5f + 3f * Timid,
                Reaction.Flee => 6f + 10f * Timid,
                Reaction.Approach => 2f,
                _ => 1f,
            });
        }
        return best;
    }

    private static int Rank(Reaction r) => r switch { Reaction.Flee => 4, Reaction.Freeze => 3, Reaction.Approach => 2, Reaction.Look => 1, _ => 0 };
}

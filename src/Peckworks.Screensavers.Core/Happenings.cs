namespace Peckworks.Screensavers.Core;

/// <summary>
/// One small thing that happens now and then in a scene: a shooting star, a
/// ghost rising from a grave, a firework. It comes on, does its bit for a few
/// seconds, and goes away until the director picks it again.
///
/// FEYNMAN VERSION: think of a cuckoo clock. Most of the time the little door
/// is shut. Now and then it opens, the bird does its routine, and the door
/// shuts again. A Happening is one bird. The HappeningDirector below is the
/// clockwork that decides when each door opens.
///
/// The one rule that keeps these simple: Draw is told "t", the number of
/// seconds since this showing began, and works out everything from that
/// number alone (where the star is, how faded the ghost is). It keeps no
/// running tally from frame to frame. That means any moment of any happening
/// can be drawn on its own, which is how we test them: "show me the firework
/// at 2.5 seconds" (see PECKWORKS_HAPPENING on the director).
///
/// Paint your sprites in the constructor (once). In Begin, only roll dice.
/// </summary>
public abstract class Happening
{
    /// <summary>How long one showing lasts, in seconds. May change per showing (set it in Begin).</summary>
    public abstract float Seconds { get; }

    /// <summary>
    /// The prop this happening takes over while it runs ("moon", "skeleton"),
    /// or null. The director never runs two happenings with the same claim
    /// at once, so a face cannot appear in the moon while it is turning red.
    /// </summary>
    public virtual string? Claims => null;

    /// <summary>Called at the start of each showing. Pick this showing's random details here (which tombstone, which color).</summary>
    public virtual void Begin(Random rng) { }

    /// <summary>Draw this showing as it looks t seconds after Begin (0 up to Seconds).</summary>
    public abstract void Draw(FrameBuffer fb, float t);

    /// <summary>
    /// "Ease in, ease out": turns a steady 0-to-1 count into one that starts
    /// slowly, speeds up, and slows to a stop, the way a hand moves. Numbers
    /// outside 0 to 1 are pinned to the ends. Use it on any movement or fade
    /// that should not start or stop with a jerk.
    /// </summary>
    protected static float Smooth(float x)
    {
        x = Math.Clamp(x, 0f, 1f);
        return x * x * (3 - 2 * x);
    }

    /// <summary>
    /// A fade-in, hold, fade-out curve for a showing "total" seconds long:
    /// climbs from 0 to 1 over the first fadeIn seconds, stays at 1, and
    /// drops back to 0 over the last fadeOut seconds. Multiply an opacity by
    /// it and the thing arrives and leaves softly instead of popping.
    /// </summary>
    protected static float Fade(float t, float total, float fadeIn, float fadeOut) =>
        Math.Min(Smooth(t / Math.Max(0.001f, fadeIn)), Smooth((total - t) / Math.Max(0.001f, fadeOut)));
}

/// <summary>
/// Decides which happening goes on next, and when.
///
/// It works like a deck of cards: every happening is one card, the deck is
/// shuffled, and one card is dealt every few seconds. Only when the deck runs
/// out is it shuffled again. So over a few minutes you see every happening
/// once before any repeats, in an order that is different every time.
///
/// Building a happening means painting its sprites, which takes a moment
/// (up to a twelfth of a second on a 4K screen). Doing all of them at
/// startup would slow the launch. Doing each one at the moment it is first
/// dealt freezes the picture for that moment, a visible stutter. So they are
/// built by a HELPER: a second line of work (a "background thread") that
/// starts when the scene does and paints them one after another while the
/// picture carries on. If one is dealt before the helper has reached it, it
/// is simply built on the spot.
///
/// FOR TESTING: set the environment variable PECKWORKS_HAPPENING before
/// launching. A happening's name plays that one alone, starting at time zero
/// and repeating, so "/snapshot out.png 1920 1080 2.5" shows it 2.5 seconds
/// in. The word "all" plays every happening back to back, a showreel.
/// PECKWORKS_SEED (any whole number) makes the scene's dice repeatable, so
/// two runs paint the same scenery: see SceneRandom.
/// </summary>
public sealed class HappeningDirector
{
    private const int MaxAtOnce = 3;

    private readonly (string Name, Func<Happening> Make)[] _cast;
    private readonly Happening?[] _made;                       // each one once it is built (by the helper, or on first use)
    private readonly Random _rng;
    private readonly List<int> _deck = [];                     // cards still to deal this round
    private readonly List<(int Who, double Start)> _running = [];
    private readonly double _gapMin, _gapMax;                  // seconds between one start and the next
    private readonly bool _off, _reel;
    private readonly int _only = -1;                           // testing: play just this one
    private int _reelNext;
    private volatile bool _building;                           // true while the helper is still painting (volatile: both lines of work read it)
    private double _time, _nextAt;                             // a double: this clock runs for days (see the scene's clock)

    /// <param name="cast">Every happening: a name, and how to build it.</param>
    /// <param name="frequency">1 = normal (a new one every 5 to 12 seconds), 2 = twice as often, 0 = never.</param>
    public HappeningDirector((string Name, Func<Happening> Make)[] cast, Random rng, float frequency)
    {
        _cast = cast;
        _made = new Happening?[cast.Length];
        _rng = rng;
        _off = frequency <= 0 || cast.Length == 0;
        _gapMin = 5 / Math.Max(0.01, frequency);
        _gapMax = 12 / Math.Max(0.01, frequency);
        _nextAt = 3 + 3 * rng.NextDouble();                    // the first one comes soon

        string? test = Environment.GetEnvironmentVariable("PECKWORKS_HAPPENING");
        if (!string.IsNullOrWhiteSpace(test) && cast.Length > 0)
        {
            _off = false;
            _nextAt = 0;
            _reel = test.Equals("all", StringComparison.OrdinalIgnoreCase);
            if (!_reel)
            {
                _only = Array.FindIndex(cast, c => c.Name.Equals(test, StringComparison.OrdinalIgnoreCase));
                // A typo here must be loud. Quietly playing nothing would look
                // exactly like a happening that forgot to draw itself.
                if (_only < 0)
                    throw new ArgumentException($"PECKWORKS_HAPPENING='{test}' is not a happening. Known: all, {string.Join(", ", cast.Select(c => c.Name))}");
            }
        }

        // Start the helper (see the class summary). Not in testing: a test
        // wants one happening and clean timings, not eighteen others being
        // painted alongside. If the helper trips over anything, it just
        // stops; whatever it did not build is built on first use instead,
        // and any real fault shows up there, on the main line of work.
        if (!_off && _only < 0 && !_reel)
        {
            _building = true;
            Task.Run(() =>
            {
                try { for (int i = 0; i < _cast.Length; i++) Built(i); }
                catch { }
                finally { _building = false; }
            });
        }
    }

    /// <summary>
    /// The scene's dice. Normally different on every launch. For testing, set
    /// the environment variable PECKWORKS_SEED to a whole number and every
    /// launch rolls the same numbers, so the scenery (and which tombstone the
    /// ghost picks) comes out the same each time. That is how you look at
    /// three moments of ONE scene, or check a fix on the exact scene that
    /// showed the fault. A scene uses it in place of "new Random()".
    /// </summary>
    public static Random SceneRandom() =>
        int.TryParse(Environment.GetEnvironmentVariable("PECKWORKS_SEED"), out int seed) ? new Random(seed) : new Random();

    /// <summary>
    /// Happening number "who", built if it is not yet. The helper and the
    /// main line may both get here for the same one at the same moment, and
    /// there must only ever be ONE of each (a showing that began on one copy
    /// and was drawn from another would be drawn before its dice were
    /// rolled). CompareExchange is the tie-break: "put mine in the slot only
    /// if the slot is still empty, and tell me what was there". Whoever
    /// loses the race throws its copy away and uses the winner's.
    /// </summary>
    private Happening Built(int who) =>
        _made[who] ?? Interlocked.CompareExchange(ref _made[who], _cast[who].Make(), null) ?? _made[who]!;

    public void Update(double elapsedSeconds)
    {
        if (_off) return;
        _time += elapsedSeconds;
        _running.RemoveAll(r => _time - r.Start >= _made[r.Who]!.Seconds);
        if (_time < _nextAt) return;

        bool testing = _only >= 0 || _reel;
        int who = testing && _running.Count > 0 ? -1           // testing plays strictly one at a time
                : _only >= 0 ? _only
                : _reel ? _reelNext++ % _cast.Length
                : Deal();
        if (who < 0)
        {
            _nextAt = _time + 1;                               // nothing can go on right now; look again in a second
            return;
        }

        Happening h = Built(who);
        h.Begin(_rng);
        _running.Add((who, _time));
        _nextAt = testing ? _time + h.Seconds + 1
                // A showing that found nothing to draw (no branch to hang a web
                // on) says so with a tiny Seconds. Deal again in a second
                // instead of leaving the stage empty for a whole turn.
                : h.Seconds < 0.5f ? _time + 1
                : _time + _gapMin + (_gapMax - _gapMin) * _rng.NextDouble();
    }

    /// <summary>
    /// Deals the next card that is free to go on: not already running, and
    /// not after a prop that a running happening has claimed. Returns -1 if
    /// the stage is full or every remaining card is blocked.
    /// </summary>
    private int Deal()
    {
        if (_running.Count >= MaxAtOnce) return -1;
        if (_deck.Count == 0)
        {
            _deck.AddRange(Enumerable.Range(0, _cast.Length));
            for (int i = _deck.Count - 1; i > 0; i--)          // shuffle: swap each card with a random earlier one
            {
                int j = _rng.Next(i + 1);
                (_deck[i], _deck[j]) = (_deck[j], _deck[i]);
            }
        }
        for (int i = _deck.Count - 1; i >= 0; i--)
        {
            int who = _deck[i];
            if (_running.Any(r => r.Who == who)) continue;
            // While the helper is still painting, a card it has not reached
            // yet is left in the deck: building it here, on the main line,
            // would freeze the picture (the fox takes half a second at 4K).
            if (_building && _made[who] == null) continue;
            string? claim = Built(who).Claims;
            if (claim != null && _running.Any(r => _made[r.Who]!.Claims == claim)) continue;
            _deck.RemoveAt(i);
            return who;
        }
        return -1;
    }

    /// <summary>Draws every happening that is on right now, oldest first.</summary>
    public void Draw(FrameBuffer fb)
    {
        foreach (var (who, start) in _running)
            _made[who]!.Draw(fb, (float)(_time - start));
    }
}

namespace Peckworks.Screensavers.Core;

/// <summary>
/// One LITTLE SCENE: a short play with a beginning, a middle and an end (a
/// fox dives into the snow after a mouse and comes up with a white nose; the
/// mice trim a tiny tree). A happening is a cameo of a few seconds; a story
/// is half a minute with actors who do one thing after another.
///
/// The rule that keeps a story testable: Begin rolls ALL its dice and writes
/// the script (who is where at which second), and Draw draws from the
/// story's clock "t" alone. The same seed then plays the same story, frame
/// for frame, and any second of it can be rendered on its own.
///
/// This is the engine's copy of what Thanksgiving grew in its own Stories.cs
/// (2026-11-05), without Thanksgiving's cast of flip-books: the brains
/// travel between savers, the bodies do not.
/// </summary>
public abstract class Story
{
    public abstract string Name { get; }

    /// <summary>The props it needs, as HappeningDirector claims ("ground,cabin"): it never begins while a happening holds one, and holds them itself while it plays.</summary>
    public abstract string Claims { get; }

    /// <summary>False when the story has nothing to show right now (its actors' pictures are not built yet, Santa is off screen).</summary>
    public virtual bool CanBegin => true;

    /// <summary>Rolls the dice and writes the script; returns how many seconds the story runs.</summary>
    public abstract float Begin(Random rng);

    /// <summary>Once a frame while it plays, with the story's clock, before anything draws (a story that drives a borrowed resident moves it here).</summary>
    public virtual void Update(float t) { }

    public abstract void Draw(FrameBuffer fb, float t);

    /// <summary>Called when the story ends (or is cut): hand back anything borrowed.</summary>
    public virtual void End() { }
}

/// <summary>
/// Deals the stories like a deck of cards, one every minute or so (every
/// story once before any repeats), and draws the one that is on.
///
/// It shares the happenings' claims: a story that needs the cabin waits
/// while a happening is using the cabin, and while the story plays no
/// happening that needs the cabin can begin (HappeningDirector.SceneClaims).
///
/// Test aids (environment variables): PECKWORKS_STORY=1 starts the first
/// story at exactly 4 seconds with short rests between (so a frame dump's
/// story time is its process time less four), =0 never plays one.
/// PECKWORKS_STORYPICK names the one story to play, every time.
/// PECKWORKS_STORYLOG names a file that gets one line per story begun.
/// </summary>
public sealed class StoryDirector
{
    private readonly HappeningDirector _director;
    private readonly Random _rng;
    private readonly Story[] _stories;
    private readonly List<int> _deck = new();
    private readonly string? _only, _log;
    private readonly bool _quick;
    private readonly float _frequency;
    private Story? _on;
    private double _time, _next;
    private float _t, _end;

    /// <summary>Asked before a story is dealt: true holds the deal a second (a resident is in the middle of something the story would trample).</summary>
    public Func<bool>? Hold;

    /// <summary>True while a story is on (the residents keep out of its way).</summary>
    public bool Playing => _on != null;

    /// <summary>The story that is on, or null.</summary>
    public Story? On => _on;

    /// <param name="frequency">1 = a story every minute or so, 2 = twice as often, 0 = never.</param>
    public StoryDirector(HappeningDirector director, Random sceneRng, Story[] stories, float frequency = 1f)
    {
        _director = director;
        _rng = new Random(sceneRng.Next());          // its own dice, seeded by ONE roll of the scene's
        _stories = stories;
        _frequency = frequency;
        string? ask = Environment.GetEnvironmentVariable("PECKWORKS_STORY");
        _quick = ask == "1";
        _next = _quick ? 4.0 : ask == "0" || frequency <= 0 || stories.Length == 0 ? double.MaxValue : (8 + 14 * _rng.NextDouble()) / frequency;
        _only = Environment.GetEnvironmentVariable("PECKWORKS_STORYPICK");
        if (string.IsNullOrEmpty(_only)) _only = null;
        _log = Environment.GetEnvironmentVariable("PECKWORKS_STORYLOG");
    }

    public void Update(float dt)
    {
        _time += dt;
        if (_on != null)
        {
            _t += dt;
            if (_t >= _end)
            {
                _on.End();
                _on = null;
                _director.SceneClaims = null;
                _next = _time + (_quick ? 3 : (35 + 50 * _rng.NextDouble()) / _frequency);
            }
            else _on.Update(_t);
            return;
        }
        if (_time < _next) return;
        if (Hold?.Invoke() == true) { _next = _time + 1; return; }

        // The deck: every story once before any repeats. A story that cannot
        // begin now (a prop held, its actors not ready) keeps its card and waits its turn.
        if (_deck.Count == 0)
        {
            for (int i = 0; i < _stories.Length; i++) _deck.Add(i);
            for (int i = _deck.Count - 1; i > 0; i--) { int j = _rng.Next(i + 1); (_deck[i], _deck[j]) = (_deck[j], _deck[i]); }
        }
        for (int i = _deck.Count - 1; i >= 0; i--)
        {
            Story st = _stories[_deck[i]];
            if (_only != null && !string.Equals(st.Name, _only, StringComparison.OrdinalIgnoreCase)) continue;
            if (!st.CanBegin || _director.Claimed(st.Claims)) continue;
            if (_only == null) _deck.RemoveAt(i);
            _on = st;
            _t = 0;
            _end = st.Begin(_rng);
            _director.SceneClaims = st.Claims;
            if (!string.IsNullOrEmpty(_log))
            {
                using var sw = new StreamWriter(_log, append: true);
                sw.WriteLine(FormattableString.Invariant($"story {st.Name} at {_time:F1}s, runs {_end:F2}s"));
            }
            return;
        }
        _next = _time + 2;                           // nothing could begin: look again in a moment
    }

    public void Draw(FrameBuffer fb)
    {
        if (_on != null) _on.Draw(fb, _t);
    }
}

/// <summary>Something that can draw one of its members on request (see DepthQueue).</summary>
public interface IDepthDrawn
{
    void DrawOne(FrameBuffer fb, int index);
}

/// <summary>
/// Draws everything that stands on one stretch of ground in the right
/// order: FARTHEST FIRST, so whatever is nearer the viewer is painted over
/// whatever is behind it. On a ground seen from the side and a little above,
/// "farther" simply means "feet higher up the screen".
///
/// Every frame each resident hands in one line per animal: where its feet
/// are and who to ask to draw it. The queue sorts the lines by the feet's y
/// and calls them back in that order. Like lining children up for a school
/// photo by height: nobody decides the order by hand, the rule does.
///
/// Without it each KIND of animal is drawn as a block, and a hare that is
/// nearer than a reindeer runs "under" it because reindeer happen to be
/// drawn later (Thanksgiving met that with its possum).
/// </summary>
public sealed class DepthQueue
{
    // "Seq" is the order things were handed in. Two animals with their feet on the very same row keep that
    // order from frame to frame; without it the sort may swap them at random and the pair flickers.
    private readonly List<(float FeetY, int Seq, IDepthDrawn Who, int Index)> _items = new();
    private static readonly Comparison<(float FeetY, int Seq, IDepthDrawn Who, int Index)> ByFeet =
        (a, b) => a.FeetY != b.FeetY ? a.FeetY.CompareTo(b.FeetY) : a.Seq.CompareTo(b.Seq);

    /// <summary>Hands in one thing to draw this frame. "index" comes back to the owner's DrawOne, so it knows which of its animals is meant.</summary>
    public void Add(float feetY, IDepthDrawn who, int index = 0) => _items.Add((feetY, _items.Count, who, index));

    /// <summary>Draws everything handed in, farthest first, and empties the queue for the next frame.</summary>
    public void Flush(FrameBuffer fb)
    {
        _items.Sort(ByFeet);
        for (int i = 0; i < _items.Count; i++) _items[i].Who.DrawOne(fb, _items[i].Index);
        _items.Clear();
    }
}

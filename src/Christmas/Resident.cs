using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Christmas;

/// <summary>
/// The base of everything that LIVES in the Christmas scene all the time
/// (the reindeer, the fox and the hares, the snowman, the birds at the
/// table, the mice in the log, the otters on the pond). A happening is a
/// visitor that comes on for a few seconds; a resident is always somewhere,
/// doing something, and notices what goes on around it.
///
/// What every resident gets from here:
///
///   - ITS OWN DICE (Rng), seeded by one roll of the scene's dice. Adding a
///     new doing to the fox later can then never change what the reindeer
///     roll: each animal's luck is its own.
///   - THE NOTICEBOARD (Board, Core/Minds/Senses.cs): whatever happens in
///     the scene is pinned there, and a resident reads it to react.
///   - BuildInBackground: an animal's flip-books take a moment to paint, so
///     they are painted on a helper thread while the snow is already
///     falling. The scene does not update or draw a resident until Ready.
///   - Stamp: puts a picture on the frame with the animal's FEET at a
///     point, hidden by any near prop that stands nearer than those feet
///     (the depth picture, ChristmasScenery.NearDepth).
///   - Lull: the scene sets it while a story plays. A resident with Lull on
///     finishes what it is doing and keeps to quiet doings out of the
///     story's way ("the mouse" in a story must not be any mouse).
///
/// The scene asks each resident, every frame: Update (think and move), then
/// Collect (hand the depth queue one line per animal), and the queue calls
/// DrawOne back in far-to-near order (Core/Stories.cs, DepthQueue).
/// </summary>
internal abstract class Resident : IDepthDrawn
{
    protected readonly ChristmasScenery S;
    protected readonly Random Rng;
    protected readonly Noticeboard Board;
    private volatile bool _ready;

    /// <summary>True once the flip-books are painted. Until then the scene leaves this resident alone.</summary>
    public bool Ready => _ready;

    /// <summary>Set by the scene while a story plays: keep out of its way.</summary>
    public bool Lull;

    protected Resident(ChristmasScenery s, Random sceneRng, Noticeboard board)
    {
        S = s;
        Rng = new Random(sceneRng.Next());
        Board = board;
    }

    /// <summary>
    /// Paints the flip-books on a helper thread and marks the resident Ready
    /// when they are done. "build" must only PAINT (make sprites from sizes
    /// and colours): no dice, no reading of anything that changes, because
    /// it runs at the same time as the scene.
    /// </summary>
    protected void BuildInBackground(Action build) =>
        Quietly(() => { try { build(); } catch { return; } _ready = true; });

    // ---- PAINTING WITHOUT STEALING THE SHOW ----
    //
    // FEYNMAN VERSION: the flip-books are painted while the snow is already falling. At first every resident
    // painted at full tilt on every processor core at once, and for the first five seconds the picture itself
    // could not get a turn: 26 thousandths of a second a frame instead of 7, the snow juddering (the publish
    // check caught it; his word for the whole saver is "silky", and that includes the first second). So the
    // painters now work like cleaners in a theatre during the show: on a few threads only, and at a LOWER
    // PRIORITY than the picture, which means Windows lets the picture go first whenever both want the
    // processor. The animals arrive a few seconds later; nothing on screen ever waits for them.

    /// <summary>Runs "work" on a helper thread of its own at below-normal priority.</summary>
    public static void Quietly(Action work) =>
        new Thread(() => work()) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "painting flip-books" }.Start();

    /// <summary>
    /// Does body(0), body(1) ... body(n - 1), shared between a few helper threads at below-normal priority, and
    /// returns when all are done. For painting many pages: quicker than one after another, without taking every
    /// core the way Parallel.For does.
    /// </summary>
    public static void QuietFor(int n, Action<int> body)
    {
        if (n <= 0) return;
        int workers = Math.Clamp(Math.Min(n, Environment.ProcessorCount / 4), 1, 3);
        int next = -1;
        Exception? failed = null;
        var threads = new Thread[workers];
        for (int w = 0; w < workers; w++)
        {
            threads[w] = new Thread(() =>
            {
                try { int i; while ((i = Interlocked.Increment(ref next)) < n) body(i); }
                catch (Exception e) { failed = e; }
            }) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "painting flip-books" };
            threads[w].Start();
        }
        foreach (Thread t in threads) t.Join();
        if (failed != null) throw failed;
    }

    /// <summary>Thinks and moves. dt is the seconds since the last frame.</summary>
    public abstract void Update(float dt);

    /// <summary>Hands the queue one line per visible animal: q.Add(its feet's y, this, its index).</summary>
    public abstract void Collect(DepthQueue q);

    /// <summary>Draws the animal with that index.</summary>
    public abstract void DrawOne(FrameBuffer fb, int index);

    /// <summary>
    /// Stamps "page" so that the pixel (originX, originY) of the page lands
    /// on the animal's feet at (feetX, feetY), and hides whatever part of it
    /// stands behind a nearer prop. "mirror" flips it to face left; the
    /// origin flips with it, so the feet stay on the same spot.
    /// </summary>
    protected void Stamp(FrameBuffer fb, Sprite page, float feetX, float feetY, int originX, int originY, bool mirror = false, float opacity = 1f) =>
        S.Stamp(fb, page, feetX, feetY, originX, originY, mirror, opacity);
}

/// <summary>
/// The residents' shared test log. With PECKWORKS_MINDLOG set to a file
/// name, every resident may write lines to it (its place, speed and doing
/// each frame, every roll and reaction), all through this ONE handle: two
/// writers each opening the same file refuse the second one and the saver
/// dies. Free when the variable is unset: Open() answers null.
/// </summary>
internal static class MindLog
{
    private static StreamWriter? _writer;
    private static bool _tried;

    public static StreamWriter? Open()
    {
        if (_tried) return _writer;
        _tried = true;
        string? path = Environment.GetEnvironmentVariable("PECKWORKS_MINDLOG");
        if (string.IsNullOrEmpty(path)) return null;
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
        return _writer;
    }
}

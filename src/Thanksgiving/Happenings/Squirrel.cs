using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// A tawny squirrel bounds along the fence's upper rail with an acorn in its
/// mouth, hops up onto a post top, sits up, takes the acorn in its paws and
/// nibbles it (tail curled up behind and flicking, a glance up now and
/// then), then hops round, drops back onto the rail and bounds off the way
/// it came. It stays out of the stretch of fence by the scarecrow and the
/// corn shock, where the rail is busy: it only ever visits the left and
/// right ends of the fence, coming in from the nearer screen edge and going
/// back out of it.
///
/// FEYNMAN VERSION: a flip-book, with the pages picked by DISTANCE while it
/// runs. A squirrel does not run like a dog. It BOUNDS: the forepaws land,
/// the hind feet swing past them and land ahead, the hind legs push off and
/// the whole body flies stretched out, again and again. So one bound is one
/// full turn of the flip-book (16 pages). The page is worked out from how
/// far it has gone, so the legs are always in step with the ground: slow
/// down and the bounds slow with it. The constructor squeezes the bound
/// length a touch so a WHOLE number of bounds fit between the screen edge
/// and the post, which makes the last bound end exactly on the post top
/// (the same trick the turkey uses to stop).
///
/// The drawing and the poses live in SquirrelArt.cs, shared with the acorn
/// caper. In front of the fence (layer 1, no stencil). Facing left is the
/// same sticker turned over.
/// </summary>
internal sealed class Squirrel : Happening
{
    private const int RunPages = 16, RisePages = 5, SitTail = 4, SitNib = 4;
    private const float Speed = 0.27f;           // running speed, U per second
    private const float RiseFor = 0.5f, SitFor = 4.6f, TurnFor = 0.4f;

    private readonly ThanksgivingScenery _s;
    private readonly float _k;
    private readonly int _w, _h, _ox, _by;       // sheet size, the body origin's x in it, and the rail line's y in it
    private readonly Sprite[] _run = new Sprite[RunPages];
    private readonly Sprite[] _rise = new Sprite[RisePages];
    private readonly Sprite[,] _sit = new Sprite[SitTail, SitNib];
    private readonly Sprite[] _look = new Sprite[SitTail];
    private readonly List<int> _cand = new();    // posts it may stop on

    private int _dir = 1, _post;
    private float _stopX, _topRise, _bound, _dist, _t1, _t2;
    private float _total = 9f;

    public override float Seconds => _total;
    public override int Layer => 1;
    public override string? Claims => "fence";
    public override bool CanBegin => _cand.Count > 0;

    public Squirrel(ThanksgivingScenery s)
    {
        _s = s;
        _k = Math.Max(9f, s.U * 0.030f);   // one squirrel is one size: the stories' and the patch's too (Cast.K)
        _w = (int)(2.55f * _k) + 6;
        _h = (int)(1.9f * _k) + 6;
        _ox = (int)(1.75f * _k) + 3;
        _by = _h - 4;

        for (int i = 0; i < s.FencePosts.Length; i++)
        {
            float f = s.FencePosts[i] / s.Width;
            if ((f > 0.08f && f < 0.32f) || (f > 0.64f && f < 0.92f)) _cand.Add(i);
        }

        for (int i = 0; i < RunPages; i++) _run[i] = Page(i / (float)RunPages, 0f, 0f, 0, false);
        float[] rise = [0.2f, 0.45f, 0.7f, 0.9f, 1f];
        for (int i = 0; i < RisePages; i++) _rise[i] = Page(0.97f, rise[i], 0f, 0, false);
        float[] nib = [0f, 0.5f, 1f, 0.5f];
        for (int a = 0; a < SitTail; a++)
        {
            for (int b = 0; b < SitNib; b++) _sit[a, b] = Page(0.97f, 1f, nib[b], a, false);
            _look[a] = Page(0.97f, 1f, 0f, a, true);
        }
    }

    // ----------------------------------------------------------------- dice

    public override void Begin(Random rng)
    {
        if (_cand.Count == 0) { _total = 0.1f; return; }
        _post = _cand[rng.Next(_cand.Count)];
        _stopX = _s.FencePosts[_post];
        _dir = _stopX < _s.Width / 2f ? 1 : -1;                    // it comes from the nearer edge, and goes back to it
        float start = _dir > 0 ? -1.9f * _k : _s.Width + 1.9f * _k;
        _dist = Math.Abs(_stopX - start);
        int bounds = Math.Max(2, (int)MathF.Round(_dist / (2.2f * _k)));
        _bound = _dist / bounds;                                    // a whole number of bounds
        _topRise = 0.30f * _s.FencePostHeight;                      // the post top stands this far over the upper rail
        _t1 = _dist / (Speed * _s.U);
        _t2 = _t1;
        _total = _t1 + RiseFor + SitFor + TurnFor + _t2;
    }

    // ----------------------------------------------------------------- draw

    /// <summary>The upper rail's height at x (the painter's own curve: straight between posts with a small sag). Shared with the acorn caper.</summary>
    internal static float RailAt(ThanksgivingScenery s, float x)
    {
        float[] p = s.FencePosts;
        if (x <= p[0] || x >= p[^1]) return s.RailY(x, 1);
        int i = 0;
        while (i + 2 < p.Length && p[i + 1] <= x) i++;
        float f = (x - p[i]) / (p[i + 1] - p[i]);
        return s.RailY(p[i], 1) + (s.RailY(p[i + 1], 1) - s.RailY(p[i], 1)) * f + s.U * 0.004f * 4 * f * (1 - f) * 0.5f;
    }

    private float RailAt(float x) => RailAt(_s, x);

    public override void Draw(FrameBuffer fb, float t)
    {
        if (_cand.Count == 0) return;
        float stopAt = _t1;                                          // on the post top from here
        float sitAt = stopAt + RiseFor, turnAt = sitAt + SitFor, outAt = turnAt + TurnFor;
        float foot = 1.5f;                                           // feet a hair into the rail, so they stand ON it
        bool mirror;
        float x, y;
        Sprite page;

        if (t < stopAt)
        {
            float dist = _dist * (t / _t1);
            mirror = _dir < 0;
            x = _dir > 0 ? _stopX - _dist + dist : _stopX + _dist - dist;
            y = RailAt(x) - foot;
            // The last bound lands UP on the post top.
            float last = Smooth(((dist - (_dist - _bound)) / _bound - 0.4f) / 0.6f);
            y -= _topRise * last;
            float cyc = dist / _bound;
            page = _run[(int)((cyc - MathF.Floor(cyc)) * RunPages) % RunPages];
        }
        else
        {
            x = _stopX;
            float top = RailAt(x) - foot - _topRise;
            y = top;
            mirror = _dir < 0;
            if (t < sitAt) page = _rise[Math.Min(RisePages - 1, (int)((t - stopAt) / RiseFor * RisePages))];
            else if (t < turnAt)
            {
                float u = t - sitAt;
                // A tail flick every 1.5 s (a quick swish through four poses and back), a nibble burst, a glance up near the end.
                float ft = u % 1.5f;
                int tail = ft < 0.4f ? new[] { 1, 2, 3, 2, 1 }[Math.Min(4, (int)(ft / 0.4f * 5))] : 0;
                bool glance = u > 3.0f && u < 3.7f;
                if (glance) page = _look[tail];
                else
                {
                    float nt = u % 1.1f;
                    int nb = nt < 0.7f ? (int)(nt * 7) % SitNib : 0;   // chew about seven times a second, then pause
                    page = _sit[tail, nb];
                }
            }
            else if (t < outAt)
            {
                // Turn round with a little hop: up, flip at the top, down. The gathered flight page shows the body tucked.
                float s = (t - turnAt) / TurnFor;
                y = top - 4f * s * (1 - s) * 0.5f * _k;
                mirror = s < 0.5f ? _dir < 0 : _dir > 0;
                page = _run[4];
            }
            else
            {
                float dist = _dist * ((t - outAt) / _t2);
                mirror = _dir > 0;                                   // now facing the way it came from
                x = _dir > 0 ? _stopX - dist : _stopX + dist;
                y = RailAt(x) - foot - _topRise * (1 - Smooth(dist / (_bound * 0.6f)));
                float cyc = dist / _bound;
                page = _run[(int)((cyc - MathF.Floor(cyc)) * RunPages) % RunPages];
            }
        }
        int left = (int)MathF.Round(x) - (mirror ? _w - _ox : _ox);
        page.Draw(fb, left, (int)MathF.Round(y) - _by, 1f, null, mirror);
    }

    // ----------------------------------------------------------------- paint

    /// <summary>
    /// One page. phase: where in a bound (0 = the forepaws touching down). sit: 0 = running, 1 = sitting up (in between = rising).
    /// nib: how far the mouth has chewed (0 to 1). tail: which of four tail-flick poses. look: head tipped up.
    /// </summary>
    private Sprite Page(float phase, float sit, float nib, int tail, bool look) =>
        PaintPage(_k, _w, _h, _ox, _by, phase, sit, nib, tail, look, _bound / _k);

    /// <summary>
    /// Paints one page of the squirrel onto a sheet "w" by "h" pixels, the
    /// feet's origin at "ox" across and "by" down, "k" pixels to the squirrel
    /// unit (about its body length). Shared with the acorn caper, which adds:
    /// "acorn" false for a squirrel with nothing in its mouth or paws; "dig"
    /// 1 for the digging pose (forepaws scrabbling by "digPhase"); "rotate",
    /// degrees to turn the whole picture about the feet's origin, so the run
    /// pages become a climb (-90 is head up the trunk, +90 head first down
    /// it); "cling" keeps the body to the bark (no flight lift).
    /// </summary>
    internal static Sprite PaintPage(float k, int w, int h, int ox, int by, float phase, float sit, float nib, int tail, bool look, float bound,
        bool acorn = true, float dig = 0f, float digPhase = 0f, float rotate = 0f, bool cling = false)
    {
        SquirrelArt.Pose q;
        if (dig > 0f) { q = SquirrelArt.Dig(digPhase); q.Acorn = acorn ? 1 : 0; }
        else if (sit > 0f) { q = SquirrelArt.Sit(sit, tail, look, acorn); q.Nib = nib; q.Acorn = acorn ? (sit >= 0.45f ? 2 : 1) : 0; }
        else { q = SquirrelArt.Bound(phase, bound, cling); q.Acorn = acorn ? 1 : 0; }
        q.Rotate = rotate;
        return SquirrelArt.Page(q, k, w, h, ox, by);
    }

    /// <summary>The head alone, poking out of a hole (the caper's hollow): the sheet's origin (ox, by) is the middle of the opening.</summary>
    internal static Sprite PaintPeek(float k, int w, int h, int ox, int by, float outBy, bool blink) =>
        Sprite.Paint(w, h, g =>
        {
            g.TranslateTransform(ox, by);
            g.ScaleTransform(k, -k);
            SquirrelArt.PaintPeek(g, outBy, blink);
        });

    /// <summary>The same acorn the squirrel carries, alone on a small sheet (its middle at the sheet's centre), turned by "angDeg".</summary>
    internal static Sprite PaintAcorn(float k, float angDeg)
    {
        int size = (int)(k * 0.22f) + 4;
        return Sprite.Paint(size, size, g =>
        {
            g.TranslateTransform(size / 2f, size / 2f);
            g.ScaleTransform(k, -k);
            SquirrelArt.Acorn(g, new PointF(0, 0), angDeg);
        });
    }
}

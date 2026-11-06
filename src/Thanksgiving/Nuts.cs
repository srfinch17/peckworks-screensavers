using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;
using Thanksgiving.Happenings;

namespace Thanksgiving;

/// <summary>
/// The acorns on the near ground: where every nut is and what state it is in.
/// A nut that falls from the oak branches lands in the patch and STAYS THERE
/// (his rule: "nuts stay on the ground until squirrels or other things pick
/// them up") until a squirrel fetches it; the squirrel then eats it or buries
/// it, and a buried nut is a stash with an owner, a little mound of earth that
/// another squirrel may dig up. The strong gust shakes a dozen loose at once
/// (Shake), which is what starts the squirrels' frenzy (PatchLife.Frenzy).
///
/// FEYNMAN VERSION: this is a list of nuts and nothing else. Each nut is a
/// little record: where it is, which way up, and one word for what it is
/// doing (Falling, Loose, Carried, Buried, Gone). The AcornDrop happening
/// and the gust only ADD falling nuts; the squirrels' minds (PatchLife) ask
/// "is there a loose nut near me", "is there a stash near me that is not
/// mine", and then Take, Bury, Eat or Drop. The falling itself is the
/// AcornDrop's own sum (fall, two bounces, a roll, from t alone), done here
/// so a nut that lands joins the ground without a seam. A carried nut is
/// not drawn here at all: it is in the squirrel's mouth or paws, part of
/// its pose. Every nut on the ground is stamped against the depth picture
/// with the nearness of its own spot, so a nut behind a pumpkin is hidden
/// by it, the same as a mouse there.
/// </summary>
internal sealed class NutField
{
    public enum State { Falling, Loose, Carried, Buried, Gone }

    public sealed class Nut
    {
        public float X, Y;                          // its spot on the ground (the point it rests on), pixels
        public float Angle;                         // how it lies, radians (a quarter turn is on its side)
        public State State;
        public int Owner = -1;                      // who buried it (a critter's id), or -1
        // The fall: from the twig at (Sx, Sy), starting after Delay seconds; T counts from the drop's beginning.
        public float Sx, Sy, Delay, T, Fall, B1, B2, All, Drift, FinalAngle, LandX;
    }

    private const int Pages = 24, Cap = 24;
    private const float Gravity = 2.6f, HangFor = 0.5f, Restitution = 0.45f, Settle = 0.6f;

    private readonly ThanksgivingScenery _s;
    private readonly Random _rng;
    private readonly Noticeboard _board;
    private readonly StreamWriter? _log;
    private readonly float _a;                      // half the acorn's width, pixels
    private readonly int _size;
    private readonly Sprite[] _page = new Sprite[Pages];
    private readonly Sprite _shadow, _mound;
    private readonly List<Nut> _nuts = new();
    private double _time;

    /// <summary>Where a nut may lie: the patch's own step test (PatchLife sets it), so no nut lands inside a pumpkin's foot.</summary>
    public Ground.Solid? CanStand;

    public NutField(ThanksgivingScenery s, Random sceneRng, Noticeboard board)
    {
        _s = s;
        _board = board;
        _rng = new Random(sceneRng.Next());
        _log = MindLog.Open();
        _a = Math.Max(2f, s.U * 0.0055f);          // smaller than the caper's pile nuts: a nut on the ground beside a 0.030 U squirrel reads at about a third of its length
        _size = (int)(_a * 5f) + 4;
        for (int i = 0; i < Pages; i++) _page[i] = AcornDrop.PaintAcorn(_a, _size, MathF.Tau * i / Pages);
        int sw = (int)(_a * 6f) + 4, sh = (int)(_a * 2f) + 4;
        _shadow = Sprite.Paint(sw, sh, g =>
        {
            for (int i = 0; i < 5; i++)
            {
                float k = 1f - i / 5f;
                using var b = new SolidBrush(Color.FromArgb(34, 30, 16, 6));
                g.FillEllipse(b, sw / 2f - _a * 2.2f * k, sh / 2f - _a * 0.8f * k, _a * 4.4f * k, _a * 1.6f * k);
            }
        });
        // A stash: a low mound of turned earth, darker than the ground, with a few crumbs.
        int mw = (int)(_a * 4f) + 4, mh = (int)(_a * 1.6f) + 4;
        _mound = Sprite.Paint(mw, mh, g =>
        {
            using (var earth = new SolidBrush(Color.FromArgb(200, 96, 62, 30))) g.FillEllipse(earth, 2, 2, mw - 4, mh - 4);
            using (var lit = new SolidBrush(Color.FromArgb(120, 140, 96, 50))) g.FillEllipse(lit, 2 + (mw - 4) * 0.2f, 2, (mw - 4) * 0.5f, (mh - 4) * 0.5f);
            using var crumb = new SolidBrush(Color.FromArgb(70, 44, 22));
            foreach (var (fx, fy) in new[] { (0.15f, 0.6f), (0.8f, 0.5f), (0.5f, 0.85f) })
                g.FillEllipse(crumb, 2 + (mw - 4) * fx, 2 + (mh - 4) * fy, Math.Max(1f, _a * 0.3f), Math.Max(1f, _a * 0.3f));
        });
    }

    public IReadOnlyList<Nut> All => _nuts;
    public float A => _a;
    public bool HasRoom { get { int n = 0; foreach (Nut nut in _nuts) if (nut.State != State.Gone) n++; return n < Cap; } }
    public int LooseCount { get { int n = 0; foreach (Nut nut in _nuts) if (nut.State == State.Loose) n++; return n; } }
    public int FallingCount { get { int n = 0; foreach (Nut nut in _nuts) if (nut.State == State.Falling) n++; return n; } }

    /// <summary>A nut lets go of the twig at "twig" after "delay" seconds, and lands on the patch under it.</summary>
    public void Drop(PointF twig, float delay)
    {
        if (!HasRoom) return;
        float u = _s.U, g = Gravity * u;
        // Where it lands: a row of the patch under the twig, somewhere a critter could stand (so not in a pumpkin's foot).
        float lx = twig.X, ly = _s.WalkY(twig.X) + u * 0.05f * (float)_rng.NextDouble();
        for (int tries = 0; tries < 12 && CanStand != null && !CanStand(lx, ly); tries++)
        {
            lx = twig.X + u * 0.03f * ((float)_rng.NextDouble() * 2 - 1);
            ly = _s.WalkY(lx) + u * 0.06f * (float)_rng.NextDouble();
        }
        var nut = new Nut { State = State.Falling, Sx = twig.X, Sy = twig.Y + _a * 1.8f, Delay = delay, LandX = lx, Y = ly };
        float groundCentre = ly - _a;
        float h0 = Math.Max(_a, groundCentre - nut.Sy);
        nut.Fall = MathF.Sqrt(2 * h0 / g);
        float v = g * nut.Fall;
        nut.B1 = 2 * Restitution * v / g;
        nut.B2 = 2 * Restitution * Restitution * v / g;
        nut.All = nut.Fall + nut.B1 + nut.B2 + Settle;
        nut.Drift = (_rng.Next(2) == 0 ? -1 : 1) * u * (0.025f + 0.025f * (float)_rng.NextDouble());
        nut.FinalAngle = (_rng.Next(2) == 0 ? -1 : 1) * (MathF.PI / 2 + MathF.Tau * _rng.Next(2));
        nut.X = lx;
        _nuts.Add(nut);
    }

    /// <summary>The gust: "n" nuts let go of the corner branches over the next couple of seconds, both sides, well apart.</summary>
    public void Shake(int n)
    {
        float u = _s.U;
        var spots = new List<PointF>();
        foreach (PointF p in _s.CornerBranchSpots)
            if (p.X > _a * 3 && p.X < _s.Width - _a * 3 && p.Y > 0 && p.Y < _s.Height * 0.5f) spots.Add(p);
        var chosen = new List<PointF>();
        for (int tries = 0; tries < 200 && chosen.Count < n && spots.Count > 0; tries++)
        {
            PointF p = spots[_rng.Next(spots.Count)];
            bool apart = true;
            foreach (PointF q in chosen) if (MathF.Abs(q.X - p.X) < u * 0.025f) apart = false;
            if (apart) chosen.Add(p);
        }
        for (int i = 0; i < chosen.Count; i++) Drop(chosen[i], 0.3f + 1.8f * (float)_rng.NextDouble());
        _log?.WriteLine($"{_time,8:F2} nuts shake {chosen.Count} loose");
    }

    // ---- what the squirrels ask ----

    public Nut? NearestLoose(PointF from, float within)
    {
        Nut? best = null; float bd = within * within;
        foreach (Nut nut in _nuts)
        {
            if (nut.State != State.Loose) continue;
            float d = (nut.X - from.X) * (nut.X - from.X) + (nut.Y - from.Y) * (nut.Y - from.Y);
            if (d < bd) { bd = d; best = nut; }
        }
        return best;
    }

    public Nut? NearestStash(int notOwner, PointF from, float within)
    {
        Nut? best = null; float bd = within * within;
        foreach (Nut nut in _nuts)
        {
            if (nut.State != State.Buried || nut.Owner == notOwner) continue;
            float d = (nut.X - from.X) * (nut.X - from.X) + (nut.Y - from.Y) * (nut.Y - from.Y);
            if (d < bd) { bd = d; best = nut; }
        }
        return best;
    }

    public void Take(Nut nut) { nut.State = State.Carried; }
    public void Bury(Nut nut, float x, float y, int owner) { nut.State = State.Buried; nut.X = x; nut.Y = y; nut.Owner = owner; }
    public void Eat(Nut nut) { nut.State = State.Gone; }
    public void DropAt(Nut nut, float x, float y) { nut.State = State.Loose; nut.X = x; nut.Y = y; nut.Angle = MathF.PI / 2; }

    // ---- the frames ----

    public void Update(float dt)
    {
        _time += dt;
        foreach (Nut nut in _nuts)
        {
            if (nut.State != State.Falling) continue;
            float was = nut.T; nut.T += dt;
            float landAt = nut.Delay + HangFor + nut.Fall;
            if (was < landAt && nut.T >= landAt) _board.Post(Sense.Food, new PointF(nut.LandX, nut.Y), 0.15f * _s.U, 0.6f, this);   // the thud: the hungry nearby look
            if (nut.T >= nut.Delay + HangFor + nut.All) { nut.State = State.Loose; nut.X = nut.LandX + nut.Drift; nut.Angle = nut.FinalAngle; _log?.WriteLine($"{_time,8:F2} nut landed at {nut.X:F0},{nut.Y:F0}"); }
        }
        _nuts.RemoveAll(n => n.State == State.Gone);
    }

    public void Draw(FrameBuffer fb)
    {
        float u = _s.U, g = Gravity * u;
        foreach (Nut nut in _nuts)
        {
            switch (nut.State)
            {
                case State.Loose:
                    _shadow.DrawCentered(fb, nut.X, nut.Y - _a * 0.1f, 1f, null, false, _s.NearDepth, _s.Nearness(nut.Y));
                    Stamp(fb, nut.X, nut.Y - _a, nut.Angle, 1f, nut.Y);
                    break;
                case State.Buried:
                    _mound.DrawCentered(fb, nut.X, nut.Y - _a * 0.3f, 1f, null, false, _s.NearDepth, _s.Nearness(nut.Y));
                    break;
                case State.Falling:
                {
                    float local = nut.T - nut.Delay;
                    if (local < 0) break;
                    float x = nut.Sx, cy, angle, height = 0, opacity;
                    if (local < HangFor)
                    {
                        float k = local / HangFor;
                        opacity = Happening_Smooth(local / 0.18f);
                        angle = 0.16f * MathF.Sin(MathF.PI * k) * MathF.Sin(local * 38f);
                        cy = nut.Sy;
                    }
                    else
                    {
                        float tau = local - HangFor;
                        float groundCentre = nut.Y - _a;
                        float h0 = Math.Max(_a, groundCentre - nut.Sy);
                        float v = g * nut.Fall;
                        if (tau < nut.Fall) height = h0 - 0.5f * g * tau * tau;
                        else if (tau < nut.Fall + nut.B1) { float q = tau - nut.Fall; height = Restitution * v * q - 0.5f * g * q * q; }
                        else if (tau < nut.Fall + nut.B1 + nut.B2) { float q = tau - nut.Fall - nut.B1; height = Restitution * Restitution * v * q - 0.5f * g * q * q; }
                        height = MathF.Max(0f, height);
                        float rollK = Math.Clamp((tau - nut.Fall) / (nut.All - nut.Fall), 0f, 1f);
                        x = nut.LandX + nut.Drift * (1 - (1 - rollK) * (1 - rollK)) + (nut.Sx - nut.LandX) * MathF.Max(0f, 1f - tau / nut.Fall);   // slides from the twig's x to the landing x on the way down
                        float tsp = Math.Min(tau, nut.All);
                        float omega = 2 * nut.FinalAngle / nut.All;
                        angle = nut.FinalAngle - omega * (nut.All - tsp) * (nut.All - tsp) / (2 * nut.All);
                        opacity = 1f;
                        cy = groundCentre - height;
                        _shadow.DrawCentered(fb, x, nut.Y - _a * 0.1f, Math.Min(1f, 1.7f / (1f + height / (_a * 2))), null, false, _s.NearDepth, _s.Nearness(nut.Y));
                    }
                    Stamp(fb, x, cy, angle, opacity, local < HangFor ? _s.Height : nut.Y);   // on the twig it is in front of everything
                    break;
                }
            }
        }
    }

    private void Stamp(FrameBuffer fb, float x, float cy, float angle, float opacity, float depthY)
    {
        int page = (int)MathF.Round(((angle / MathF.Tau) % 1f + 1f) % 1f * Pages) % Pages;
        _page[page].Draw(fb, (int)MathF.Round(x) - _size / 2, (int)MathF.Round(cy) - _size / 2, opacity, null, false, _s.NearDepth, _s.Nearness(depthY));
    }

    private static float Happening_Smooth(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3f - 2f * x); }
}

using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// Two or three acorns let go of the left corner branch, one after another.
/// Each hangs on its twig for a moment (a little shiver, as if the wind
/// tugged it), lets go, tumbles down, bounces twice on the ground, rolls on
/// its side a few pixels and stops. Then they all fade away together.
///
/// FEYNMAN VERSION: an acorn is a small flip-book (24 pages, the acorn turned
/// a little more on each) and a plain sum for the fall. A dropped thing speeds
/// up steadily, so the distance fallen is half of gravity times the time
/// squared; from that, the constructor-free sums in Begin work out how long
/// each acorn takes to reach the ground. A bounce gives back less than half
/// the speed it landed with, and a bounce of speed v stays in the air 2v/g
/// seconds. So two small bounces follow the fall, each lower than the last.
///
/// The spin is planned backwards. We know the acorn must END lying on its
/// side (turned a quarter of a turn, plus perhaps a whole turn or two), and
/// that it spins fastest at the start and slows to nothing as it comes to
/// rest. That fixes the spin speed: angle = final - speed * (time left)^2 /
/// (2 * whole time), which starts at zero (the acorn hangs cap up) and ends
/// exactly on its side.
///
/// Where it drops from: the left third of the screen has the left maples,
/// whose leaves are the same dark brown and orange as an acorn, so the
/// constructor prefers twig spots whose straight fall is clear of every
/// maple's canopy. If a screen has none (a very narrow one), the acorns
/// fall in front of the maples, which is allowed (layer 1: in front of
/// everything), the least-bad spots first.
///
/// What it is solid to: the ground (WalkY at its x). It passes in front of
/// the farm, the fence and the maples. The soft shadow on the ground shrinks
/// as the acorn rises, which is what makes the bounces read as height.
/// </summary>
internal sealed class AcornDrop : Happening
{
    private const float Total = 5.2f;
    private const int Pages = 24;
    private const float Gravity = 2.6f;             // U per second squared
    private const float HangFor = 0.5f;             // how long each one shivers on its twig before letting go
    private const float Restitution = 0.45f;        // a bounce keeps this share of the landing speed
    private const float Settle = 0.6f;              // seconds the last roll takes to die away after the last bounce

    private struct Nut
    {
        public float Sx, Sy;                        // where it hangs (pixels)
        public float Start;                         // when it appears on the twig
        public float Fall, B1, B2, All;             // fall time, the two bounce flights, and fall + bounces + settle
        public float Drift;                         // sideways pixels it rolls over the whole landing (signed)
        public float FinalAngle;                    // radians: where it ends, on its side
    }

    private readonly ThanksgivingScenery _s;
    private readonly float _a;                      // half the acorn's width, in pixels
    private readonly int _size;
    private readonly Sprite[] _page = new Sprite[Pages];
    private readonly Sprite _shadow;
    private readonly List<PointF> _clear = [];      // twig spots whose fall misses the maples
    private readonly List<PointF> _other = [];      // the rest, kept in case a screen has too few clear ones
    private Nut[] _nuts = [];
    private bool _usable;

    public override float Seconds => Total;
    public override int Layer => 1;
    public override string? Claims => "branches,cornucopia";   // its acorns can land by the horn, where an apple rolls
    public override bool CanBegin => _usable;

    public AcornDrop(ThanksgivingScenery s)
    {
        _s = s;
        _a = Math.Max(2.5f, s.U * 0.0085f);
        _size = (int)(_a * 5f) + 4;

        // Twig spots in the left third, on screen, in the upper half (a long enough fall to be worth watching).
        foreach (PointF p in s.CornerBranchSpots)
        {
            if (p.X < _a * 3 || p.X >= s.Width / 3f || p.Y < 0 || p.Y > s.Height * 0.5f) continue;
            bool clear = true;
            foreach (var (at, r) in s.Canopies)
                if (MathF.Abs(p.X - at.X) < r * 1.05f + _a * 2) { clear = false; break; }
            (clear ? _clear : _other).Add(p);
        }
        _usable = _clear.Count + _other.Count > 0;

        for (int i = 0; i < Pages; i++)
            _page[i] = PaintAcorn(MathF.Tau * i / Pages);
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
    }

    public override void Begin(Random rng)
    {
        float u = _s.U, g = Gravity * u;
        int n = 2 + rng.Next(2);

        // Pick n spots that are not on top of one another: clear ones first, then the rest.
        var chosen = new List<PointF>();
        void Take(List<PointF> from)
        {
            for (int tries = 0; tries < 40 && chosen.Count < n && from.Count > 0; tries++)
            {
                PointF p = from[rng.Next(from.Count)];
                bool apart = true;
                foreach (PointF q in chosen) if (MathF.Abs(q.X - p.X) < u * 0.04f) apart = false;
                if (apart) chosen.Add(p);
            }
        }
        Take(_clear);
        Take(_other);
        if (chosen.Count == 0) { _nuts = []; return; }

        _nuts = new Nut[chosen.Count];
        for (int i = 0; i < _nuts.Length; i++)
        {
            // Acorns fall in the order they were picked, left to right is not wanted: shuffle comes free from the random picks.
            var nut = new Nut { Sx = chosen[i].X, Sy = chosen[i].Y + _a * 1.8f, Start = 0.1f + i * 0.85f + 0.3f * (float)rng.NextDouble() };
            float groundCentre = _s.WalkY(nut.Sx) - _a;
            float h0 = Math.Max(_a, groundCentre - nut.Sy);
            nut.Fall = MathF.Sqrt(2 * h0 / g);
            float v = g * nut.Fall;
            nut.B1 = 2 * Restitution * v / g;
            nut.B2 = 2 * Restitution * Restitution * v / g;
            nut.All = nut.Fall + nut.B1 + nut.B2 + Settle;
            nut.Drift = (rng.Next(2) == 0 ? -1 : 1) * u * (0.025f + 0.025f * (float)rng.NextDouble());
            nut.FinalAngle = (rng.Next(2) == 0 ? -1 : 1) * (MathF.PI / 2 + MathF.Tau * rng.Next(2));
            _nuts[i] = nut;
        }
    }

    // ------------------------------------------------------------------ draw

    public override void Draw(FrameBuffer fb, float t)
    {
        float u = _s.U, g = Gravity * u;
        float over = Smooth((Total - t) / 0.8f);                  // the whole group fades out together at the end

        foreach (Nut n in _nuts)
        {
            float local = t - n.Start;
            if (local < 0) continue;
            float release = HangFor;
            float x = n.Sx, cy, angle, height = 0, opacity;

            if (local < release)
            {
                // Hanging on the twig: fades in, shivers (the shiver swells and dies away so it lets go calmly).
                float k = local / release;
                opacity = Smooth(local / 0.18f);
                angle = 0.16f * MathF.Sin(MathF.PI * k) * MathF.Sin(local * 38f);
                cy = n.Sy;
            }
            else
            {
                float tau = local - release;
                float groundCentre = _s.WalkY(n.Sx) - _a;
                float h0 = Math.Max(_a, groundCentre - n.Sy);
                float v = g * n.Fall;
                if (tau < n.Fall) height = h0 - 0.5f * g * tau * tau;
                else if (tau < n.Fall + n.B1) { float q = tau - n.Fall; height = Restitution * v * q - 0.5f * g * q * q; }
                else if (tau < n.Fall + n.B1 + n.B2) { float q = tau - n.Fall - n.B1; height = Restitution * Restitution * v * q - 0.5f * g * q * q; }
                height = MathF.Max(0f, height);

                float rollK = Math.Clamp((tau - n.Fall) / (n.All - n.Fall), 0f, 1f);      // the sideways roll starts at the first landing and slows to a stop
                x = n.Sx + n.Drift * (1 - (1 - rollK) * (1 - rollK));

                float tsp = Math.Min(tau, n.All);
                float omega = 2 * n.FinalAngle / n.All;                                    // the spin speed at the start, from the plan above
                angle = n.FinalAngle - omega * (n.All - tsp) * (n.All - tsp) / (2 * n.All);
                opacity = 1f;
                cy = _s.WalkY(x) - _a - height;                              // resting centre minus the height (the fall is the height counting down from the twig)
            }

            opacity *= over;
            if (opacity <= 0) continue;

            if (local >= release)
                _shadow.DrawCentered(fb, x, _s.WalkY(x) - _a * 0.1f, Math.Min(1f, opacity * 1.7f / (1f + height / (_a * 2))));
            int page = (int)MathF.Round(((angle / MathF.Tau) % 1f + 1f) % 1f * Pages) % Pages;
            _page[page].Draw(fb, (int)MathF.Round(x) - _size / 2, (int)MathF.Round(cy) - _size / 2, opacity);
        }
    }

    // ----------------------------------------------------------------- paint

    /// <summary>One acorn turned by "turn" radians, cap up at 0. The sprite's centre is the acorn's middle.</summary>
    private Sprite PaintAcorn(float turn) =>
        Sprite.Paint(_size, _size, g =>
        {
            float a = _a;
            g.TranslateTransform(_size / 2f, _size / 2f);
            g.RotateTransform(turn * 180f / MathF.PI);
            g.TranslateTransform(0, -0.1f * a);

            // The nut: a plump teardrop with a point at the bottom, chestnut brown, a paler stripe where the light catches it.
            PointF[] nutPts = [new(-a, -0.35f * a), new(-0.97f * a, 0.55f * a), new(-0.45f * a, 1.3f * a), new(0, 1.7f * a), new(0.45f * a, 1.3f * a), new(0.97f * a, 0.55f * a), new(a, -0.35f * a)];
            using var nut = new GraphicsPath();
            nut.AddClosedCurve(nutPts, 0.5f);
            using (var b = new SolidBrush(Color.FromArgb(134, 80, 36))) g.FillPath(b, nut);
            GraphicsState st = g.Save();
            g.SetClip(nut, CombineMode.Intersect);
            using (var hi = new SolidBrush(Color.FromArgb(190, 132, 70)))
                g.FillEllipse(hi, -0.75f * a, -0.1f * a, 0.5f * a, 1.5f * a);
            using (var lo = new SolidBrush(Color.FromArgb(90, 60, 32, 14)))
                g.FillEllipse(lo, 0.25f * a, 0.1f * a, 1.0f * a, 1.7f * a);
            g.Restore(st);
            using (var rim = new Pen(Color.FromArgb(150, 56, 32, 14), Math.Max(0.8f, a * 0.22f))) g.DrawPath(rim, nut);
            using (var tip = new SolidBrush(Color.FromArgb(70, 40, 22)))                  // the little dark point
                g.FillEllipse(tip, -0.13f * a, 1.55f * a, 0.26f * a, 0.26f * a);

            // The cap: a dome with a rough scaly look (a few lighter dots), a lighter rim and a stub of stem.
            using var cap = new GraphicsPath();
            cap.AddClosedCurve([new PointF(-1.12f * a, -0.30f * a), new PointF(-0.95f * a, -1.05f * a), new PointF(0, -1.45f * a), new PointF(0.95f * a, -1.05f * a), new PointF(1.12f * a, -0.30f * a), new PointF(0, -0.12f * a)], 0.5f);
            using (var b = new SolidBrush(Color.FromArgb(88, 56, 30))) g.FillPath(b, cap);
            GraphicsState st2 = g.Save();
            g.SetClip(cap, CombineMode.Intersect);
            using (var dot = new SolidBrush(Color.FromArgb(140, 160, 116, 64)))
                foreach (var d in (PointF[])[new(-0.6f, -0.45f), new(0f, -0.4f), new(0.6f, -0.45f), new(-0.3f, -0.85f), new(0.3f, -0.85f), new(0f, -1.2f)])
                {
                    float r = Math.Max(0.5f, a * 0.14f);
                    g.FillEllipse(dot, d.X * a - r, d.Y * a - r, r * 2, r * 2);
                }
            g.Restore(st2);
            using (var rim = new Pen(Color.FromArgb(160, 36, 20, 10), Math.Max(0.8f, a * 0.2f))) g.DrawPath(rim, cap);
            using (var lip = new Pen(Color.FromArgb(190, 150, 104, 56), Math.Max(0.8f, a * 0.16f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(lip, -0.95f * a, -0.28f * a, 0.95f * a, -0.28f * a);
            using (var stem = new Pen(Color.FromArgb(70, 44, 24), Math.Max(1f, a * 0.3f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(stem, 0, -1.4f * a, 0.1f * a, -1.85f * a);
        });
}

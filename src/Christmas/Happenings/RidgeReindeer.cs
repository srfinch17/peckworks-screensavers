using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace Christmas.Happenings;

/// <summary>
/// A reindeer walks up over the far mountain ridge, stands in profile
/// against the night sky, turns its head to look back (a quiet moment, like
/// a deer in a Christmas card), then walks back down out of sight.
///
/// It is a dark silhouette with a thin pale edge, because dark on a dark
/// blue sky would vanish; the pale rim is what lifts it off the sky, the way
/// a snowy edge catches the moonlight.
///
/// It is drawn "only where the sky is open" (the OpenSky stencil). The far
/// ridge was painted over the sky, so the ridge itself hides whatever of the
/// reindeer is below it. That is the whole trick for "rising from behind the
/// hill": the reindeer is just a sprite that starts sunk below the ridge and
/// slides up while it walks, and the stencil cuts it off at the ridge line.
///
/// Three flip-books at once:
///   - the body and legs: 16 walking poses (four legs, each a quarter of a
///     step behind the one before, each foot sliding back along the ground
///     and then lifting and swinging forward). Pose 0 is also the standing
///     pose, so a walk starts and stops without a pop.
///   - the head: three poses (looking ahead, facing us, looking back),
///     crossfaded, so the head turns smoothly while the body stands still.
///   - everything is painted twice, once facing right and once facing left.
///
/// Where it stands is picked in Begin: a spot where the far ridge towers well
/// above the near mountains (so the near mountains do not hide it), is not
/// too steep, and is away from the moon (a pale rim cannot be seen in the
/// moon's glow). If there is no such spot, the showing ends at once.
/// </summary>
internal sealed class RidgeReindeer : Happening
{
    private const int Poses = 16;
    private const float Rise = 3.5f, Stand = 5f, Down = 3.5f;   // seconds: walking up, standing, walking down
    private const float StrideWalk = 0.48f;                      // how far it walks while rising (or sinking), in reindeer heights
    private static readonly Color Dark = Color.FromArgb(24, 30, 60);
    private static readonly Color Rim = Color.FromArgb(205, 150, 172, 222);

    private readonly ChristmasScenery _s;
    private readonly int _hh, _bw, _hs;                          // body sheet height, body sheet width, head sheet size (all pixels)
    private readonly Sprite[,] _body = new Sprite[2, Poses];     // [facing right/left, walking pose]
    private readonly Sprite[,] _head = new Sprite[2, 3];         // [facing right/left, looking ahead / facing us / looking back]
    private bool _ok, _facingLeft;
    private float _x0, _dir;                                     // where it stands, and which way it walks (+1 right, -1 left)

    private float _seconds = Rise + Stand + Down;
    public override float Seconds => _seconds;

    // Where the neck ends and the head begins, as fractions of the body sheet's height.
    private const float NeckX = 0.74f, NeckY = 0.27f;

    public RidgeReindeer(ChristmasScenery s)
    {
        _s = s;
        _hh = Math.Max(18, (int)(s.U * 0.06f));
        _bw = (int)(_hh * 0.85f);
        _hs = (int)(_hh * 0.8f);
        for (int side = 0; side < 2; side++)
        {
            for (int pose = 0; pose < Poses; pose++) _body[side, pose] = PaintBody(side == 1, pose);
            for (int view = 0; view < 3; view++) _head[side, view] = PaintHead(side == 1, view);
        }
    }

    /// <summary>
    /// Paints one shape twice: first fat and pale (the rim), then dark on
    /// top. "grow" is how much fatter the pale pass is. Where two parts of
    /// the animal overlap, the dark pass covers the rim between them, so the
    /// rim only shows around the OUTSIDE of the whole animal.
    /// </summary>
    private static void TwoPass(Graphics g, float rimWidth, Action<Color, float> shapes)
    {
        shapes(Rim, rimWidth);
        shapes(Dark, 0);
    }

    private Sprite PaintBody(bool flip, int pose) => Sprite.Paint(_bw, _hh, g =>
    {
        if (flip) { g.TranslateTransform(_bw, 0); g.ScaleTransform(-1, 1); }
        float H = _hh;
        float rim = Math.Max(1.6f, H * 0.04f);
        float legW = Math.Max(1.8f, H * 0.05f);
        float baseline = H * 0.97f, hipY = H * 0.55f, stepA = H * 0.09f, lift = H * 0.07f;
        PointF P(float x, float y) => new(x * H, y * H);

        TwoPass(g, rim, (col, grow) =>
        {
            using var brush = new SolidBrush(col);
            using var edge = new Pen(col, Math.Max(0.01f, grow)) { LineJoin = LineJoin.Round };
            void Fill(PointF[] pts) { g.FillPolygon(brush, pts); if (grow > 0) g.DrawPolygon(edge, pts); }

            // Torso, neck and tail.
            g.FillEllipse(brush, 0.10f * H, 0.40f * H, 0.60f * H, 0.20f * H);
            if (grow > 0) g.DrawEllipse(edge, 0.10f * H, 0.40f * H, 0.60f * H, 0.20f * H);
            Fill([P(0.52f, 0.46f), P(0.66f, 0.53f), P(0.79f, NeckY + 0.03f), P(0.71f, NeckY - 0.02f)]);
            Fill([P(0.12f, 0.43f), P(0.05f, 0.37f), P(0.10f, 0.52f)]);

            // Four legs. Order: far hind, far fore, near hind, near fore (a lateral walk: each a quarter step after the last).
            (float hip, float offset, float knee)[] legs = [(0.22f, 0.50f, -0.04f), (0.58f, 0.75f, 0.04f), (0.22f, 0f, -0.04f), (0.58f, 0.25f, 0.04f)];
            foreach (var (hipX, offset, knee) in legs)
            {
                float ph = (pose / (float)Poses + offset) % 1f;
                float dx, up = 0;
                if (ph < 0.75f) dx = stepA * (1 - 2 * ph / 0.75f);                      // standing on it: the foot slides back along the ground
                else
                {
                    float s = (ph - 0.75f) / 0.25f;
                    dx = stepA * (-1 + 2 * s);                                          // in the air: it swings forward again
                    up = lift * MathF.Sin(MathF.PI * s);
                }
                PointF hipP = new(hipX * H, hipY), foot = new(hipX * H + dx, baseline - up);
                PointF kneeP = new((hipP.X + foot.X) / 2 + knee * H, (hipP.Y + foot.Y) / 2);
                using var pen = new Pen(col, legW + grow) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                g.DrawLines(pen, [hipP, kneeP, foot]);
            }
        });
    });

    /// <summary>
    /// The head and its antlers. Views: 0 = side-on looking ahead (the way it
    /// walks), 1 = facing us, 2 = looking back over its shoulder (the
    /// mirror of view 0). Anchored on the neck's top, in the middle of the sheet.
    /// </summary>
    private Sprite PaintHead(bool flip, int view) => Sprite.Paint(_hs, _hs, g =>
    {
        // The sheet is flipped if the animal faces left, or if this is the look-back view; both together cancel out.
        bool mirror = flip ^ (view == 2);
        if (mirror && view != 1) { g.TranslateTransform(_hs, 0); g.ScaleTransform(-1, 1); }
        float S = _hs, mid = S / 2f;
        float rim = Math.Max(1.4f, _hh * 0.04f);
        float antler = Math.Max(1.2f, S * 0.045f);
        PointF P(float x, float y) => new(mid + x * S, mid + y * S);
        PointF[] Line(params (float x, float y)[] pts) => [.. pts.Select(p => P(p.x, p.y))];

        TwoPass(g, rim, (col, grow) =>
        {
            using var brush = new SolidBrush(col);
            using var edge = new Pen(col, Math.Max(0.01f, grow)) { LineJoin = LineJoin.Round };
            using var pen = new Pen(col, antler + grow) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            void Fill(PointF[] pts) { g.FillPolygon(brush, pts); if (grow > 0) g.DrawPolygon(edge, pts); }

            if (view == 1)
            {
                // Facing us: an upright oval, two ears, and a symmetrical pair of antlers.
                g.FillEllipse(brush, mid - 0.075f * S, mid - 0.075f * S, 0.15f * S, 0.21f * S);
                if (grow > 0) g.DrawEllipse(edge, mid - 0.075f * S, mid - 0.075f * S, 0.15f * S, 0.21f * S);
                foreach (float sd in new[] { -1f, 1f })
                {
                    Fill(Line((sd * 0.06f, -0.02f), (sd * 0.18f, -0.07f), (sd * 0.07f, -0.07f)));
                    g.DrawLines(pen, Line((sd * 0.04f, -0.07f), (sd * 0.10f, -0.16f), (sd * 0.12f, -0.26f), (sd * 0.09f, -0.34f)));
                    g.DrawLines(pen, Line((sd * 0.10f, -0.16f), (sd * 0.04f, -0.22f)));
                    g.DrawLines(pen, Line((sd * 0.12f, -0.26f), (sd * 0.20f, -0.30f)));
                }
                return;
            }

            // Side-on: a tilted oval head with the muzzle pointing ahead and down, an ear, and two antlers (near and far).
            var state = g.Save();
            g.TranslateTransform(mid + 0.11f * S, mid + 0.04f * S);
            g.RotateTransform(22);
            g.FillEllipse(brush, -0.15f * S, -0.075f * S, 0.30f * S, 0.15f * S);
            if (grow > 0) g.DrawEllipse(edge, -0.15f * S, -0.075f * S, 0.30f * S, 0.15f * S);
            g.Restore(state);
            Fill(Line((-0.02f, -0.03f), (-0.11f, -0.10f), (0.02f, -0.08f)));
            foreach (float shift in new[] { 0f, 0.06f })
            {
                float yShift = shift * 0.2f;
                g.DrawLines(pen, Line((0.01f + shift, -0.05f + yShift), (-0.06f + shift, -0.14f + yShift), (-0.07f + shift, -0.24f + yShift), (-0.04f + shift, -0.33f + yShift)));
                g.DrawLines(pen, Line((-0.06f + shift, -0.14f + yShift), (0.03f + shift, -0.20f + yShift)));
                g.DrawLines(pen, Line((-0.07f + shift, -0.24f + yShift), (0.02f + shift, -0.30f + yShift)));
                g.DrawLines(pen, Line((-0.07f + shift, -0.24f + yShift), (-0.14f + shift, -0.28f + yShift)));
            }
        });
    });

    public override void Begin(Random rng)
    {
        _ok = false;
        _seconds = 0.2f;                                           // "nothing happened", unless a good spot turns up
        _facingLeft = rng.Next(2) == 0;
        _dir = _facingLeft ? -1 : 1;

        float u = _s.U, hh = _hh;
        float reach = u * 0.04f;                                   // the walking range either side of the standing spot
        for (int tries = 0; tries < 80; tries++)
        {
            float x = _s.Width * (0.12f + 0.76f * (float)rng.NextDouble());
            float footY = RidgeY(_s.FarRange, x);

            // Far from the moon, measured to the middle of the animal.
            float mx = x - _s.Moon.At.X, my = footY - hh * 0.5f - _s.Moon.At.Y;
            if (MathF.Sqrt(mx * mx + my * my) < u * 0.15f + _s.Moon.R) continue;

            // The near mountains must sit well below the far ridge all along its walk.
            bool clear = true;
            for (int i = -2; i <= 2 && clear; i++)
            {
                float sx = x + reach * i / 2f;
                clear = RidgeY(_s.NearRange, sx) - RidgeY(_s.FarRange, sx) >= u * 0.07f;
            }
            if (!clear) continue;

            // Not too steep under its feet (a hoof in mid-air looks wrong).
            if (MathF.Abs(RidgeY(_s.FarRange, x - hh * 0.25f) - RidgeY(_s.FarRange, x + hh * 0.25f)) > hh * 0.12f) continue;

            _x0 = x;
            _ok = true;
            _seconds = Rise + Stand + Down;
            return;
        }
    }

    /// <summary>The ridge's height at x, read along the straight lines between its corners (exactly what the painter filled).</summary>
    private static float RidgeY(PointF[] ridge, float x)
    {
        for (int i = 1; i < ridge.Length - 2; i++)
            if (ridge[i].X <= x && x <= ridge[i + 1].X)
            {
                float span = ridge[i + 1].X - ridge[i].X;
                return span <= 0 ? ridge[i].Y : ridge[i].Y + (ridge[i + 1].Y - ridge[i].Y) * (x - ridge[i].X) / span;
            }
        return Brushwork.RidgeYAt(ridge, x);                       // off the ends: the library's nearest-corner answer
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        if (!_ok) return;

        // Where it is: walking up (eased, so it starts and stops gently), standing, then walking down.
        // "sink" is how far below standing height it is, 1 = wholly behind the ridge.
        float x, sink, cycles;
        if (t < Rise)
        {
            float p = Smooth(t / Rise);
            x = _x0 - _dir * StrideWalk * _hh * (1 - p); sink = 1 - p; cycles = 2 * p;
        }
        else if (t < Rise + Stand) { x = _x0; sink = 0; cycles = 0; }
        else
        {
            float p = Smooth((t - Rise - Stand) / Down);
            x = _x0 + _dir * StrideWalk * _hh * p; sink = p; cycles = 2 * p;
        }

        int pose = (int)(cycles * Poses) % Poses;                  // the legs: pose 0 at the start and end of each walk, so standing is seamless
        // A gentle bob while walking: two bobs per stride, a pixel or two at least, rounded once.
        float bobAmp = Math.Max(1f, _s.U * 0.0015f);
        float bob = MathF.Round(bobAmp * MathF.Sin(cycles * MathF.Tau * 2) * (pose == 0 ? 0 : 1));

        float footY = RidgeY(_s.FarRange, x) + _hh * 0.02f;
        int top = (int)MathF.Round(footY - _hh * 0.97f + _hh * 1.06f * sink + bob);
        int left = (int)MathF.Round(x - _bw / 2f);
        int side = _facingLeft ? 1 : 0;
        _body[side, pose].Draw(fb, left, top, 1f, _s.OpenSky);

        // The head: crossfade between neighbouring views as it turns. It looks ahead, turns to us, then looks back, holds, and returns.
        float tt = t - Rise;
        float turn = 2 * Smooth((tt - 0.6f) / 1.3f) - 2 * Smooth((tt - 3.4f) / 1.3f);   // 0 = ahead, 1 = facing us, 2 = looking back
        int lo = Math.Clamp((int)turn, 0, 1);
        float f = turn - lo;
        float ax = left + (_facingLeft ? _bw - NeckX * _hh : NeckX * _hh), ay = top + NeckY * _hh;
        int hl = (int)MathF.Round(ax - _hs / 2f), ht = (int)MathF.Round(ay - _hs / 2f);
        _head[side, lo].Draw(fb, hl, ht, 1 - Smooth(2 * f - 1), _s.OpenSky);
        if (f > 0) _head[side, lo + 1].Draw(fb, hl, ht, Smooth(2 * f), _s.OpenSky);
    }
}

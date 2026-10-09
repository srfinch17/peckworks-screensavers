using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// Dragonflies patrolling the pond: shiokara tombo, the pale blue dragonfly
/// of Japanese ponds, out from April, as the cherries bloom.
///
/// WHAT A DRAGONFLY DOES (watch one over any pond): it HOVERS, almost still,
/// for a second or two; then it DARTS, very fast and dead straight, to a new
/// spot, and stops there just as suddenly. Between darts it swings round to
/// face a new way. Now and then it PERCHES on a lily pad, wings held flat and
/// still, for a while. And the female DIPS: she flies down and touches the
/// tip of her tail to the water to lay an egg, leaving a ring.
///
/// Seen from above, its wings are a shimmering blur in flight (they beat
/// thirty times a second), and clear and still when it perches. Its shadow
/// on the water lies down and to the right, further away the higher it flies.
///
/// The body is rigid, so a dragonfly is a picture (TurningStamp) turned to
/// face its way, never flipped. Three pictures: wings still (perched), and
/// two blur positions swapped every frame for the shimmer.
/// </summary>
internal sealed class Dragonflies
{
    private enum Doing { Hover, Dart, Perch, Dip }

    private struct Fly
    {
        public float X, Y, Height;          // the spot below it (screen pixels), and how high above it (u)
        public float Heading, WantHeading;
        public Doing Doing;
        public float Timer;                 // how long the current doing lasts
        public float FromX, FromY, FromH, ToX, ToY, ToH, Progress, Duration;
        public float Phase;                 // its own jitter rhythm
        public int Kind;                    // 0 = blue male, 1 = golden female
    }

    private readonly Pond _pond;
    private readonly Ripples _ripples;
    private readonly Random _rng;
    private readonly Fly[] _flies;
    private readonly TurningStamp[][] _looks;    // [kind][0 still, 1 blur A, 2 blur B]
    private readonly float _u;
    private int _frame;
    private double _time;

    public Dragonflies(Pond pond, Ripples ripples, int count, Random rng)
    {
        _pond = pond;
        _ripples = ripples;
        _rng = rng;
        _u = pond.U;
        _looks = [Looks(Color.FromArgb(120, 172, 214), Color.FromArgb(30, 34, 40)), Looks(Color.FromArgb(214, 168, 70), Color.FromArgb(70, 50, 24))];
        _flies = new Fly[Math.Max(0, count)];
        for (int i = 0; i < _flies.Length; i++)
        {
            PointF p = RandomWaterPoint();
            _flies[i] = new Fly
            {
                X = p.X, Y = p.Y, Height = 0.06f + 0.05f * (float)rng.NextDouble(),
                Heading = (float)(rng.NextDouble() * MathF.Tau), Doing = Doing.Hover,
                Timer = (float)rng.NextDouble() * 2, Phase = (float)(rng.NextDouble() * 10), Kind = i % 2,
            };
            _flies[i].WantHeading = _flies[i].Heading;
        }
    }

    /// <summary>The three pictures of one kind: body colour, and the colour of its tail tip and markings.</summary>
    private TurningStamp[] Looks(Color body, Color dark)
    {
        float len = _u * 0.046f;                  // nose to tail tip: a little larger than life, so it reads at a glance
        int size = (int)MathF.Ceiling(len * 1.25f) + 4;
        float c = size / 2f;
        TurningStamp Make(float wingAlpha, float swing) => new(size, size, g =>
        {
            // Wings: two pairs, attached at the thorax, reaching out sideways.
            foreach (float side in (ReadOnlySpan<float>)[-1, 1])
                for (int pair = 0; pair < 2; pair++)
                {
                    float rootX = c + len * (pair == 0 ? 0.13f : 0.05f);
                    float sweep = (pair == 0 ? 0.12f : -0.18f) + swing;   // forewings a touch forward, hindwings back
                    float wl = len * 0.47f, ww = len * (pair == 0 ? 0.085f : 0.105f);
                    GraphicsState st = g.Save();
                    g.TranslateTransform(rootX, c);
                    g.RotateTransform((side * (90 - sweep * 57.3f)));
                    using (var wing = new SolidBrush(Color.FromArgb((int)(wingAlpha * 255), 236, 242, 246)))
                        g.FillEllipse(wing, 0, -ww / 2, wl, ww);
                    using (var edge = new Pen(Color.FromArgb((int)(wingAlpha * 180), 60, 70, 76), Math.Max(0.6f, len * 0.006f)))
                        g.DrawEllipse(edge, 0, -ww / 2, wl, ww);
                    using (var spot = new SolidBrush(Color.FromArgb((int)(MathF.Min(1, wingAlpha * 2.2f) * 255), dark)))
                        g.FillEllipse(spot, wl * 0.82f, -ww * 0.18f, wl * 0.08f, ww * 0.36f);   // the dark spot near each wing tip
                    g.Restore(st);
                }
            // The abdomen: long and slim, banded, with a dark tip.
            float tail = c - len * 0.55f, root = c + len * 0.02f, aw = len * 0.075f;
            using (var abd = new GraphicsPath())
            {
                abd.AddPolygon([new PointF(root, c - aw / 2), new PointF(tail + len * 0.04f, c - aw * 0.36f), new PointF(tail, c),
                    new PointF(tail + len * 0.04f, c + aw * 0.36f), new PointF(root, c + aw / 2)]);
                using var ab = new LinearGradientBrush(new PointF(0, c - aw), new PointF(0, c + aw), Brushwork(body, 1.15f), Brushwork(body, 0.8f));
                g.FillPath(ab, abd);
            }
            using (var band = new Pen(Color.FromArgb(110, dark), Math.Max(0.6f, len * 0.008f)))
                for (int k = 1; k < 8; k++)
                {
                    float x = root - (root - tail) * k / 8.5f;
                    g.DrawLine(band, x, c - aw * 0.45f, x, c + aw * 0.45f);
                }
            using (var tip = new SolidBrush(dark))
                g.FillEllipse(tip, tail - len * 0.01f, c - aw * 0.3f, len * 0.13f, aw * 0.6f);
            // Thorax and head: a broad chest and two big round eyes.
            using (var th = new SolidBrush(Brushwork(body, 0.75f)))
                g.FillEllipse(th, c - len * 0.02f, c - len * 0.06f, len * 0.2f, len * 0.12f);
            using (var eye = new SolidBrush(Color.FromArgb(58, 96, 104)))
            {
                float er = len * 0.052f;
                g.FillEllipse(eye, c + len * 0.17f - er, c - er * 1.6f, er * 2, er * 2);
                g.FillEllipse(eye, c + len * 0.17f - er, c - er * 0.4f, er * 2, er * 2);
            }
        });
        return [Make(0.5f, 0), Make(0.3f, 0.18f), Make(0.24f, -0.12f)];
    }

    private static Color Brushwork(Color c, float k) =>
        Color.FromArgb(Math.Min(255, (int)(c.R * k)), Math.Min(255, (int)(c.G * k)), Math.Min(255, (int)(c.B * k)));

    private PointF RandomWaterPoint()
    {
        for (int tries = 0; tries < 200; tries++)
        {
            float x = (float)_rng.NextDouble() * _pond.Width, y = (float)_rng.NextDouble() * _pond.Height;
            int i = (int)y * _pond.Width + (int)x;
            if (_pond.Water[i] && _pond.Open[i]) return new PointF(x, y);
        }
        return _pond.Centre;
    }

    public void Update(float dt)
    {
        _time += dt;
        _frame++;
        for (int i = 0; i < _flies.Length; i++)
        {
            ref Fly f = ref _flies[i];
            // Turning to face its way: quick, as dragonflies do (a full half turn in a fraction of a second).
            float dh = MathF.IEEERemainder(f.WantHeading - f.Heading, MathF.Tau);
            f.Heading += Math.Clamp(dh, -9 * dt, 9 * dt);
            f.Timer -= dt;
            switch (f.Doing)
            {
                case Doing.Hover:
                    if (f.Timer <= 0) Next(ref f);
                    break;
                case Doing.Dart:
                case Doing.Dip:
                    // Wait until it faces the way it is going, then go: fast, easing to a dead stop.
                    if (MathF.Abs(dh) > 0.25f && f.Progress == 0) break;
                    f.Progress = MathF.Min(1, f.Progress + dt / f.Duration);
                    float e = 1 - MathF.Pow(1 - f.Progress, 3);      // fast start, sudden-feeling stop
                    f.X = f.FromX + (f.ToX - f.FromX) * e;
                    f.Y = f.FromY + (f.ToY - f.FromY) * e;
                    if (f.Doing == Doing.Dip)
                    {
                        // Down to the water and back up: a quick touch at the middle of the move.
                        float touch = MathF.Sin(f.Progress * MathF.PI);
                        float was = f.Height;
                        f.Height = f.FromH + (0.0f - f.FromH) * touch;
                        if (was > 0.004f && f.Height <= 0.004f) _ripples.Add(f.X - MathF.Cos(f.Heading) * _u * 0.016f, f.Y - MathF.Sin(f.Heading) * _u * 0.016f, 0.6f);
                    }
                    else f.Height = f.FromH + (f.ToH - f.FromH) * e;
                    if (f.Progress >= 1) { f.Doing = f.ToH < 0.01f && f.Doing == Doing.Dart ? Doing.Perch : Doing.Hover; f.Timer = f.Doing == Doing.Perch ? 3 + 5 * (float)_rng.NextDouble() : 0.6f + 1.9f * (float)_rng.NextDouble(); }
                    break;
                case Doing.Perch:
                    if (f.Timer <= 0) { Go(ref f, RandomNear(f.X, f.Y, 0.08f, 0.2f), 0.06f + 0.05f * (float)_rng.NextDouble(), Doing.Dart); }
                    break;
            }
        }
    }

    /// <summary>What next, after a hover: usually another dart; sometimes a perch on a lily pad, or (a female) a dip.</summary>
    private void Next(ref Fly f)
    {
        double roll = _rng.NextDouble();
        if (roll < 0.12 && _pond.Pads.Count > 0)
        {
            LilyPad pad = _pond.Pads[_rng.Next(_pond.Pads.Count)];
            float a = (float)(_rng.NextDouble() * MathF.Tau);
            var spot = new PointF(pad.Center.X + MathF.Cos(a) * pad.Radius * 0.55f, pad.Center.Y + MathF.Sin(a) * pad.Radius * 0.45f);
            Go(ref f, spot, 0.003f, Doing.Dart);
        }
        else if (roll < 0.24 && f.Kind == 1)
            Go(ref f, RandomNear(f.X, f.Y, 0.04f, 0.1f), f.Height, Doing.Dip);
        else
            Go(ref f, RandomNear(f.X, f.Y, 0.06f, 0.26f), 0.05f + 0.07f * (float)_rng.NextDouble(), Doing.Dart);
    }

    private void Go(ref Fly f, PointF to, float height, Doing doing)
    {
        f.FromX = f.X; f.FromY = f.Y; f.FromH = f.Height;
        f.ToX = to.X; f.ToY = to.Y; f.ToH = height;
        float d = MathF.Sqrt((to.X - f.X) * (to.X - f.X) + (to.Y - f.Y) * (to.Y - f.Y));
        f.Duration = MathF.Max(0.15f, d / (_u * 0.9f)) * (doing == Doing.Dip ? 2.2f : 1f);   // darts: nearly a screen height a second
        f.Progress = 0;
        f.Doing = doing;
        if (d > 1) f.WantHeading = MathF.Atan2((to.Y - f.Y) / Ripples.Squash, to.X - f.X);
    }

    /// <summary>A spot over open water between near and far (in u) from here, preferring ones not hidden by the branches.</summary>
    private PointF RandomNear(float x, float y, float near, float far)
    {
        for (int tries = 0; tries < 60; tries++)
        {
            float a = (float)(_rng.NextDouble() * MathF.Tau), d = _u * (near + (far - near) * (float)_rng.NextDouble());
            float nx = x + MathF.Cos(a) * d, ny = y + MathF.Sin(a) * d * Ripples.Squash;
            if (nx < 0 || ny < 0 || nx >= _pond.Width || ny >= _pond.Height) continue;
            int i = (int)ny * _pond.Width + (int)nx;
            if (_pond.Water[i] && _pond.Open[i]) return new PointF(nx, ny);
        }
        return RandomWaterPoint();
    }

    public void DrawShadows(FrameBuffer fb)
    {
        var shade = Color.FromArgb(8, 24, 26);
        foreach (Fly f in _flies)
        {
            float h = f.Height * _u;
            float a = 0.28f * (1 - MathF.Min(0.6f, f.Height * 4));       // fainter the higher it is (the shadow spreads)
            _looks[f.Kind][0].Draw(fb, f.X + h * Light.ShadowX, f.Y + h * Light.ShadowY, f.Heading, 1f, Ripples.Squash, a, shade, _pond.Open);
        }
    }

    public void Draw(FrameBuffer fb)
    {
        foreach (Fly f in _flies)
        {
            int look = f.Doing == Doing.Perch && f.Height < 0.01f ? 0 : 1 + (_frame + (int)f.Phase) % 2;
            float jitter = f.Doing == Doing.Hover ? _u * 0.0015f : 0;
            float t = (float)(_time % 10000.0) + f.Phase;
            float x = f.X + jitter * MathF.Sin(t * 7.3f), y = f.Y - f.Height * _u * Light.Lift + jitter * MathF.Sin(t * 5.1f + 1);
            float near = 1 + f.Height * 0.9f;                            // higher = nearer to us = a touch bigger
            _looks[f.Kind][look].Draw(fb, x, y, f.Heading, near, Ripples.Squash, 1f, null, _pond.Open);
        }
    }
}

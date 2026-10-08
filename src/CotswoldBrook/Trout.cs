using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Photo;

namespace CotswoldBrook;

/// <summary>
/// Brown trout in the brook, seen from the bank.
///
/// WHAT A TROUT LOOKS LIKE FROM THE BANK: not a picture-book fish. You see a
/// long dark torpedo shape just under the surface, a little blurry because
/// you are looking through water, olive-brown on top with a paler flank,
/// and the tail sweeping slowly from side to side. Most of the time it
/// HOLDS: it faces upstream and swims just hard enough to stay in one
/// place, drifting back a little and nudging forward. Now and then it darts
/// to a new spot, and now and then it turns round. That is all this class
/// does.
///
/// THE SHAPE is painted once at startup as sprites (Core/Sprite.cs): for
/// each of three sizes (far, middle, near water), 24 poses of the tail
/// swinging, so the swim reads as smooth (6 poses looked like snapping,
/// see the repo rules). The fish is seen from the bank at a low angle, so
/// it is painted flattened: long and thin.
///
/// Drawn after the ripples, only on open water (the stencil), and given a
/// small wobble of its own so it looks to be under the moving surface.
/// </summary>
internal sealed class Trout
{
    private struct Fish
    {
        public float X, Y;            // where it is now
        public float HomeX, HomeY;    // the spot it holds
        public int Facing;            // +1 = facing right (upstream), -1 = left
        public float Tail;            // the tail's swing, 0 to 1 = one full beat
        public float Speed;           // how fast it is moving now, pixels per second
        public double NextMove;       // when it next darts somewhere
        public float Size;            // 0 far ... 2 near (picks the sprite set)
    }

    private const int Poses = 24;
    private readonly PhotoBackdrop _photo;
    private readonly Random _rng;
    private readonly Fish[] _fish;
    private readonly Sprite[][] _poses = new Sprite[3][];   // [size][pose]
    private double _time;

    public Trout(PhotoBackdrop photo, int count, Random rng)
    {
        _photo = photo;
        _rng = rng;
        // Lengths of the three sizes, as fractions of the photo's height.
        float[] lengths = [0.060f, 0.085f, 0.115f];
        for (int s = 0; s < 3; s++)
        {
            int len = Math.Max(10, (int)(photo.P * lengths[s]));
            _poses[s] = new Sprite[Poses];
            for (int i = 0; i < Poses; i++)
                _poses[s][i] = PaintFish(len, MathF.Sin(i * MathF.Tau / Poses));
        }

        _fish = new Fish[photo.WaterBottom > photo.WaterTop ? count : 0];
        for (int i = 0; i < _fish.Length; i++)
        {
            PointF home = PickSpot(i);
            _fish[i] = new Fish
            {
                X = home.X, Y = home.Y, HomeX = home.X, HomeY = home.Y,
                Facing = rng.NextDouble() < 0.75 ? 1 : -1,
                Tail = (float)rng.NextDouble(),
                NextMove = 3 + 10 * rng.NextDouble(),
            };
            _fish[i].Size = SizeAt(home.Y);
        }
    }

    /// <summary>
    /// A spot of open water for a fish to hold, well inside the water (not at
    /// the soft edge), in the nearer two thirds of the brook where a fish is
    /// big enough to read, and not too close to another fish.
    /// </summary>
    private PointF PickSpot(int self)
    {
        int w = _photo.Width;
        // If no spot passes every test, settle for any open water; if there is
        // none of that either, stay where it is (never a spot on dry land).
        PointF? fallback = null;
        for (int tries = 0; tries < 200; tries++)
        {
            float x = (float)(_rng.NextDouble() * w);
            float y = _photo.WaterTop + (_photo.WaterBottom - _photo.WaterTop) * (0.30f + 0.62f * (float)_rng.NextDouble());
            if (!SurelyWater(x, y)) continue;
            fallback ??= new(x, y);
            if (!BrightEnough(x, y)) continue;
            bool crowded = false;
            for (int j = 0; j < _fish.Length && !crowded; j++)
                if (j != self && _fish[j].HomeX != 0 && MathF.Abs(_fish[j].HomeX - x) < _photo.P * 0.13f && MathF.Abs(_fish[j].HomeY - y) < _photo.P * 0.03f)
                    crowded = true;
            if (!crowded) return new(x, y);
        }
        return fallback ?? new(_fish[self].X, _fish[self].Y);
    }

    /// <summary>
    /// Is the water here light enough to see a dark fish against? Over the
    /// dark reflection of the reeds a trout is invisible (as it would be to
    /// someone on the bank); over the golden reflection of the houses or the
    /// sky it shows up. Averaged over the fish's length, in the golden photo.
    /// </summary>
    private bool BrightEnough(float x, float y)
    {
        int py = (int)y, sum = 0, n = 0;
        for (float dx = -_photo.P * 0.05f; dx <= _photo.P * 0.05f; dx += 2)
        {
            int px = (int)(x + dx);
            if (px < 0 || px >= _photo.Width) continue;
            uint c = _photo.Pixels[py * _photo.Width + px];
            sum += (int)(((c >> 16) & 0xFF) * 3 + ((c >> 8) & 0xFF) * 6 + (c & 0xFF)) / 10;
            n++;
        }
        return n > 0 && sum / n > 105;
    }

    /// <summary>Open water here, and also a fish-length to either side (so the whole fish fits).</summary>
    private bool SurelyWater(float x, float y)
    {
        float reach = _photo.P * 0.06f;
        foreach (float dx in new[] { -reach, 0, reach })
        {
            int px = (int)(x + dx), py = (int)y;
            if (px < 0 || px >= _photo.Width || py < 0 || py >= _photo.Height) return false;
            if (_photo.Water[py * _photo.Width + px] < 255) return false;
        }
        return true;
    }

    /// <summary>0 far ... 2 near, from how far down the water the fish is.</summary>
    private float SizeAt(float y) =>
        Math.Clamp((y - _photo.WaterTop) / Math.Max(1f, _photo.WaterBottom - _photo.WaterTop) * 3f - 0.5f, 0, 2);

    public void Update(double dt)
    {
        _time += dt;
        float p = _photo.P;
        for (int i = 0; i < _fish.Length; i++)
        {
            ref Fish f = ref _fish[i];
            // Time to go somewhere else? Usually a short dart to a new spot;
            // one time in four it turns round first.
            if (_time >= f.NextMove)
            {
                PointF next = PickSpot(i);
                f.HomeX = next.X; f.HomeY = next.Y;
                if (_rng.NextDouble() < 0.25) f.Facing = -f.Facing;
                f.NextMove = _time + 6 + 14 * _rng.NextDouble();
            }

            // Swim toward home. Far away: a quick dart; close: barely moving,
            // just holding against the current with a slow drift back and forth.
            float dx = f.HomeX - f.X, dy = f.HomeY - f.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            float want = Math.Min(p * 0.20f, dist * 1.6f);                      // pixels per second
            f.Speed += (want - f.Speed) * Math.Min(1f, (float)dt * 3f);
            if (dist > 0.5f)
            {
                f.X += dx / dist * f.Speed * (float)dt;
                f.Y += dy / dist * f.Speed * (float)dt;
                // A fish swims head first: when it darts the other way, it turns to face that way.
                if (MathF.Abs(dx) > p * 0.02f) f.Facing = dx > 0 ? 1 : -1;
            }
            // Holding against the current: a slow surge forward and back.
            float hold = MathF.Sin((float)(_time * 0.4 % Math.Tau) + i * 1.7f) * p * 0.004f;
            f.X += hold * (float)dt;

            // The tail beats faster when it swims harder: one beat per second
            // holding, three when darting.
            f.Tail = (f.Tail + (float)dt * (1.0f + 2.0f * Math.Min(1f, f.Speed / (p * 0.1f)))) % 1f;
            f.Size += (SizeAt(f.Y) - f.Size) * Math.Min(1f, (float)dt);   // changes size smoothly as it moves nearer or farther
        }
    }

    /// <param name="opacity">1 in daylight; less at dusk, when there is less light to see into the water.</param>
    public void Draw(FrameBuffer fb, float opacity)
    {
        if (opacity <= 0.01f) return;
        foreach (ref readonly Fish f in _fish.AsSpan())
        {
            Sprite s = _poses[(int)MathF.Round(f.Size)][(int)(f.Tail * Poses) % Poses];
            // The surface's own wobble, so the fish looks to be under it.
            float wob = MathF.Sin((float)(_time * 1.3 % Math.Tau) + f.Y * 0.05f) * _photo.P * 0.0012f;
            s.DrawCentered(fb, MathF.Round(f.X + wob), MathF.Round(f.Y), opacity, _photo.OpenWater, mirror: f.Facing < 0);
        }
    }

    /// <summary>
    /// One pose of a trout facing right, length "len" pixels, tail swung by
    /// "swing" (-1 to 1). The body is a torpedo; the back half bends with
    /// the swing and the tail fin flicks further, as a real fish's does.
    /// Painted soft (a gradient that fades at the edge) because it is seen
    /// through water.
    /// </summary>
    private static Sprite PaintFish(int len, float swing)
    {
        float h = len * 0.21f;                                   // flattened by the low viewing angle
        int sw = len + 4, sh = (int)(h * 3) + 4;
        return Sprite.Paint(sw, sh, g =>
        {
            float cy = sh / 2f, x0 = 2, head = x0 + len;
            // The spine: straight at the head, bending more toward the tail.
            PointF Spine(float t) => new(head - t * len, cy + swing * h * 0.9f * t * t);
            using var body = new GraphicsPath();
            var top = new List<PointF>();
            var bottom = new List<PointF>();
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f;
                // Thickness along the body: a rounded but narrow nose, widest
                // a third of the way back, tapering to the tail's root. (The
                // curve "square root of t times (1 - t)" has that shape; 0.385
                // is its peak, dividing by it makes the widest point exactly h.)
                float thick = h * 0.5f * MathF.Sqrt(t) * (1 - t) / 0.385f + h * 0.05f;
                PointF c = Spine(t * 0.82f);
                top.Add(new(c.X, c.Y - thick));
                bottom.Add(new(c.X, c.Y + thick));
            }
            bottom.Reverse();
            body.AddClosedCurve([.. top, .. bottom], 0.3f);

            // The tail fin: a small fan off the end of the spine, flicked further than the body.
            PointF root = Spine(0.82f);
            PointF tip = Spine(1f);
            tip.Y += swing * h * 0.6f;
            using var fin = new GraphicsPath();
            fin.AddPolygon([root, new(tip.X, tip.Y - h * 0.55f), new(tip.X + h * 0.25f, tip.Y), new(tip.X, tip.Y + h * 0.55f)]);

            // Dark olive-brown, darkest along the back, softer at the edges:
            // a gradient from the middle out.
            using (var soft = new PathGradientBrush(body)
            {
                CenterPoint = Spine(0.35f),
                CenterColor = Color.FromArgb(245, 26, 27, 18),
                SurroundColors = [Color.FromArgb(150, 34, 34, 24)],
            })
                g.FillPath(soft, body);
            using (var finBrush = new SolidBrush(Color.FromArgb(170, 30, 31, 22)))
                g.FillPath(finBrush, fin);
        });
    }
}

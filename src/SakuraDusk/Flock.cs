using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace SakuraDusk;

/// <summary>
/// A loose flock of distant birds crossing the evening sky.
///
/// Each bird is the illustrator's shorthand (two curved wing strokes
/// meeting in the middle), painted once per wing position as a sprite
/// (Core/Sprite.cs) and stamped each frame, like a flip-book. The flock
/// drifts slowly across as a group; inside it, every bird keeps its own
/// place, bobs a little, and beats its wings on its own rhythm, with a
/// glide now and then (wings held still, slightly raised), which is what
/// keeps seven birds from looking like one bird copied seven times.
///
/// The birds are only stamped where the backdrop is open sky (the painter
/// hands over a stencil), so they pass behind the blossom branches that
/// reach in from the top corners, and in front of the sun. (They fly well
/// above the hills, so in practice only the branches ever cover them.)
/// </summary>
internal sealed class Flock
{
    private struct Bird
    {
        public float OffsetX, OffsetY;   // place in the flock, relative to its center
        public float Bob, BobSpeed;      // a gentle rise and fall of its own
        public float Flap, FlapSpeed;    // where the wings are in their beat, and how fast
        public float Lift;               // where the wing tips are right now: -1 = down ... +1 = up
        public float GlidePhase;         // a slow clock that decides when this bird glides
        public int Size;
    }

    // Enough wing positions that one beat steps through them in small
    // moves; with only a handful the wings visibly snap from pose to pose.
    private const int Poses = 24;
    private const float LiftLow = -0.5f, LiftHigh = 0.95f;   // the range of wing positions the poses cover

    private readonly Sprite[,] _sprites;   // [size, pose]
    private readonly Bird[] _birds;
    private readonly int _w;
    private readonly float _speed, _startX, _centerY, _u;
    private double _time;

    public Flock(int width, int height, float u, Random rng)
    {
        _w = width;
        _u = u;
        _centerY = height * 0.40f;
        _speed = u * 0.018f * (rng.Next(2) == 0 ? 1 : -1);   // a slow crossing, either direction
        _startX = width * (0.25f + 0.4f * (float)rng.NextDouble());

        float[] spans = [u * 0.010f, u * 0.013f, u * 0.016f];   // half wingspans: far, middle, near
        _sprites = new Sprite[spans.Length, Poses];
        for (int s = 0; s < spans.Length; s++)
            for (int p = 0; p < Poses; p++)
                _sprites[s, p] = PaintBird(spans[s], u, LiftLow + (LiftHigh - LiftLow) * p / (Poses - 1f));

        _birds = new Bird[7];
        for (int i = 0; i < _birds.Length; i++)
        {
            int size = rng.Next(spans.Length);
            // Pick a place in the flock that does not overlap a bird already
            // placed: try random spots until one is clear (or give up after
            // many tries and take the last one). Two birds are "clear" of
            // each other when they are apart across OR apart in height.
            float ox = 0, oy = 0;
            for (int attempt = 0; attempt < 60; attempt++)
            {
                ox = u * 0.26f * ((float)rng.NextDouble() - 0.5f);
                oy = u * 0.12f * ((float)rng.NextDouble() - 0.5f);
                bool clear = true;
                for (int j = 0; j < i && clear; j++)
                {
                    float apartX = (spans[size] + spans[_birds[j].Size]) * 1.5f;
                    clear = MathF.Abs(ox - _birds[j].OffsetX) > apartX || MathF.Abs(oy - _birds[j].OffsetY) > u * 0.032f;
                }
                if (clear) break;
            }
            _birds[i] = new Bird
            {
                OffsetX = ox,
                OffsetY = oy,
                Bob = (float)(rng.NextDouble() * Math.PI * 2),
                BobSpeed = 0.5f + 0.6f * (float)rng.NextDouble(),
                Flap = (float)(rng.NextDouble() * Math.PI * 2),
                FlapSpeed = 6f + 3f * (float)rng.NextDouble(),   // about one to one and a half beats a second
                Lift = 0.45f,
                GlidePhase = (float)(rng.NextDouble() * Math.PI * 2),
                Size = size,
            };
        }
    }

    /// <summary>
    /// One bird: two wing strokes from the tips down to the body. "lift" is
    /// how high the wing tips are above the body, in half wingspans: negative
    /// means the wings are beating downward.
    ///
    /// A real wing bends: on the downstroke the tips trail behind the
    /// shoulders, on the upstroke they trail the other way. So the steering
    /// point of each stroke is pulled a little against the direction of the
    /// tips, which bows the wing and makes the beat look fluid instead of
    /// like two stiff sticks hinging.
    /// </summary>
    private static Sprite PaintBird(float s, float u, float lift)
    {
        int size = (int)(s * 2.4f) + 4;
        float cx = size / 2f, cy = size * 0.55f;
        return Sprite.Paint(size, size, g =>
        {
            using var pen = new Pen(Color.FromArgb(220, 44, 22, 56), Math.Max(1f, u * 0.0025f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            float tipY = cy - s * lift;
            float bend = -s * lift * 0.35f;                              // the bow: against the tips
            var leftTip = new PointF(cx - s, tipY);
            var rightTip = new PointF(cx + s, tipY);
            var body = new PointF(cx, cy);
            g.DrawBezier(pen, leftTip, new PointF(cx - s * 0.55f, cy + (tipY - cy) * 0.15f + bend), new PointF(cx - s * 0.25f, cy + bend * 0.3f), body);
            g.DrawBezier(pen, body, new PointF(cx + s * 0.25f, cy + bend * 0.3f), new PointF(cx + s * 0.55f, cy + (tipY - cy) * 0.15f + bend), rightTip);
        });
    }

    public void Update(float dt)
    {
        _time += dt;
        for (int i = 0; i < _birds.Length; i++)
        {
            ref Bird b = ref _birds[i];
            // Each phase is an angle, so it is kept wrapped to one turn
            // (Tau = 2 pi). A float that just kept growing would, after a few
            // days of running, be too coarse to register one frame's step.
            b.Bob = (b.Bob + b.BobSpeed * dt) % MathF.Tau;
            b.GlidePhase = (b.GlidePhase + 0.35f * dt) % MathF.Tau;
            // Gliding: when this bird's slow clock is high, the wings stop
            // beating and settle slightly raised. Otherwise they beat.
            bool gliding = MathF.Sin(b.GlidePhase) > 0.55f;
            if (!gliding) b.Flap = (b.Flap + b.FlapSpeed * dt) % MathF.Tau;
            float target = gliding ? 0.45f : MathF.Sin(b.Flap);

            // Ease toward the target instead of jumping to it: each frame the
            // wings close a fraction of the remaining gap. While beating the
            // fraction is large (the wings follow the beat closely); while
            // settling into or out of a glide it is small, so the change is
            // gentle rather than a snap.
            float ease = gliding ? 4f : 18f;
            b.Lift += (target - b.Lift) * MathF.Min(1f, ease * dt);
        }
    }

    public void Draw(FrameBuffer fb, bool[] openSky)
    {
        // The flock's center comes straight from the clock, wrapped so the
        // flock crosses, spends a while out of sight, and comes back in from
        // the side it first appeared on (it keeps flying the same way).
        float span = _u * 0.3f;                                   // the flock's width, so it is fully off screen before wrapping
        double trip = _w + 2 * span + _u * 0.8f;
        double along = ((_time * Math.Abs(_speed) + _startX + span) % trip) - span;
        float centerX = _speed > 0 ? (float)along : _w - (float)along;

        foreach (ref readonly Bird b in _birds.AsSpan())
        {
            float x = centerX + b.OffsetX;
            float y = _centerY + b.OffsetY + _u * 0.006f * MathF.Sin(b.Bob);
            int pose = Math.Clamp((int)MathF.Round((b.Lift - LiftLow) / (LiftHigh - LiftLow) * (Poses - 1)), 0, Poses - 1);
            _sprites[b.Size, pose].DrawCentered(fb, x, y, 1f, openSky);
        }
    }
}

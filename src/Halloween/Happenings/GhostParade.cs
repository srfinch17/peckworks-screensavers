using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Halloween.Happenings;

/// <summary>
/// Three little ghosts float across the sky in single file, follow-the-leader,
/// along one wavy path. The third ghost is smaller and keeps dawdling behind,
/// then hurries to catch up: that is the charm of it.
///
/// THE PATH: one gentle wave, "height = base + wave * sin(across)". The
/// leader's across-position moves at a steady speed. The second ghost is the
/// same, a fixed gap behind. Because every ghost reads its height off the SAME
/// wave at its own across-position, they all trace the same ribbon, like
/// ducklings: where the leader bobbed up, the ones behind bob up a moment later.
///
/// THE STRAGGLER: the third ghost's gap is not fixed. It swings between about
/// 1.6 and 3.4 gaps behind the leader on a slow sine wave: it falls back, then
/// speeds up to close the distance.
///
/// THE SHEETS: classic sheet ghosts, painted once as a flip-book of 16 poses.
/// Only the hem changes from pose to pose: its scallops are a wave that slides
/// along, like a flag in a breeze. Each ghost is painted leaning a little
/// forward, facing right; for the other direction every sprite is painted
/// mirrored. A pale violet rim keeps them readable against the pale moon.
///
/// Each ghost is see-through (opacity 0.6) over a soft glow of its own light.
/// It all goes through OpenSky, so they slip behind trees and branches.
/// </summary>
internal sealed class GhostParade : Happening
{
    private const int Poses = 16;
    private static readonly Color Sheet = Color.FromArgb(226, 236, 255);        // pale blue-white
    private static readonly Color Rim = Color.FromArgb(170, 130, 120, 215);     // soft violet outline
    private static readonly Color Face = Color.FromArgb(235, 28, 24, 66);       // dark eyes and mouth
    private static readonly Color Halo = Color.FromArgb(170, 195, 255);

    private readonly HalloweenScenery _s;
    private readonly Sprite[,,] _ghost = new Sprite[2, 2, Poses];               // [size: big/small, direction: right/left, pose]
    private readonly Sprite[] _glow = new Sprite[2];                            // [size]
    private readonly float[] _height = new float[2];                            // ghost height in pixels, per size

    private bool _leftward;
    private float _baseY, _waveAmp, _waveLength, _start, _speed, _gap, _phase3;
    private readonly float[] _bobPhase = new float[3];

    public override float Seconds => 14f;

    public GhostParade(HalloweenScenery s)
    {
        _s = s;
        _height[0] = Math.Max(14f, s.U * 0.056f);         // big ghosts: about 0.05 U tall (never under 14 px)
        _height[1] = Math.Max(11f, s.U * 0.042f);         // the little straggler
        for (int size = 0; size < 2; size++)
        {
            _glow[size] = Sprite.Glow((int)(_height[size] * 0.95f), Halo);
            for (int dir = 0; dir < 2; dir++)
                for (int pose = 0; pose < Poses; pose++)
                    _ghost[size, dir, pose] = PaintGhost(_height[size], dir == 1, pose);
        }
    }

    /// <summary>
    /// Paints one ghost. "h" is its height in pixels; everything below is a
    /// fraction of h, so one recipe makes any size. Origin (0,0) is the middle
    /// of the ghost; y runs from -0.5 (top of the head) to +0.5 (the hem).
    /// </summary>
    private static Sprite PaintGhost(float h, bool mirrored, int pose)
    {
        int w = (int)(h * 1.1f) + 6, hh = (int)(h * 1.25f) + 6;
        float phase = pose * MathF.Tau / Poses;            // how far along the hem's wave: pose 16 is pose 0 again, so it loops
        return Sprite.Paint(w, hh, g =>
        {
            g.TranslateTransform(w / 2f, hh / 2f);
            if (mirrored) g.ScaleTransform(-1, 1);
            g.RotateTransform(8);                          // lean into the direction of travel (drawn facing right)

            // The body outline: a dome for the head, straight-ish sides that
            // flare slightly, and a hem that is a sine wave (3 scallops).
            const float half = 0.30f, hemHalf = 0.37f;
            using var body = new GraphicsPath();
            body.AddArc(-half * h, -0.5f * h, half * 2 * h, half * 2 * h, 180, 180);   // the top half of a circle
            var hem = new List<PointF>();
            const int steps = 28;
            for (int i = 0; i <= steps; i++)
            {
                float f = i / (float)steps;                // 0 at the right end, 1 at the left end
                float x = hemHalf - 2 * hemHalf * f;
                float y = 0.42f + 0.055f * MathF.Sin(f * MathF.Tau * 3f + phase);
                hem.Add(new PointF(x * h, y * h));
            }
            body.AddLines([new PointF(half * h, -0.2f * h), .. hem, new PointF(-half * h, -0.2f * h)]);
            body.CloseFigure();

            using (var fill = new SolidBrush(Sheet)) g.FillPath(fill, body);
            using (var pen = new Pen(Rim, Math.Max(1f, h * 0.04f)) { LineJoin = LineJoin.Round }) g.DrawPath(pen, body);

            // Face: two tall dark eyes and a small round mouth, shifted a
            // touch toward the direction of travel so the ghost looks where it is going.
            using var dark = new SolidBrush(Face);
            float ex = 0.10f, ey = -0.20f, lookX = 0.03f;
            foreach (int side in (int[])[-1, 1])
                g.FillEllipse(dark, (side * ex + lookX - 0.04f) * h, (ey - 0.055f) * h, 0.08f * h, 0.11f * h);
            g.FillEllipse(dark, (lookX + 0.02f - 0.04f) * h, (-0.03f) * h, 0.08f * h, 0.10f * h);
        });
    }

    public override void Begin(Random rng)
    {
        _leftward = rng.Next(2) == 0;
        float u = _s.U;
        _baseY = _s.Height * (0.14f + 0.28f * (float)rng.NextDouble());
        _waveAmp = u * (0.035f + 0.025f * (float)rng.NextDouble());
        _waveLength = u * (0.7f + 0.4f * (float)rng.NextDouble());
        _gap = u * 0.11f;                                  // spacing between ghosts along the path
        _phase3 = (float)(rng.NextDouble() * Math.PI * 2);
        for (int i = 0; i < 3; i++) _bobPhase[i] = (float)(rng.NextDouble() * Math.PI * 2);

        // The leader starts fully off screen and travels until the straggler
        // (up to 3.4 gaps behind) is fully off the far side too.
        float margin = _height[0] * 1.2f;
        _start = -margin;
        float travel = _s.Width + 2 * margin + 3.4f * _gap;
        _speed = travel / Seconds;
    }

    // Where a ghost is along the screen, left to right, "behind" pixels behind the leader.
    private float AcrossAt(float t, float behind) => _start + _speed * t - behind;

    public override void Draw(FrameBuffer fb, float t)
    {
        float u = _s.U;
        float fade = Fade(t, Seconds, 0.4f, 0.4f);        // belt and braces: they are off screen at both ends anyway
        int dir = _leftward ? 1 : 0;

        // The straggler: a slow wobble in how far behind it is (1.6 to 3.4 gaps).
        float lag3 = _gap * (2.5f + 0.9f * MathF.Sin(t * 0.8f + _phase3));

        // Drawn back to front: the little one first, so the bigger ones overlap it if they ever meet.
        for (int i = 2; i >= 0; i--)
        {
            int size = i == 2 ? 1 : 0;
            float across = AcrossAt(t, i == 2 ? lag3 : i * _gap);
            // The shared wavy ribbon: height depends only on across-position.
            float y = _baseY + _waveAmp * MathF.Sin(across / _waveLength * MathF.Tau);
            // Each ghost's own gentle bob on top, never under a pixel and a half.
            y += Math.Max(1.5f, u * 0.008f) * MathF.Sin(t * 2.2f + _bobPhase[i]);

            float x = _leftward ? _s.Width - across : across;

            // A soft glow of its own light, then the see-through sheet over it.
            _glow[size].DrawCentered(fb, x, y, 0.32f * fade, _s.OpenSky);
            // The hem ripples: pose steps at about 12 per second, each ghost on its own offset.
            int pose = ((int)MathF.Floor(t * 12f + i * 5f) % Poses + Poses) % Poses;
            _ghost[size, dir, pose].DrawCentered(fb, x, y, 0.6f * fade, _s.OpenSky);
        }
    }
}

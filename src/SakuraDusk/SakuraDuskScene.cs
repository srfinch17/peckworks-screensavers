using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace SakuraDusk;

/// <summary>
/// Cherry blossom petals drifting down over a pond at sunset.
///
/// The same theater as Sakura: a BACKDROP painted once (DuskPainter.cs) and
/// ACTORS drawn over it every frame (the shared PetalField in the engine,
/// Core/Sakura/PetalField.cs, which explains how a petal falls, sways, spins
/// and tumbles). The one twist here is the light: the petals are tinted warm
/// so they glow in the sunset, and the glints on the water are golden and
/// live only in the sun's reflection.
/// </summary>
internal sealed class SakuraDuskScene : IScreensaverScene
{
    /// <summary>A glint of light on the water that brightens and dims.</summary>
    private struct Glint
    {
        public int X, Y, Length;
        public float Phase, Speed;
    }

    private readonly Random _rng = new();
    private readonly DuskScenery _scenery;
    private readonly PetalField _petals;
    private readonly Glint[] _glints;
    private float _time;

    public SakuraDuskScene(int width, int height, SakuraDuskSettings settings)
    {
        _scenery = DuskPainter.Paint(width, height, _rng);

        _petals = new PetalField(width, height,
            PetalField.CountFor(width, height, settings.DensityPercent),
            settings.FallSpeedPercent / 100f, settings.WindPercent / 100f, settings.PetalSizePercent / 100f,
            _scenery.BlossomSpots, _rng)
        {
            Tint = (1f, 0.90f, 0.84f),   // evening light: a little less green and blue, so the petals warm up
        };

        // ---- Glints in the sun's reflection ----
        // The painter tells us the patch of water the sun lights up. Glints
        // are scattered only there, denser and shorter near the far edge (far
        // away) and longer near the bottom (close to us).
        RectangleF sun = _scenery.SunPath;
        _glints = new Glint[90];
        for (int i = 0; i < _glints.Length; i++)
        {
            float depth = (float)Math.Pow(_rng.NextDouble(), 0.8);   // 0 = far edge of the patch, 1 = near edge
            float spread = 0.35f + 0.65f * depth;                     // the patch widens toward us
            float across = ((float)_rng.NextDouble() + (float)_rng.NextDouble() - 1) * spread;   // bunched near the middle
            _glints[i] = new Glint
            {
                X = (int)(sun.X + sun.Width * (0.5f + 0.5f * across)),
                Y = (int)(sun.Y + depth * sun.Height),
                Length = Math.Max(2, (int)(height * (0.004f + 0.02f * (float)_rng.NextDouble()) * (0.3f + depth))),
                Phase = (float)(_rng.NextDouble() * Math.PI * 2),
                Speed = 0.8f + 2.2f * (float)_rng.NextDouble(),
            };
        }
    }

    public void Update(double elapsedSeconds)
    {
        float dt = (float)elapsedSeconds;
        _time += dt;
        _petals.Update(dt);
    }

    public void Render(FrameBuffer fb)
    {
        // 1. The backdrop. Array.Copy is one fast block copy of every pixel.
        Array.Copy(_scenery.Pixels, fb.Pixels, fb.Pixels.Length);

        // 2. Golden glints twinkling in the sun's reflection.
        foreach (ref readonly Glint gl in _glints.AsSpan())
        {
            float s = MathF.Sin(_time * gl.Speed + gl.Phase);
            if (s <= 0.2f) continue;                       // dark most of the time, flashing briefly
            float alpha = (s - 0.2f) / 0.8f * 0.55f;
            DrawGlint(fb, gl, alpha);
        }

        // 3. The petals, far to near.
        _petals.Draw(fb);
    }

    /// <summary>A short horizontal streak of warm light, brightest in the middle.</summary>
    private static void DrawGlint(FrameBuffer fb, in Glint gl, float alpha)
    {
        if (gl.Y < 0 || gl.Y >= fb.Height) return;
        int row = gl.Y * fb.Width;
        for (int i = 0; i < gl.Length; i++)
        {
            int x = gl.X + i;
            if (x < 0 || x >= fb.Width) continue;
            float t = 1 - MathF.Abs(i / (float)gl.Length * 2 - 1);
            fb.Pixels[row + x] = FrameBuffer.Blend(fb.Pixels[row + x], 255, 240, 200, alpha * t);
        }
    }

    public void Dispose() { }
}

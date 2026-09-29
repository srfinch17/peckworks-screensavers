using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace Sakura;

/// <summary>
/// Cherry blossom petals drifting down over Mount Fuji, like snow.
///
/// Two layers, like a theater:
///
///   1. The BACKDROP (SceneryPainter.cs): painted once at startup, never moves.
///   2. The ACTORS: a few hundred petals, run by the shared PetalField in the
///      engine (Core/Sakura/PetalField.cs, which explains how a petal falls,
///      sways, spins and tumbles). Each frame we copy the backdrop, twinkle a
///      few glints on the lake, then draw every petal where it is right now.
/// </summary>
internal sealed class SakuraScene : IScreensaverScene
{
    /// <summary>A glint of light on the lake that brightens and dims.</summary>
    private struct Glint
    {
        public int X, Y, Length;
        public float Phase, Speed;
    }

    private readonly Random _rng = new();
    private readonly Scenery _scenery;
    private readonly PetalField _petals;
    private readonly Glint[] _glints;
    private float _time;

    public SakuraScene(int width, int height, SakuraSettings settings)
    {
        _scenery = SceneryPainter.Paint(width, height, _rng);

        _petals = new PetalField(width, height,
            PetalField.CountFor(width, height, settings.DensityPercent),
            settings.FallSpeedPercent / 100f, settings.WindPercent / 100f, settings.PetalSizePercent / 100f,
            _scenery.BlossomSpots, _rng);

        // ---- Glints on the lake ----
        _glints = new Glint[120];
        float lakeTop = _scenery.HorizonY;
        for (int i = 0; i < _glints.Length; i++)
        {
            float depth = (float)Math.Pow(_rng.NextDouble(), 0.7);   // 0 = far shore, 1 = bottom of screen
            _glints[i] = new Glint
            {
                X = _rng.Next(width),
                Y = (int)(lakeTop + height * 0.01f + depth * (height - lakeTop - height * 0.01f)),
                Length = Math.Max(2, (int)(height * (0.006f + 0.03f * (float)_rng.NextDouble()) * (0.3f + depth))),
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

        // 2. Glints twinkling on the lake.
        foreach (ref readonly Glint gl in _glints.AsSpan())
        {
            float s = MathF.Sin(_time * gl.Speed + gl.Phase);
            if (s <= 0.2f) continue;                       // dark most of the time, flashing briefly
            float alpha = (s - 0.2f) / 0.8f * 0.45f;
            DrawGlint(fb, gl, alpha);
        }

        // 3. The petals, far to near.
        _petals.Draw(fb);
    }

    /// <summary>A short horizontal streak of light. Only lands on water (bluish pixels), never on the trees.</summary>
    private static void DrawGlint(FrameBuffer fb, in Glint gl, float alpha)
    {
        if (gl.Y < 0 || gl.Y >= fb.Height) return;
        int row = gl.Y * fb.Width;
        for (int i = 0; i < gl.Length; i++)
        {
            int x = gl.X + i;
            if (x < 0 || x >= fb.Width) continue;
            uint bg = fb.Pixels[row + x];
            int r = (int)((bg >> 16) & 0xFF), b = (int)(bg & 0xFF);
            if (b < r + 20) continue;                      // not water
            // Brightest in the middle of the streak, fading at both ends.
            float t = 1 - MathF.Abs(i / (float)gl.Length * 2 - 1);
            fb.Pixels[row + x] = FrameBuffer.Blend(bg, 240, 248, 255, alpha * t);
        }
    }

    public void Dispose() { }
}

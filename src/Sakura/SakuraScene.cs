using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;
using Sakura.Happenings;

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
///   3. The HAPPENINGS: twenty small surprises (a cap cloud on Fuji, a
///      kingfisher, a shiba inu) that each come on for a few seconds now and
///      then. Each is one class in the Happenings folder; the engine's
///      director (Core/Happenings.cs) decides which goes on when.
/// </summary>
internal sealed class SakuraScene : IScreensaverScene
{
    /// <summary>A glint of light on the lake that brightens and dims.</summary>
    private struct Glint
    {
        public int X, Y, Length;
        public float Phase, Speed;
    }

    private readonly Random _rng = HappeningDirector.SceneRandom();   // new dice every launch, unless PECKWORKS_SEED asks for a repeat
    private readonly Scenery _scenery;
    private readonly PetalField _petals;
    private readonly Glint[] _glints;
    private readonly Boat _boat;
    private readonly HappeningDirector _happenings;
    // A "double" (about 15 digits of precision), not a "float" (about 7). A
    // screensaver can run for days, and a float clock that large can no
    // longer register a 16 millisecond step: the glints would freeze.
    private double _time;

    public SakuraScene(int width, int height, SakuraSettings settings)
    {
        _scenery = SceneryPainter.Paint(width, height, _rng);
        float u = Math.Min(height, width * 9f / 16f);   // the size unit: height, or less on a tall screen

        _petals = new PetalField(width, height,
            PetalField.CountFor(width, height, settings.DensityPercent),
            settings.FallSpeedPercent / 100f, settings.WindPercent / 100f, settings.PetalSizePercent / 100f,
            _scenery.BlossomSpots, _rng, sizeUnit: u);

        // ---- The boat crossing the lake (Core/Sakura/Boat.cs), in plain daylight colors ----
        _boat = new Boat(width, _scenery.BoatWaterline, u, shade: 0f, shadow: Color.Black, _rng);

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

        // ---- The happenings: hand the director the cast list and how often to deal ----
        _happenings = new HappeningDirector(SakuraHappenings.Cast(_scenery), _rng, settings.SurprisePercent / 100f);
    }

    public void Update(double elapsedSeconds)
    {
        float dt = (float)elapsedSeconds;
        _time += dt;
        _petals.Update(dt);
        _boat.Update(dt);
        _happenings.Update(elapsedSeconds);
    }

    public void Render(FrameBuffer fb)
    {
        // 1. The backdrop. Array.Copy is one fast block copy of every pixel.
        Array.Copy(_scenery.Pixels, fb.Pixels, fb.Pixels.Length);

        // 1b. The FAR happenings (layer 0): the sky, Fuji, the far shore. Before
        //     the boat, so a shinkansen on the shore is behind it.
        _happenings.Draw(fb, 0);

        // 2. The boat, only where the lake is open, so the banks and trees hide it.
        _boat.Draw(fb, _scenery.OpenWater);

        // 3. Glints twinkling on the lake.
        foreach (ref readonly Glint gl in _glints.AsSpan())
        {
            float s = (float)Math.Sin(_time * gl.Speed + gl.Phase);   // Math.Sin of the double clock (see _time)
            if (s <= 0.2f) continue;                       // dark most of the time, flashing briefly
            float alpha = (s - 0.2f) / 0.8f * 0.45f;
            DrawGlint(fb, gl, alpha);
        }

        // 3b. The NEAR happenings (layer 1): on the lake in front of the boat,
        //     on the banks, in the trees and branches.
        _happenings.Draw(fb, 1);

        // 4. The petals, far to near.
        _petals.Draw(fb);

        // 5. Happenings NEARER than the petals (layer 2): a petal blizzard
        //    blowing right past the viewer.
        _happenings.Draw(fb, 2);
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

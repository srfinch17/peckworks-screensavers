using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;
using SakuraDusk.Happenings;

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
///
/// And the HAPPENINGS: twenty small surprises (floating lanterns, a frog
/// leaping into the pond, the first star) that each come on for a few
/// seconds now and then. Each is one class in the Happenings folder; the
/// engine's director (Core/Happenings.cs) decides which goes on when.
/// </summary>
internal sealed class SakuraDuskScene : IScreensaverScene
{
    /// <summary>A glint of light on the water that brightens and dims.</summary>
    private struct Glint
    {
        public int X, Y, Length;
        public float Phase, Speed;
    }

    private readonly Random _rng = HappeningDirector.SceneRandom();   // new dice every launch, unless PECKWORKS_SEED asks for a repeat
    private readonly DuskScenery _scenery;
    private readonly PetalField _petals;
    private readonly Glint[] _glints;
    private readonly Boat _boat;
    private readonly Flock _birds;
    private readonly HappeningDirector _happenings;
    // A "double" (about 15 digits of precision), not a "float" (about 7). A
    // screensaver can run for days, and a float clock that large can no
    // longer register a 16 millisecond step: the glints would freeze.
    private double _time;

    public SakuraDuskScene(int width, int height, SakuraDuskSettings settings)
    {
        _scenery = DuskPainter.Paint(width, height, _rng);
        float u = Math.Min(height, width * 9f / 16f);   // the size unit: height, or less on a tall screen

        _petals = new PetalField(width, height,
            PetalField.CountFor(width, height, settings.DensityPercent),
            settings.FallSpeedPercent / 100f, settings.WindPercent / 100f, settings.PetalSizePercent / 100f,
            _scenery.BlossomSpots, _rng, sizeUnit: u)
        {
            Tint = (1f, 0.90f, 0.84f),   // evening light: a little less green and blue, so the petals warm up
        };

        // ---- The flock of birds crossing the sky (Flock.cs) ----
        _birds = new Flock(width, height, u, _rng);

        // ---- The boat crossing the pond (Core/Sakura/Boat.cs) ----
        // Shaded most of the way toward the hills' plum shadow, because the
        // sun is behind it: against a sunset, a boat is nearly a silhouette.
        // It is also smaller than Sakura's boat, because it sails far out
        // near the opposite shore: closer in, it would line up with the
        // bridge's railing and look as if it were riding on the bridge.
        _boat = new Boat(width, _scenery.BoatWaterline, u, shade: 0.62f, shadow: Color.FromArgb(50, 26, 60), _rng, scale: 0.6f);

        // ---- Glints in the sun's reflection ----
        // The painter tells us the strip of water the sun lights up. Glints
        // are scattered there, bunched under the sun, shorter near the far
        // edge (far away) and longer near the bottom (close to us). The bridge
        // crosses the strip, so DrawGlint checks every pixel and only lights
        // bright water.
        RectangleF sun = _scenery.SunPath;
        _glints = new Glint[160];
        for (int i = 0; i < _glints.Length; i++)
        {
            float depth = (float)Math.Pow(_rng.NextDouble(), 0.8);   // 0 = far edge of the patch, 1 = near edge
            float spread = 0.35f + 0.65f * depth;                     // the patch widens toward us
            float across = ((float)_rng.NextDouble() + (float)_rng.NextDouble() - 1) * spread;   // bunched near the middle
            _glints[i] = new Glint
            {
                X = (int)(sun.X + sun.Width * (0.5f + 0.5f * across)),
                Y = (int)(sun.Y + depth * sun.Height),
                Length = Math.Max(2, (int)(u * (0.004f + 0.02f * (float)_rng.NextDouble()) * (0.3f + depth))),
                Phase = (float)(_rng.NextDouble() * Math.PI * 2),
                Speed = 0.8f + 2.2f * (float)_rng.NextDouble(),
            };
        }

        // ---- The happenings: hand the director the cast list and how often to deal ----
        _happenings = new HappeningDirector(DuskHappenings.Cast(_scenery), _rng, settings.SurprisePercent / 100f);
    }

    public void Update(double elapsedSeconds)
    {
        float dt = (float)elapsedSeconds;
        _time += dt;
        _petals.Update(dt);
        _boat.Update(dt);
        _birds.Update(dt);
        _happenings.Update(elapsedSeconds);
    }

    public void Render(FrameBuffer fb)
    {
        // 1. The backdrop. Array.Copy is one fast block copy of every pixel.
        Array.Copy(_scenery.Pixels, fb.Pixels, fb.Pixels.Length);

        // 1b. The FAR happenings (layer 0): the sky, the sun, the hills, the far
        //     water. Before the birds and the boat, which pass in front.
        _happenings.Draw(fb, 0);

        // 2. The birds, only where the sky is open, so the hills and branches hide them.
        _birds.Draw(fb, _scenery.OpenSky);

        // 3. The boat, only where the pond is open, so the banks and the bridge hide it.
        _boat.Draw(fb, _scenery.OpenWater);

        // 4. Golden glints twinkling in the sun's reflection.
        foreach (ref readonly Glint gl in _glints.AsSpan())
        {
            float s = (float)Math.Sin(_time * gl.Speed + gl.Phase);   // Math.Sin of the double clock (see _time)
            if (s <= 0.2f) continue;                       // dark most of the time, flashing briefly
            float alpha = (s - 0.2f) / 0.8f * 0.55f;
            DrawGlint(fb, gl, alpha);
        }

        // 4b. The NEAR happenings (layer 1): on the water in front of the
        //     boat, on the bridge and banks, in the trees and branches.
        _happenings.Draw(fb, 1);

        // 5. The petals, far to near.
        _petals.Draw(fb);

        // 6. Happenings NEARER than the petals (layer 2), if any.
        _happenings.Draw(fb, 2);
    }

    /// <summary>
    /// A short horizontal streak of warm light, brightest in the middle. Only
    /// lands on bright, warm water pixels: the bridge's wood, the banks, and
    /// the deep water near the bottom are all too dark to pass the test, so a
    /// glint can never sit on the railing.
    /// </summary>
    private static void DrawGlint(FrameBuffer fb, in Glint gl, float alpha)
    {
        if (gl.Y < 0 || gl.Y >= fb.Height) return;
        int row = gl.Y * fb.Width;
        for (int i = 0; i < gl.Length; i++)
        {
            int x = gl.X + i;
            if (x < 0 || x >= fb.Width) continue;
            uint bg = fb.Pixels[row + x];
            int r = (int)((bg >> 16) & 0xFF), g = (int)((bg >> 8) & 0xFF);
            if (r < 150 || g < 95) continue;               // not sunlit water
            float t = 1 - MathF.Abs(i / (float)gl.Length * 2 - 1);
            fb.Pixels[row + x] = FrameBuffer.Blend(fb.Pixels[row + x], 255, 240, 200, alpha * t);
        }
    }

    public void Dispose() { }
}

using Peckworks.Screensavers.Core;

namespace CabinByStream;

/// <summary>
/// A flowery meadow at dusk: a small thatched cottage with white smoke
/// rising from its chimney, a stream winding past with trout in it,
/// squirrels running around the trees, and fireflies over the grass.
///
/// The same theater as the other painted savers: a BACKDROP painted once
/// (MeadowPainter.cs) and ACTORS drawn over it every frame. The actors here:
///
///   - The SMOKE (ChimneySmoke.cs): puffs born at the chimney that rise,
///     spread, lean with the breeze and thin away.
///   - The STREAM'S SURFACE: glints of sky light that slide downstream along
///     the water, faster where the water is nearer. The water itself is in
///     the backdrop; only the light moves.
///   - The FISH (FishSchool.cs): trout holding against the current, drifting
///     back and darting forward.
///   - The SQUIRRELS (Squirrels.cs): running between the trees and up them.
///   - The FIREFLIES (Fireflies.cs): blinking on their own slow cycles.
///   - Small lights: the windows and the door lamp flicker a little, the
///     brighter stars twinkle.
/// </summary>
internal sealed class CabinByStreamScene : IScreensaverScene
{
    /// <summary>One glint sliding down the stream: how far along, how far off the middle, and its own flicker.</summary>
    private struct Glint
    {
        public float P, Lateral, Length, Phase, Speed;
    }

    private struct Star
    {
        public float X, Y, Phase, Speed;
    }

    private readonly Random _rng = HappeningDirector.SceneRandom();   // new dice every launch, unless PECKWORKS_SEED asks for a repeat
    private readonly MeadowScenery _scenery;
    private readonly ChimneySmoke _smoke;
    private readonly Fireflies _fireflies;
    private readonly FishSchool _fish;
    private readonly Squirrels _squirrels;
    private readonly Glint[] _glints;
    private readonly Star[] _stars;
    private readonly Sprite _windowGlow, _lampGlow, _starGlow;
    private readonly float _u;
    // A "double" (about 15 digits of precision), not a "float" (about 7). A
    // screensaver can run for days, and a float clock that large can no
    // longer register a 16 millisecond step: everything would freeze.
    private double _time;

    public CabinByStreamScene(int width, int height, CabinByStreamSettings settings)
    {
        _scenery = MeadowPainter.Paint(width, height, _rng);
        _u = _scenery.U;
        float breeze = settings.BreezePercent / 100f;

        _smoke = new ChimneySmoke(_scenery.ChimneyTop, _u, settings.SmokePercent / 100f, breeze, _rng);
        _fireflies = new Fireflies(_scenery, settings.FireflyPercent / 100f, breeze, _rng);
        _fish = new FishSchool(_scenery, settings.FishCount, _rng);
        _squirrels = new Squirrels(_scenery, settings.SquirrelCount, _rng);

        // The lights. The windows are already painted bright; the glow adds
        // the warmth on the wall and the gentle unsteadiness of firelight.
        _windowGlow = Sprite.Glow(Math.Max(4, (int)(_scenery.WindowSize * 1.6f)), Color.FromArgb(255, 180, 100));
        _lampGlow = Sprite.Glow(Math.Max(3, (int)(_u * 0.022f)), Color.FromArgb(255, 200, 120));
        _starGlow = Sprite.Glow(Math.Max(2, (int)(_u * 0.0028f)), Color.FromArgb(255, 246, 228));
        _stars = _scenery.Stars.Select(p => new Star
        {
            X = p.X, Y = p.Y,
            Phase = (float)(_rng.NextDouble() * Math.PI * 2),
            Speed = 0.6f + 1.6f * (float)_rng.NextDouble(),
        }).ToArray();

        // Glints on the stream. Each has a place along the water (P, 0 far
        // to 1 near) and across it; Update slides them downstream.
        _glints = new Glint[200];
        for (int i = 0; i < _glints.Length; i++)
            _glints[i] = new Glint
            {
                P = (float)_rng.NextDouble(),
                Lateral = ((float)_rng.NextDouble() * 2 - 1) * 0.85f,
                Length = 0.15f + 0.45f * (float)_rng.NextDouble(),
                Phase = (float)(_rng.NextDouble() * Math.PI * 2),
                Speed = 1.5f + 3f * (float)_rng.NextDouble(),
            };
    }

    public void Update(double elapsedSeconds)
    {
        float dt = (float)elapsedSeconds;
        _time += dt;
        _smoke.Update(dt);
        _fish.Update(dt);
        _squirrels.Update(dt);
        // The current. A glint's speed down the screen grows as it comes
        // nearer (perspective: the same real speed covers more pixels close
        // up), so the rate of P grows with P. When it slides off the bottom
        // it is born again at the far end.
        for (int i = 0; i < _glints.Length; i++)
        {
            ref Glint gl = ref _glints[i];
            gl.P += (0.012f + 0.06f * gl.P) * dt;
            if (gl.P > 1f) { gl.P -= 1f; gl.Lateral = ((float)_rng.NextDouble() * 2 - 1) * 0.85f; }
        }
    }

    public void Render(FrameBuffer fb)
    {
        // 1. The backdrop. Array.Copy is one fast block copy of every pixel.
        Array.Copy(_scenery.Pixels, fb.Pixels, fb.Pixels.Length);

        // 2. The stars twinkle: a tiny glow over each bright one, brighter and dimmer on its own rhythm.
        foreach (ref readonly Star st in _stars.AsSpan())
        {
            float b = 0.15f + 0.55f * (0.5f + 0.5f * (float)Math.Sin(_time * st.Speed + st.Phase));   // Math.Sin of the double clock
            _starGlow.DrawCentered(fb, st.X, st.Y, b);
        }

        // 3. The fish, under the surface: before the glints, which are the surface.
        _fish.Draw(fb);

        // 4. Light sliding down the stream.
        StreamShape stream = _scenery.Stream;
        foreach (ref readonly Glint gl in _glints.AsSpan())
        {
            float s = (float)Math.Sin(_time * gl.Speed + gl.Phase);
            if (s <= 0.1f) continue;                                  // dark most of the time, catching the light briefly
            float y = stream.Top + (stream.Bottom - stream.Top) * gl.P;
            float hw = stream.HalfWidth(y);
            float x = stream.CenterX(y) + gl.Lateral * hw;
            int len = Math.Max(2, (int)(hw * gl.Length));
            DrawGlint(fb, (int)x, (int)y, len, (s - 0.1f) / 0.9f * 0.45f * (0.4f + 0.6f * gl.P));
        }

        // 5. Firelight in the windows and the lamp by the door: a slow, slightly
        //    irregular flicker (two sines of unrelated speeds added together).
        float flicker = 0.30f + 0.06f * (float)(Math.Sin(_time * 7.3) + Math.Sin(_time * 11.9)) / 2;
        foreach (PointF win in _scenery.Windows)
            _windowGlow.DrawCentered(fb, win.X, win.Y, flicker);
        _lampGlow.DrawCentered(fb, _scenery.DoorLamp.X, _scenery.DoorLamp.Y, 0.8f + 0.15f * (float)Math.Sin(_time * 9.1));

        // 6. The squirrels, on the grass and the trunks.
        _squirrels.Draw(fb);

        // 7. The smoke, above the roof, in front of the sky and the far trees.
        _smoke.Draw(fb);

        // 8. The fireflies, nearest of all.
        _fireflies.Draw(fb, _time);
    }

    /// <summary>
    /// A short horizontal streak of pale light on the water, brightest in
    /// the middle, only on open water (the stencil), so it never lands on a
    /// stone or the bank.
    /// </summary>
    private void DrawGlint(FrameBuffer fb, int x0, int y, int len, float alpha)
    {
        if (y < 0 || y >= fb.Height) return;
        int row = y * fb.Width;
        for (int i = 0; i < len; i++)
        {
            int x = x0 - len / 2 + i;
            if (x < 0 || x >= fb.Width || !_scenery.OpenWater[row + x]) continue;
            float t = 1 - MathF.Abs(i / (float)len * 2 - 1);
            fb.Pixels[row + x] = FrameBuffer.Blend(fb.Pixels[row + x], 238, 230, 240, alpha * t);
        }
    }

    public void Dispose() { }
}

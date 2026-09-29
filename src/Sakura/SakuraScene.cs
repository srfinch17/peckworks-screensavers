using Peckworks.Screensavers.Core;

namespace Sakura;

/// <summary>
/// Cherry blossom petals drifting down over Mount Fuji, like snow.
///
/// ======================================================================
///  THE BIG IDEA
/// ======================================================================
///
/// Two layers, like a theater:
///
///   1. The BACKDROP (SceneryPainter.cs): painted once at startup, never moves.
///   2. The ACTORS: a few hundred petals. Each frame we copy the backdrop, then
///      draw every petal where it is right now.
///
/// What makes a falling petal look like a petal and not a pink dot:
///
///   - It FALLS slowly, and it is pushed sideways by a breeze that rises and dies
///     away over time (so the whole flurry leans one way, then settles).
///   - It SWAYS: a gentle side-to-side swing, like a leaf rocking as it falls.
///   - It SPINS in the plane of the screen.
///   - It TUMBLES: it flips over and over, so you see it face-on, then edge-on,
///     then face-on again. We fake that 3D tumble in 2D by squashing the
///     petal's width: width times cos(flip angle). cos swings smoothly from 1
///     to 0 and back, so the petal looks like it is turning over. Edge-on petals
///     also fall a little faster, because they cut through the air.
///   - DEPTH: each petal has a depth Z (0 = far, 1 = near). Near petals are
///     bigger, faster, and more opaque. Far ones are small, slow, and slightly
///     see-through, so they sit back in the haze. That difference alone is what
///     makes the scene feel 3D. (It is called "parallax": close things move
///     across your view faster than far things, like fence posts versus hills
///     from a car window.)
///
/// Some petals are born at the blossom clusters on the branches, fading in, so
/// they look like they are letting go of the tree. The rest drift in from above
/// the top of the screen.
/// </summary>
internal sealed class SakuraScene : IScreensaverScene
{
    /// <summary>Everything about one falling petal.</summary>
    private struct Petal
    {
        public float X, Y;           // center position, in pixels
        public float Z;              // depth: 0 = far away, 1 = close to the viewer
        public float Size;           // length, in pixels
        public float Angle, Spin;    // turn in the screen plane (radians), and how fast it turns
        public float Flip, FlipSpeed;// tumble angle (radians), and how fast it tumbles
        public float Sway, SwayFreq, SwayAmp; // side-to-side swing: where in the swing, how fast, how far
        public float Pink;           // 0 = white petal, 1 = pink petal
        public float Fade;           // 0..1, fades in when born on a branch
    }

    /// <summary>A glint of light on the lake that brightens and dims.</summary>
    private struct Glint
    {
        public int X, Y, Length;
        public float Phase, Speed;
    }

    private readonly Random _rng = new();
    private readonly int _w, _h;
    private readonly Scenery _scenery;
    private readonly Petal[] _petals;
    private readonly Glint[] _glints;
    private readonly float _speedScale, _windScale, _sizeScale;
    private float _time;

    public SakuraScene(int width, int height, SakuraSettings settings)
    {
        _w = width;
        _h = height;
        _speedScale = settings.FallSpeedPercent / 100f;
        _windScale = settings.WindPercent / 100f;
        _sizeScale = settings.PetalSizePercent / 100f;

        _scenery = SceneryPainter.Paint(width, height, _rng);

        // ---- Petals ----
        // About 220 at 100% on a 16:9 screen; more on wider screens.
        float aspect = width / (float)Math.Max(1, height);
        int count = Math.Max(10, (int)(220 * settings.DensityPercent / 100f * aspect / (16f / 9f)));
        _petals = new Petal[count];
        for (int i = 0; i < count; i++)
        {
            // Spread depths evenly so there are always some near and some far.
            _petals[i].Z = (i + (float)_rng.NextDouble()) / count;
            Respawn(ref _petals[i], scatterOnScreen: true);
        }
        // Draw far petals first and near ones last, so near petals cover far
        // ones (the "painter's algorithm": paint the back first). Depth never
        // changes for a slot, so sorting once is enough.
        Array.Sort(_petals, (a, b) => a.Z.CompareTo(b.Z));

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

    /// <summary>Give a petal a fresh random personality and a starting spot.</summary>
    private void Respawn(ref Petal p, bool scatterOnScreen)
    {
        p.Size = Math.Max(3f, _h * 0.013f * (0.45f + 1.2f * p.Z) * _sizeScale);
        p.Angle = (float)(_rng.NextDouble() * Math.PI * 2);
        p.Spin = ((float)_rng.NextDouble() - 0.5f) * 1.6f;
        p.Flip = (float)(_rng.NextDouble() * Math.PI * 2);
        p.FlipSpeed = 0.8f + 2.2f * (float)_rng.NextDouble();
        p.Sway = (float)(_rng.NextDouble() * Math.PI * 2);
        p.SwayFreq = 0.5f + 1.1f * (float)_rng.NextDouble();
        p.SwayAmp = _h * (0.01f + 0.025f * (float)_rng.NextDouble()) * (0.5f + p.Z);
        p.Pink = (float)_rng.NextDouble();

        if (scatterOnScreen)
        {
            // At startup, scatter petals across the whole screen so it is
            // already snowing blossoms when the screensaver appears.
            p.X = (float)_rng.NextDouble() * _w;
            p.Y = (float)_rng.NextDouble() * _h;
            p.Fade = 1;
        }
        else if (_scenery.BlossomSpots.Count > 0 && _rng.NextDouble() < 0.35)
        {
            // Let go of a blossom cluster on a branch, fading in.
            PointF spot = _scenery.BlossomSpots[_rng.Next(_scenery.BlossomSpots.Count)];
            p.X = spot.X + _h * 0.01f * ((float)_rng.NextDouble() - 0.5f);
            p.Y = spot.Y;
            p.Fade = 0;
        }
        else
        {
            // Drift in from just above the top edge.
            p.X = (float)_rng.NextDouble() * _w;
            p.Y = -p.Size - (float)_rng.NextDouble() * _h * 0.15f;
            p.Fade = 1;
        }
    }

    // ==================================================================
    //  UPDATE
    // ==================================================================

    public void Update(double elapsedSeconds)
    {
        float dt = (float)elapsedSeconds;
        _time += dt;

        // The breeze: two slow waves added together, so gusts rise and fall on
        // an irregular rhythm instead of a steady beat. Mostly blowing right,
        // occasionally stilling almost to nothing.
        float wind = _h * 0.03f * _windScale *
                     (0.55f + 0.5f * MathF.Sin(_time * 0.11f) + 0.3f * MathF.Sin(_time * 0.043f + 2f));

        float margin = _h * 0.1f;
        for (int i = 0; i < _petals.Length; i++)
        {
            ref Petal p = ref _petals[i];

            p.Sway += p.SwayFreq * dt;
            p.Flip += p.FlipSpeed * dt;
            p.Angle += p.Spin * dt;
            if (p.Fade < 1) p.Fade = MathF.Min(1, p.Fade + dt * 1.2f);

            // Falling: nearer petals fall faster (parallax), and petals falling
            // edge-on (|cos(flip)| near 0) drop a bit quicker than face-on ones.
            float faceOn = MathF.Abs(MathF.Cos(p.Flip));
            float fall = _h * 0.045f * (0.45f + 0.75f * p.Z) * _speedScale * (0.8f + 0.45f * (1 - faceOn));

            // Sideways: the shared breeze (felt more by near petals), plus this
            // petal's own swing. The swing's speed is the rate of change of
            // SwayAmp * sin(Sway), which is SwayAmp * SwayFreq * cos(Sway).
            float drift = wind * (0.4f + 0.6f * p.Z) + p.SwayAmp * p.SwayFreq * MathF.Cos(p.Sway);

            p.X += drift * dt;
            p.Y += fall * dt;

            // Blown off one side: come back in on the other (wrap around).
            if (p.X > _w + margin) p.X -= _w + 2 * margin;
            else if (p.X < -margin) p.X += _w + 2 * margin;

            // Reached the bottom: start a new petal.
            if (p.Y > _h + p.Size) Respawn(ref p, scatterOnScreen: false);
        }
    }

    // ==================================================================
    //  RENDER
    // ==================================================================

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
        foreach (ref readonly Petal p in _petals.AsSpan())
            DrawPetal(fb, p);
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
            int r = (int)((bg >> 16) & 0xFF), gg = (int)((bg >> 8) & 0xFF), b = (int)(bg & 0xFF);
            if (b < r + 20) continue;                      // not water
            // Brightest in the middle of the streak, fading at both ends.
            float t = 1 - MathF.Abs(i / (float)gl.Length * 2 - 1);
            fb.Pixels[row + x] = Blend(bg, 240, 248, 255, alpha * t);
        }
    }

    /// <summary>
    /// Draws one petal, pixel by pixel.
    ///
    /// For every pixel in a small square around the petal, we ask: "where is
    /// this pixel in the PETAL's own coordinates?" Rotating the pixel's offset
    /// backward by the petal's angle gives u (along the petal, -1 at the base,
    /// +1 at the tip) and v (across it). Then the question "is this pixel inside
    /// the petal?" is easy to ask about the petal's shape, no matter how the
    /// petal is turned. This trick (move the point into the shape's own frame,
    /// rather than moving the shape) is used all over computer graphics.
    ///
    /// The shape of a sakura petal: narrow where it joined the flower, broad and
    /// rounded at the outer end, with a little V-shaped notch in the tip.
    ///
    /// Soft edges: instead of "in or out", we estimate how many pixels inside
    /// the edge the point is. A pixel half-on the edge gets half coverage, which
    /// blends smoothly with the background (anti-aliasing).
    /// </summary>
    private static void DrawPetal(FrameBuffer fb, in Petal p)
    {
        float swayTilt = 0.5f * MathF.Sin(p.Sway);          // petals tilt into their swing
        float angle = p.Angle + swayTilt;
        float ca = MathF.Cos(angle), sa = MathF.Sin(angle);

        float halfLen = p.Size * 0.5f;
        float flip = MathF.Cos(p.Flip);
        float halfWid = halfLen * 0.72f * MathF.Max(0.12f, MathF.Abs(flip));  // the tumble squash

        // Color: blend white and pink. The back of a petal (flip < 0) is a touch
        // darker, so the tumble reads as the petal turning over.
        float side = flip >= 0 ? 1f : 0.86f;
        float baseR = 255, baseG = 246 - 70 * p.Pink, baseB = 249 - 50 * p.Pink;
        float opacity = (0.55f + 0.45f * p.Z) * p.Fade * 0.95f;  // far petals are fainter

        int reach = (int)MathF.Ceiling(halfLen) + 1;
        int x0 = Math.Max(0, (int)p.X - reach), x1 = Math.Min(fb.Width - 1, (int)p.X + reach);
        int y0 = Math.Max(0, (int)p.Y - reach), y1 = Math.Min(fb.Height - 1, (int)p.Y + reach);

        for (int py = y0; py <= y1; py++)
        {
            float dy = py + 0.5f - p.Y;
            int row = py * fb.Width;
            for (int px = x0; px <= x1; px++)
            {
                float dx = px + 0.5f - p.X;

                // Into the petal's own frame.
                float u = (dx * ca + dy * sa) / halfLen;         // -1 base ... +1 tip
                if (u < -1.1f || u > 1.1f) continue;
                float v = (-dx * sa + dy * ca) / halfWid;        // -1 ... +1 across
                float av = MathF.Abs(v);

                // How wide the petal is at position u (as a fraction of halfWid).
                float halfWidthHere = u >= 0
                    ? MathF.Sqrt(MathF.Max(0, 1 - u * u * u * u))                 // broad, rounded outer end
                    : MathF.Sqrt(MathF.Max(0, 1 - u * u)) * (0.4f + 0.6f * (1 + u)); // tapering toward the base

                // Distance (in pixels) inside each edge. Negative = outside.
                float dSide = (halfWidthHere - av) * halfWid;
                float dEnd = (1 - MathF.Abs(u)) * halfLen;
                float dNotch = (0.78f + 0.9f * av - u) * halfLen * 0.7f;          // the V notch at the tip
                float inside = MathF.Min(dSide, MathF.Min(dEnd, dNotch));

                float coverage = inside + 0.5f;                  // 1 px wide soft edge
                if (coverage <= 0) continue;
                if (coverage > 1) coverage = 1;

                // Deeper pink toward the base, lighter toward the tip.
                float k = 0.78f + 0.22f * (u + 1) * 0.5f;
                float r = baseR * k * side, g = baseG * (k - 0.06f * (1 - u) ) * side, b = baseB * k * side;

                fb.Pixels[row + px] = Blend(fb.Pixels[row + px], (int)r, (int)g, (int)b, coverage * opacity);
            }
        }
    }

    /// <summary>
    /// Mix a color over a pixel. alpha = how opaque the new color is (0 = invisible, 1 = solid).
    /// result = old + (new - old) * alpha, for each of red, green, and blue.
    /// </summary>
    private static uint Blend(uint bg, int r, int g, int b, float alpha)
    {
        int br = (int)((bg >> 16) & 0xFF), bgc = (int)((bg >> 8) & 0xFF), bb = (int)(bg & 0xFF);
        int nr = br + (int)((r - br) * alpha);
        int ng = bgc + (int)((g - bgc) * alpha);
        int nb = bb + (int)((b - bb) * alpha);
        return (uint)((Math.Clamp(nr, 0, 255) << 16) | (Math.Clamp(ng, 0, 255) << 8) | Math.Clamp(nb, 0, 255));
    }

    public void Dispose() { }
}

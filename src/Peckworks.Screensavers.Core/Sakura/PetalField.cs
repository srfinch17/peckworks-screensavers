namespace Peckworks.Screensavers.Core.Sakura;

/// <summary>
/// A flurry of cherry blossom petals drifting down like snow. Shared by every
/// sakura screensaver: each one paints its own backdrop and then lets this
/// class rain petals over it.
///
/// ======================================================================
///  THE BIG IDEA
/// ======================================================================
///
/// The petals are the ACTORS in front of a painted BACKDROP. Each frame the
/// screensaver copies its backdrop, then asks this class to draw every petal
/// where it is right now.
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
/// Some petals are born at blossom clusters the backdrop hands over (spots on
/// the branches), fading in, so they look like they are letting go of the
/// tree. The rest drift in from above the top of the screen.
/// </summary>
public sealed class PetalField
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

    private readonly Random _rng;
    private readonly int _w, _h;
    private readonly float _u;   // the size unit: petal size, sway, wind and fall speed scale with it
    private readonly IReadOnlyList<PointF> _blossomSpots;
    private readonly Petal[] _petals;
    private readonly float _speedScale, _windScale, _sizeScale;
    private float _time;

    /// <summary>
    /// The color of the light on the petals, as a multiplier for red, green
    /// and blue. (1, 1, 1) is plain daylight. Evening light is warmer, so a
    /// dusk scene sets something like (1, 0.9, 0.82) and every petal picks up
    /// the glow without the petal code knowing anything about sunsets.
    /// </summary>
    public (float R, float G, float B) Tint { get; set; } = (1f, 1f, 1f);

    /// <summary>
    /// What is falling. The motion (fall, sway, spin, tumble, depth) is the
    /// same for all three; only the outline changes. That is why autumn leaves
    /// and snow can borrow the petal engine instead of each needing their own.
    /// </summary>
    public enum FlakeShape
    {
        /// <summary>A cherry petal: narrow base, broad tip with a little notch.</summary>
        Petal,
        /// <summary>A leaf: pointed at both ends, like an almond, with a darker vein down the middle.</summary>
        Leaf,
        /// <summary>A snowflake: a soft round dot that does not show its tumble.</summary>
        Snow,
    }

    /// <summary>The outline of each falling thing. Cherry petals unless a scene says otherwise.</summary>
    public FlakeShape Shape { get; set; } = FlakeShape.Petal;

    /// <summary>
    /// The two colors every falling thing is mixed from. Each one picks its own
    /// spot between them when it is born, so a flurry is a blend rather than
    /// one flat color. Petals run from near-white to pink; an autumn scene
    /// might run from gold to deep red; snow sets both to white.
    /// </summary>
    public (Color Pale, Color Deep) Colors { get; set; } = (Color.FromArgb(255, 246, 249), Color.FromArgb(255, 176, 199));

    /// <param name="count">How many petals are in the air at once.</param>
    /// <param name="speedScale">1 = a slow, snow-like drift. 2 = twice as fast.</param>
    /// <param name="windScale">1 = a normal breeze. 0 = still air.</param>
    /// <param name="sizeScale">1 = normal petals.</param>
    /// <param name="blossomSpots">Places on the backdrop (blossom clusters) that petals can let go from. May be empty.</param>
    /// <param name="sizeUnit">
    /// What petal sizes and speeds are measured against. Leave it out to use the
    /// height. A tall (portrait) screen should pass something smaller, such as
    /// min(height, width * 9 / 16), so the petals stay in proportion to a
    /// backdrop that was scaled the same way.
    /// </param>
    public PetalField(int width, int height, int count, float speedScale, float windScale, float sizeScale,
                      IReadOnlyList<PointF> blossomSpots, Random rng, float? sizeUnit = null)
    {
        _w = width;
        _h = height;
        _u = sizeUnit ?? height;
        _rng = rng;
        _speedScale = speedScale;
        _windScale = windScale;
        _sizeScale = sizeScale;
        _blossomSpots = blossomSpots;

        _petals = new Petal[Math.Max(1, count)];
        for (int i = 0; i < _petals.Length; i++)
        {
            // Spread depths evenly so there are always some near and some far.
            _petals[i].Z = (i + (float)_rng.NextDouble()) / _petals.Length;
            Respawn(ref _petals[i], scatterOnScreen: true);
        }
        // Draw far petals first and near ones last, so near petals cover far
        // ones (the "painter's algorithm": paint the back first). Depth never
        // changes for a slot, so sorting once is enough.
        Array.Sort(_petals, (a, b) => a.Z.CompareTo(b.Z));
    }

    /// <summary>
    /// A sensible petal count for a screen: about 220 at 100% on a 16:9
    /// screen, more on wider screens, scaled by the user's density setting.
    /// </summary>
    /// <param name="sizeUnit">
    /// Pass the same size unit the field is built with to keep the flurry
    /// equally THICK on every screen shape. Flakes are sized by this unit, so
    /// on a tall screen (where the unit is small) each flake is small, and
    /// the screen needs many more of them to look as full. The count becomes
    /// "how many unit-sized squares fit on the screen", which on a normal
    /// wide screen works out to exactly the same number as leaving this out.
    /// </param>
    public static int CountFor(int width, int height, int densityPercent, float? sizeUnit = null)
    {
        float aspect = sizeUnit is float u
            ? width * (float)height / Math.Max(1f, u * u)
            : width / (float)Math.Max(1, height);
        return Math.Max(10, (int)(220 * densityPercent / 100f * aspect / (16f / 9f)));
    }

    /// <summary>Give a petal a fresh random personality and a starting spot.</summary>
    private void Respawn(ref Petal p, bool scatterOnScreen)
    {
        p.Size = Math.Max(3f, _u * 0.013f * (0.45f + 1.2f * p.Z) * _sizeScale);
        p.Angle = (float)(_rng.NextDouble() * Math.PI * 2);
        p.Spin = ((float)_rng.NextDouble() - 0.5f) * 1.6f;
        p.Flip = (float)(_rng.NextDouble() * Math.PI * 2);
        p.FlipSpeed = 0.8f + 2.2f * (float)_rng.NextDouble();
        p.Sway = (float)(_rng.NextDouble() * Math.PI * 2);
        p.SwayFreq = 0.5f + 1.1f * (float)_rng.NextDouble();
        p.SwayAmp = _u * (0.01f + 0.025f * (float)_rng.NextDouble()) * (0.5f + p.Z);
        p.Pink = (float)_rng.NextDouble();

        if (scatterOnScreen)
        {
            // At startup, scatter petals across the whole screen so it is
            // already snowing blossoms when the screensaver appears.
            p.X = (float)_rng.NextDouble() * _w;
            p.Y = (float)_rng.NextDouble() * _h;
            p.Fade = 1;
        }
        else if (_blossomSpots.Count > 0 && _rng.NextDouble() < 0.35)
        {
            // Let go of a blossom cluster on a branch, fading in.
            PointF spot = _blossomSpots[_rng.Next(_blossomSpots.Count)];
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

    /// <summary>Move every petal forward by dt seconds.</summary>
    public void Update(float dt)
    {
        _time += dt;

        // The breeze: two slow waves added together, so gusts rise and fall on
        // an irregular rhythm instead of a steady beat. Mostly blowing right,
        // occasionally stilling almost to nothing.
        float wind = _u * 0.03f * _windScale *
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
            float fall = _u * 0.045f * (0.45f + 0.75f * p.Z) * _speedScale * (0.8f + 0.45f * (1 - faceOn));

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
    //  DRAW
    // ==================================================================

    /// <summary>Draw every petal over whatever is already in the frame, far ones first.</summary>
    public void Draw(FrameBuffer fb)
    {
        foreach (ref readonly Petal p in _petals.AsSpan())
            DrawPetal(fb, p);
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
    private void DrawPetal(FrameBuffer fb, in Petal p)
    {
        if (Shape == FlakeShape.Snow) { DrawSnow(fb, p); return; }
        bool leaf = Shape == FlakeShape.Leaf;

        float swayTilt = 0.5f * MathF.Sin(p.Sway);          // petals tilt into their swing
        float angle = p.Angle + swayTilt;
        float ca = MathF.Cos(angle), sa = MathF.Sin(angle);

        float halfLen = p.Size * 0.5f;
        float flip = MathF.Cos(p.Flip);
        float halfWid = halfLen * 0.72f * MathF.Max(0.12f, MathF.Abs(flip));  // the tumble squash

        // Color: blend white and pink, lit by the scene's light. The back of a
        // petal (flip < 0) is a touch darker, so the tumble reads as the petal
        // turning over.
        float side = flip >= 0 ? 1f : 0.86f;
        var (pale, deep) = Colors;
        float baseR = (pale.R + (deep.R - pale.R) * p.Pink) * Tint.R;
        float baseG = (pale.G + (deep.G - pale.G) * p.Pink) * Tint.G;
        float baseB = (pale.B + (deep.B - pale.B) * p.Pink) * Tint.B;
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
                // A leaf is widest in the middle and comes to a point at both
                // ends: 1 - u*u is 1 at the middle (u = 0) and 0 at each end.
                float halfWidthHere = leaf
                    ? MathF.Max(0, 1 - u * u)
                    : u >= 0
                        ? MathF.Sqrt(MathF.Max(0, 1 - u * u * u * u))                 // broad, rounded outer end
                        : MathF.Sqrt(MathF.Max(0, 1 - u * u)) * (0.4f + 0.6f * (1 + u)); // tapering toward the base

                // Distance (in pixels) inside each edge. Negative = outside.
                float dSide = (halfWidthHere - av) * halfWid;
                float dEnd = (1 - MathF.Abs(u)) * halfLen;
                float dNotch = leaf ? dEnd : (0.78f + 0.9f * av - u) * halfLen * 0.7f;   // the V notch at a petal's tip
                float inside = MathF.Min(dSide, MathF.Min(dEnd, dNotch));

                float coverage = inside + 0.5f;                  // 1 px wide soft edge
                if (coverage <= 0) continue;
                if (coverage > 1) coverage = 1;

                // Deeper pink toward the base, lighter toward the tip.
                float k = 0.78f + 0.22f * (u + 1) * 0.5f;
                if (leaf && av * halfWid < 0.7f) k *= 0.72f;     // the leaf's vein: a darker line down the middle
                float r = baseR * k * side, g = baseG * (k - 0.06f * (1 - u)) * side, b = baseB * k * side;

                fb.Pixels[row + px] = FrameBuffer.Blend(fb.Pixels[row + px], (int)r, (int)g, (int)b, coverage * opacity);
            }
        }
    }

    /// <summary>
    /// Draws one snowflake: a soft round dot. From across a yard you cannot see
    /// a snowflake's six arms, only a pale blur, so a dot is the honest shape.
    /// The dot is solid in the middle and fades out toward its edge, which is
    /// what makes it look soft instead of like a white coin.
    /// </summary>
    private void DrawSnow(FrameBuffer fb, in Petal p)
    {
        float radius = MathF.Max(0.75f, p.Size * 0.3f);   // the floor keeps far flakes visible without turning them into blocks in the tiny preview box
        var (pale, deep) = Colors;
        int r = (int)((pale.R + (deep.R - pale.R) * p.Pink) * Tint.R);
        int g = (int)((pale.G + (deep.G - pale.G) * p.Pink) * Tint.G);
        int b = (int)((pale.B + (deep.B - pale.B) * p.Pink) * Tint.B);
        float opacity = (0.5f + 0.5f * p.Z) * p.Fade;   // far flakes are fainter

        int reach = (int)MathF.Ceiling(radius) + 1;
        int x0 = Math.Max(0, (int)p.X - reach), x1 = Math.Min(fb.Width - 1, (int)p.X + reach);
        int y0 = Math.Max(0, (int)p.Y - reach), y1 = Math.Min(fb.Height - 1, (int)p.Y + reach);

        for (int py = y0; py <= y1; py++)
        {
            float dy = py + 0.5f - p.Y;
            int row = py * fb.Width;
            for (int px = x0; px <= x1; px++)
            {
                float dx = px + 0.5f - p.X;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                // 1 in the inner half of the dot, sliding down to 0 at its edge.
                float coverage = Math.Clamp((radius - dist) / (radius * 0.5f), 0f, 1f);
                if (coverage <= 0) continue;
                fb.Pixels[row + px] = FrameBuffer.Blend(fb.Pixels[row + px], r, g, b, coverage * opacity);
            }
        }
    }
}

using Peckworks.Screensavers.Core;

namespace MatrixRain;

/// <summary>
/// The digital rain itself.
///
/// ======================================================================
///  THE BIG IDEA (read this first)
/// ======================================================================
///
/// It LOOKS like streams of characters sliding down the screen. That is not
/// what is happening, and understanding the trick is the key to the whole
/// effect.
///
/// Picture a sports stadium scoreboard made of light bulbs, where each bulb is
/// shaped like a character. The bulbs NEVER move. Each column of bulbs has a
/// little invisible cursor (a "drop") walking down it, one bulb at a time. When
/// the cursor steps onto a bulb, that bulb flashes on at full brightness, then
/// slowly dims over the next couple of seconds.
///
/// Walk the cursor down and you get: a bright bulb where the cursor is now,
/// slightly dimmer ones just above it (lit a moment ago), dimmer ones above
/// those... a comet with a fading tail. Your eye reads "a stream falling", but
/// nothing moved except where the light is. That is exactly how the film's
/// rain behaves: characters stay put, and the brightness wave travels.
///
/// So the program keeps two simple lists:
///
///   CELLS: the grid of bulbs. Each remembers which character it shows and how
///          bright it is right now.
///   DROPS: the cursors. Each remembers its column, how far down it is, and how
///          fast it walks.
///
/// Every frame (Update): cells dim a little, drops step down, any cell a drop
/// steps onto is re-lit at full brightness with a fresh random character.
/// Every frame (Render): stamp each lit cell's character in a green whose
/// brightness matches the cell, paint the cell under each cursor near-white,
/// then add glow.
///
/// Details copied from the film that sell the effect:
///   - The leading character is almost white, and it flickers through random
///     characters as it moves.
///   - Characters left behind in the tail occasionally change on their own.
///   - Drops fall at different speeds and some columns are dimmer, which adds a
///     sense of depth.
///   - Movement is in whole-character steps, not smooth sliding.
/// </summary>
internal sealed class MatrixRainScene : IScreensaverScene
{
    // ------------------------------------------------------------------
    // The two lists. "struct" = a small bundle of values stored inline in the
    // array (no separate object per cell), which keeps thousands of them fast.
    // ------------------------------------------------------------------

    /// <summary>One bulb on the scoreboard.</summary>
    private struct Cell
    {
        public int Glyph;          // which character (an index into the GlyphAtlas)
        public float Brightness;   // 0 = dark, 1 = freshly lit
        public float FadePerSec;   // how much Brightness drops per second
        public float ChangeIn;     // seconds until this cell swaps to a random new character
    }

    /// <summary>One invisible cursor walking down a column.</summary>
    private struct Drop
    {
        public int Column;
        public float Row;          // how far down, in cells. Fractional: 7.4 means "on row 7, 40% of the way to row 8"
        public float Speed;        // cells per second
        public float TrailSeconds; // how long the cells it lights take to fade out
        public float Intensity;    // 0..1, how bright this drop's trail is (dimmer ones feel farther away)
        public float Wait;         // seconds to wait off-screen before starting again
    }

    private readonly Random _rng = new();
    private readonly MatrixRainSettings _settings;
    private readonly GlyphAtlas _atlas;
    private readonly BloomEffect _bloom;

    private readonly int _cols, _rows, _cellW, _cellH, _offsetX;
    private readonly Cell[] _cells;          // _cols * _rows of them, row by row
    private readonly float[] _headGlow;      // per cell: > 0 if a cursor is sitting on it this frame
    private readonly Drop[] _drops;

    // Colors precomputed for each of 256 brightness levels (see BuildPalette).
    private readonly int[] _palR = new int[256], _palG = new int[256], _palB = new int[256];

    // Base drop speeds in cells per second, before the Speed setting multiplies them.
    private const float MinSpeed = 7f, MaxSpeed = 22f;

    public MatrixRainScene(int width, int height, double pixelScale, MatrixRainSettings settings)
    {
        _settings = settings;

        // ---- Grid geometry ----
        // Character height in real pixels: the setting (for a 1080p screen)
        // times how big this surface is compared with 1080p.
        _cellH = Math.Max(6, (int)Math.Round(settings.CharacterSize * pixelScale));
        // The film's columns are narrower than they are tall.
        _cellW = Math.Max(4, (int)Math.Round(_cellH * 0.70));

        _cols = Math.Max(1, width / _cellW);
        _rows = Math.Max(1, (height + _cellH - 1) / _cellH);   // round up: last row may be cut off by the bottom edge
        _offsetX = (width - _cols * _cellW) / 2;               // center the columns, leftover pixels split on both sides

        _atlas = new GlyphAtlas(_cellW, _cellH);

        // ---- Cells: every bulb starts dark, holding a random character ----
        _cells = new Cell[_cols * _rows];
        _headGlow = new float[_cells.Length];
        for (int i = 0; i < _cells.Length; i++)
        {
            _cells[i].Glyph = RandomGlyph();
            _cells[i].ChangeIn = NextChangeDelay();
        }

        // ---- Drops ----
        // About one drop per column at 100% density. The first _cols drops get
        // one column each so no column stays empty forever; any extras land on
        // random columns, doubling up like the film's busier columns.
        int dropCount = Math.Max(1, (int)Math.Round(_cols * settings.DensityPercent / 100.0));
        _drops = new Drop[dropCount];
        for (int i = 0; i < dropCount; i++)
        {
            _drops[i].Column = i < _cols ? i : _rng.Next(_cols);
            Respawn(ref _drops[i], firstTime: true);
        }

        // ---- Glow ----
        // Shrink factor and blur reach grow with character size so the halo is
        // always about the same size RELATIVE to a character.
        int factor = Math.Clamp(_cellH / 7, 2, 6);
        _bloom = new BloomEffect(width, height, factor, blurRadius: 2,
            strength: 1.1f * settings.GlowPercent / 100f);

        BuildPalette();
    }

    // ==================================================================
    //  UPDATE: move the world forward
    // ==================================================================

    public void Update(double elapsedSeconds)
    {
        float dt = (float)elapsedSeconds;

        // 1. Every cell dims, and occasionally swaps its character.
        for (int i = 0; i < _cells.Length; i++)
        {
            // "ref" means "work on the real cell in the array, not a copy".
            // Without it, C# would hand us a copy of the struct and our changes
            // would vanish.
            ref Cell c = ref _cells[i];
            if (c.Brightness > 0)
            {
                c.Brightness -= c.FadePerSec * dt;
                if (c.Brightness < 0) c.Brightness = 0;
            }
            c.ChangeIn -= dt;
            if (c.ChangeIn <= 0)
            {
                c.Glyph = RandomGlyph();
                c.ChangeIn = NextChangeDelay();
            }
        }

        // 2. Forget last frame's cursor positions; we mark fresh ones below.
        Array.Clear(_headGlow);

        // 3. Every drop steps down.
        for (int d = 0; d < _drops.Length; d++)
        {
            ref Drop drop = ref _drops[d];

            if (drop.Wait > 0)            // still resting off-screen
            {
                drop.Wait -= dt;
                continue;
            }

            // Which whole row was it on, and which is it on now?
            // A fast drop on a slow frame can skip more than one row, so we
            // light EVERY row it passed over, not just the one it landed on.
            // Otherwise fast drops would leave gaps in their tails.
            int before = (int)MathF.Floor(drop.Row);
            drop.Row += drop.Speed * dt;
            int after = (int)MathF.Floor(drop.Row);

            for (int row = before + 1; row <= after; row++)
                if (row >= 0 && row < _rows)
                    LightCell(row * _cols + drop.Column, drop);

            if (after >= 0 && after < _rows)
            {
                int i = after * _cols + drop.Column;
                _headGlow[i] = MathF.Max(_headGlow[i], drop.Intensity);

                // The leading character flickers rapidly between characters.
                ref Cell head = ref _cells[i];
                if (head.ChangeIn > 0.05f) head.ChangeIn = 0.03f + 0.05f * _rng.NextSingle();
            }

            // Fell off the bottom: start over at the top after a short rest.
            if (after >= _rows) Respawn(ref drop, firstTime: false);
        }
    }

    /// <summary>A drop just stepped onto this cell: light it up with a fresh character.</summary>
    private void LightCell(int index, in Drop drop)
    {
        ref Cell c = ref _cells[index];
        c.Brightness = drop.Intensity;
        // Fade speed chosen so it reaches zero after exactly TrailSeconds.
        c.FadePerSec = drop.Intensity / drop.TrailSeconds;
        c.Glyph = RandomGlyph();
        c.ChangeIn = NextChangeDelay();
    }

    /// <summary>Give a drop a new random personality and put it back at the top.</summary>
    private void Respawn(ref Drop drop, bool firstTime)
    {
        float speedScale = _settings.SpeedPercent / 100f;
        float r = _rng.NextSingle();

        drop.Speed = (MinSpeed + (MaxSpeed - MinSpeed) * r * r) * speedScale; // r*r: more slow drops than fast ones
        drop.TrailSeconds = 1.0f + 2.0f * _rng.NextSingle();                   // 1 to 3 seconds of tail
        drop.Intensity = 0.55f + 0.45f * MathF.Sqrt(_rng.NextSingle());        // most bright, some dim ("farther away")

        if (firstTime)
        {
            // Stagger the start: each drop begins somewhere ABOVE the screen, so
            // the rain pours in over the first few seconds, like the film's
            // opening, instead of every column starting in a flat line.
            drop.Row = -_rng.NextSingle() * _rows * 1.2f;
            drop.Wait = 0;
        }
        else
        {
            drop.Row = -1;
            drop.Wait = _rng.NextSingle() * 3f / speedScale;
        }
    }

    private int RandomGlyph() => _atlas.WeightedPicks[_rng.Next(_atlas.WeightedPicks.Length)];

    /// <summary>
    /// How long until a cell spontaneously changes character. Uses an
    /// "exponential" random delay: mostly short waits, occasionally long ones.
    /// That is how real random events (like raindrops hitting a particular
    /// tile) are spaced, so the flicker feels natural instead of rhythmic.
    /// The formula -ln(1 - u) * average turns a plain 0..1 random number into
    /// that kind of delay.
    /// </summary>
    private float NextChangeDelay() => -MathF.Log(1f - _rng.NextSingle()) * 2.5f;

    // ==================================================================
    //  RENDER: draw the current state
    // ==================================================================

    public void Render(FrameBuffer fb)
    {
        fb.Clear();   // start from black

        // Split the rows of cells across all CPU cores. Different rows of
        // cells never overlap on screen, so the cores never write the same
        // pixel and cannot interfere with each other.
        Parallel.For(0, _rows, row =>
        {
            int y0 = row * _cellH;
            int visibleH = Math.Min(_cellH, fb.Height - y0);   // last row may be cut off
            if (visibleH <= 0) return;

            for (int col = 0; col < _cols; col++)
            {
                int i = row * _cols + col;
                float head = _headGlow[i];
                float b = _cells[i].Brightness;
                if (head <= 0 && b < 0.01f) continue;   // dark cell: nothing to draw

                int r, g, bl;
                if (head > 0)
                {
                    // The cursor's own cell: near-white with a green tint.
                    float k = 0.7f + 0.3f * head;
                    r = (int)(200 * k); g = (int)(255 * k); bl = (int)(215 * k);
                }
                else
                {
                    int level = Math.Min(255, (int)(b * 255));
                    r = _palR[level]; g = _palG[level]; bl = _palB[level];
                }

                Stamp(fb, _atlas.Mask(_cells[i].Glyph), _offsetX + col * _cellW, y0, visibleH, r, g, bl);
            }
        });

        _bloom.Apply(fb);
    }

    /// <summary>
    /// Copy one character stamp onto the picture, tinted with color (r, g, b).
    /// Each stamp pixel's ink amount (0..255) scales the color: full ink gives
    /// the full color, half ink gives half, which is what makes the edges smooth.
    /// </summary>
    private void Stamp(FrameBuffer fb, byte[] mask, int x0, int y0, int visibleH, int r, int g, int b)
    {
        uint[] px = fb.Pixels;
        int w = _cellW, stride = fb.Width;
        for (int y = 0; y < visibleH; y++)
        {
            int maskRow = y * w;
            int outRow = (y0 + y) * stride + x0;
            for (int x = 0; x < w; x++)
            {
                int ink = mask[maskRow + x];
                if (ink == 0) continue;
                // (color * ink) >> 8 is a fast way to say color * ink / 256.
                // ">> 8" shifts the bits right by 8, which divides by 256.
                px[outRow + x] = (uint)((((r * ink) >> 8) << 16) | (((g * ink) >> 8) << 8) | ((b * ink) >> 8));
            }
        }
    }

    /// <summary>
    /// Builds the brightness to color lookup table ("palette").
    ///
    /// A table beats computing the color every time: we work out 256 colors
    /// once, then drawing a cell of brightness 0.73 is just "look up entry 186".
    ///
    /// The color path: black, then deep forest green, then the film's signature
    /// phosphor green, then a pale mint just behind the white head. The stops
    /// were chosen by comparing against frames of the film's opening titles.
    /// </summary>
    private void BuildPalette()
    {
        // (brightness, red, green, blue)
        (float At, int R, int G, int B)[] stops =
        [
            (0.00f,   0,   0,   0),
            (0.10f,   0,  26,   6),
            (0.30f,   0,  80,  22),
            (0.55f,   8, 150,  48),
            (0.80f,  40, 215,  85),
            (0.95f,  95, 245, 125),
            (1.00f, 150, 255, 165),
        ];

        for (int level = 0; level < 256; level++)
        {
            float t = level / 255f;
            int s = 0;
            while (s < stops.Length - 2 && t > stops[s + 1].At) s++;
            var a = stops[s];
            var z = stops[s + 1];
            float f = Math.Clamp((t - a.At) / (z.At - a.At), 0f, 1f);
            _palR[level] = (int)(a.R + (z.R - a.R) * f);
            _palG[level] = (int)(a.G + (z.G - a.G) * f);
            _palB[level] = (int)(a.B + (z.B - a.B) * f);
        }
    }

    public void Dispose() { /* nothing unmanaged to release; here to satisfy IDisposable */ }
}

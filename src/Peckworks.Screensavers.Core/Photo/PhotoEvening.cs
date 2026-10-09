namespace Peckworks.Screensavers.Core.Photo;

/// <summary>
/// What differs from photo to photo when evening falls: where the horizon
/// is, and how to tell a window's glass from its frame.
/// </summary>
/// <param name="Horizon">How far down the photo the horizon is (0 top, 1 bottom). The dusk sky is palest there.</param>
/// <param name="FrameColour">
/// How colourful a pixel inside a window's box must be to count as FRAME
/// rather than glass (0 = grey, 1 = pure colour). Sunlit orange stone is very
/// colourful; grey glass is not.
/// </param>
/// <param name="FrameBright">How bright (0 to 1) a frame pixel must also be. Dark coloured things behind the glass (a brown curtain) are still glass.</param>
public sealed record EveningLook(float Horizon, float FrameColour, float FrameBright)
{
    /// <summary>Honey-coloured stone in low sun around grey glass (tuned on the retired Cotswold Brook).</summary>
    public static readonly EveningLook SunlitStone = new(0.45f, 0.36f, 0.30f);
}

/// <summary>
/// Evening falling on a daytime photo: the gold drains out, dusk comes,
/// windows light up one by one and fireflies come out, and after a while the
/// gold slowly returns.
///
/// HOW DUSK IS MADE FROM A DAYTIME PHOTO: we cannot re-take the photo at
/// dusk, so we do what a film colourist does ("day for night"). Make the
/// picture darker, take most of the colour out, and tint what is left blue:
/// at dusk the light comes from the blue sky, not the sun. The sky itself is
/// repainted deep blue overhead, paling to a rose afterglow at the horizon.
/// Two versions of the photo are made once at startup, golden and dusk, and
/// the screen shows a mix of the two.
///
/// THE MIX moves one step (of 256 from golden to dusk) at a time, each step
/// spread over four frames: see Refresh.
/// </summary>
public sealed class PhotoEvening
{
    /// <summary>One pixel of window glass: where it is, how surely it is glass (0 to 256), and its lamplit colour.</summary>
    private readonly record struct WindowPane(int Index, int Glass, uint Lit);

    private readonly PhotoBackdrop _photo;
    private readonly EveningLook _look;
    private readonly uint[] _golden, _dusk;
    private readonly float _cycleSeconds;
    private readonly WindowPane[][] _panes;   // per window: its glass pixels and the colour each glows when lit
    private readonly PointF[] _windowCentres;
    private readonly PointF[] _lamps;
    private readonly Sprite _windowGlow;
    private readonly PhotoFireflies _fireflies;
    private int _mixedAt = -1;                // the dusk step (0 to 256) EVERY row of the backdrop is at; -1 = not built yet
    private int _sweepStep, _sweepRow;        // the step the current sweep is bringing rows to, and the next row it will do
    private double _time;

    /// <summary>The photo at the current hour: what the scene copies to the screen each frame.</summary>
    public uint[] Backdrop { get; }

    /// <summary>How far into dusk we are now: 0 golden ... 1 dusk.</summary>
    public float Dusk { get; private set; }

    /// <param name="windows">Each window as a box on the photo (left, top, width, height, fractions).</param>
    /// <param name="fireflyZones">Where the fireflies live, and how far away each place is.</param>
    /// <param name="fireflyAmount">1 = about eighteen on a 16:9 screen, 0 = none.</param>
    /// <param name="cycleMinutes">How long one full turn of the light takes. 0 = always golden.</param>
    /// <param name="lamps">Lamps out in the open (a porch light), as spots on the photo: a warm glow that comes on with the windows.</param>
    public PhotoEvening(PhotoBackdrop photo, EveningLook look, RectangleF[] windows, FireflyZone[] fireflyZones,
        float fireflyAmount, float cycleMinutes, Random rng, PointF[]? lamps = null)
    {
        _lamps = (lamps ?? []).Select(l => photo.ToScreen(l.X, l.Y)).ToArray();
        _photo = photo;
        _look = look;
        float p = photo.P;
        _golden = (uint[])photo.Pixels.Clone();          // our own copy: LayMist may paint on it
        _dusk = MakeDusk(_golden, photo.Width, photo.Height);
        Backdrop = new uint[_golden.Length];
        _cycleSeconds = cycleMinutes * 60f;

        _panes = windows.Select(FindGlass).ToArray();
        _windowCentres = windows.Select(b => photo.ToScreen(b.X + b.Width / 2, b.Y + b.Height / 2)).ToArray();
        _windowGlow = Sprite.Glow(Math.Max(4, (int)(p * 0.03f)), Color.FromArgb(255, 170, 90));
        _fireflies = new PhotoFireflies(photo, fireflyZones, fireflyAmount, rng);
        Refresh();
    }

    /// <summary>
    /// Paints a still sheet of mist into both pictures the evening fades
    /// between: a little of it in the golden light, more at dusk (evening mist
    /// gathers as the air cools). Painted once here, it costs nothing per
    /// frame; done every frame at 4K it cost several milliseconds. Like a
    /// scene painter adding the haze to the backdrop cloth itself instead of
    /// running a smoke machine all night.
    /// </summary>
    /// <param name="sheet">How much mist each screen pixel has at full strength, 0 to 255.</param>
    /// <param name="golden">The share of it in the golden light (0 to 1).</param>
    /// <param name="dusk">The share of it at dusk (0 to 1).</param>
    public void LayMist(byte[] sheet, Color colour, float golden, float dusk)
    {
        Lay(_golden, golden);
        Lay(_dusk, dusk);
        _mixedAt = -1;                                     // rebuild the whole backdrop from the new pictures
        _sweepRow = 0;
        Refresh();

        void Lay(uint[] picture, float strength)
        {
            int k = (int)(Math.Clamp(strength, 0f, 1f) * 256);
            for (int i = 0; i < picture.Length; i++)
            {
                int a = sheet[i] * k >> 8;                 // how much mist on this pixel, 0 to 255
                if (a == 0) continue;
                uint c = picture[i];
                int r = (int)((c >> 16) & 0xFF), g = (int)((c >> 8) & 0xFF), b = (int)(c & 0xFF);
                r += (colour.R - r) * a >> 8; g += (colour.G - g) * a >> 8; b += (colour.B - b) * a >> 8;
                picture[i] = (uint)((r << 16) | (g << 8) | b);
            }
        }
    }

    public void Update(double dt)
    {
        _time += dt;
        _fireflies.Update(dt);
        Refresh();
    }

    /// <summary>
    /// How far into dusk we are at this moment of the cycle. The cycle:
    /// golden for 30% of it, dusk falling for 20%, dusk for 35%, gold
    /// returning for 15%. The changes ease in and out (smoothstep), so there
    /// is no moment where the light visibly lurches.
    /// </summary>
    private float DuskNow()
    {
        if (_cycleSeconds <= 0) return 0;
        float q = (float)(_time % _cycleSeconds / _cycleSeconds);
        static float Ease(float t) => t * t * (3 - 2 * t);
        if (q < 0.30f) return 0;
        if (q < 0.50f) return Ease((q - 0.30f) / 0.20f);
        if (q < 0.85f) return 1;
        return 1 - Ease((q - 0.85f) / 0.15f);
    }

    /// <summary>
    /// Brings the backdrop toward the current mix of golden and dusk.
    ///
    /// The mix is counted in 256 steps; one step moves a colour by less than
    /// one shade. Redoing all eight million pixels of a 4K screen in one
    /// frame stalled that frame (a hitch every third of a second through the
    /// fade). So each step is spread over four frames, a quarter of the
    /// screen each, and the backdrop never moves more than ONE step per
    /// sweep. Mid-sweep, the top of the screen is one step ahead of the
    /// bottom: less than a shade, which no eye can see. (An earlier version
    /// let one sweep jump many steps at once, and the edge between the
    /// updated and the waiting part showed as a wave running down the screen.)
    /// </summary>
    private void Refresh()
    {
        Dusk = DuskNow();
        int target = (int)(Dusk * 256);
        int w = _photo.Width, h = _photo.Height;
        if (_mixedAt < 0)
        {
            MixRows(0, h, target);                         // the first frame: all of it, at once
            _mixedAt = target;
            return;
        }
        if (_sweepRow == 0)
        {
            if (target == _mixedAt) return;                // nothing to do: the light is holding still
            _sweepStep = _mixedAt + Math.Sign(target - _mixedAt);
        }
        int band = (h + 3) / 4;
        int end = Math.Min(h, _sweepRow + band);
        MixRows(_sweepRow, end, _sweepStep);
        _sweepRow = end >= h ? 0 : end;
        if (_sweepRow == 0) _mixedAt = _sweepStep;
    }

    private void MixRows(int y0, int y1, int k)
    {
        int w = _photo.Width;
        Parallel.For(y0, y1, y =>
        {
            for (int i = y * w, end = i + w; i < end; i++)
            {
                uint a = _golden[i], b = _dusk[i];
                uint rb = ((a & 0xFF00FF) * (uint)(256 - k) + (b & 0xFF00FF) * (uint)k) >> 8 & 0xFF00FF;
                uint g = ((a & 0x00FF00) * (uint)(256 - k) + (b & 0x00FF00) * (uint)k) >> 8 & 0x00FF00;
                Backdrop[i] = rb | g;
            }
        });
    }

    /// <summary>
    /// Lamps in the windows, coming on as the light goes. Each window has its
    /// own moment (spread over the first half of the dusk) so they do not all
    /// switch on together. Each glass pixel is blended toward its lamplit
    /// colour; then a faint glow spills onto the wall around the window.
    /// </summary>
    public void DrawWindows(FrameBuffer fb)
    {
        if (Dusk <= 0.05f) return;
        uint[] frame = fb.Pixels;
        for (int i = 0; i < _panes.Length; i++)
        {
            float on = Math.Clamp((Dusk - 0.1f - 0.03f * i) / 0.25f, 0, 1);
            if (on <= 0) continue;
            on *= 1 + 0.03f * (float)Math.Sin(_time * (5.3 + i) + i);      // the faint unsteadiness of a lamp or a fire
            int on256 = (int)(Math.Min(1f, on) * 256);
            foreach (WindowPane pane in _panes[i])
            {
                int a = pane.Glass * on256 >> 8;
                uint bg = frame[pane.Index], lit = pane.Lit;
                int r = (int)((bg >> 16) & 0xFF), g = (int)((bg >> 8) & 0xFF), b = (int)(bg & 0xFF);
                r += (((int)((lit >> 16) & 0xFF) - r) * a) >> 8;
                g += (((int)((lit >> 8) & 0xFF) - g) * a) >> 8;
                b += (((int)(lit & 0xFF) - b) * a) >> 8;
                frame[pane.Index] = (uint)((r << 16) | (g << 8) | b);
            }
            _windowGlow.DrawCentered(fb, _windowCentres[i].X, _windowCentres[i].Y, 0.22f * on);
        }
        float lampOn = Math.Clamp((Dusk - 0.1f) / 0.25f, 0, 1);
        foreach (PointF lamp in _lamps)
            _windowGlow.DrawCentered(fb, lamp.X, lamp.Y, 0.3f * lampOn);
    }

    /// <summary>Fireflies (PhotoFireflies.cs), only once it is properly dusk.</summary>
    public void DrawFireflies(FrameBuffer fb) => _fireflies.Draw(fb, Math.Clamp((Dusk - 0.55f) / 0.35f, 0, 1));

    /// <summary>
    /// Finds the glass inside one window's box. A frame (sunlit stone, a
    /// painted sash) is BRIGHT and colourful; anything else in the box is
    /// glass, including dark coloured things seen through it (a brown
    /// curtain, a dim room), which would otherwise leave the lit pane
    /// blotchy. Leaves in front of the window are left alone. Each glass
    /// pixel gets a lamplit colour that keeps the photo's own light and dark
    /// (a curtain, a glazing bar) so the lit window still has its detail.
    /// </summary>
    private WindowPane[] FindGlass(RectangleF box)
    {
        PointF a = _photo.ToScreen(box.Left, box.Top), b = _photo.ToScreen(box.Right, box.Bottom);
        var found = new List<WindowPane>();
        for (int y = Math.Max(0, (int)a.Y); y < Math.Min(_photo.Height, (int)b.Y); y++)
            for (int x = Math.Max(0, (int)a.X); x < Math.Min(_photo.Width, (int)b.X); x++)
            {
                int i = y * _photo.Width + x;
                uint c = _golden[i];
                int r = (int)((c >> 16) & 0xFF), g = (int)((c >> 8) & 0xFF), bl = (int)(c & 0xFF);
                int max = Math.Max(r, Math.Max(g, bl)), min = Math.Min(r, Math.Min(g, bl));
                float colourful = max == 0 ? 0 : (max - min) / (float)max;     // 0 = grey ... 1 = pure colour
                float lum = (0.30f * r + 0.59f * g + 0.11f * bl) / 255f;
                float frame = Math.Clamp((colourful - _look.FrameColour) / 0.14f, 0, 1) * Math.Clamp((lum - _look.FrameBright) / 0.15f, 0, 1);
                float glass = 1 - frame;
                if (g > r + 16) glass = 0;                                     // clearly green: a leaf in front of the window (glass tinted a little green by a reflected tree still counts)
                if (glass <= 0.05f) continue;
                found.Add(new WindowPane(i, (int)(glass * 256), (uint)(lum * 1000)));   // the lamplit colour comes next; for now keep the brightness
            }
        if (found.Count == 0) return [];

        // The lamplight. Every window glows equally warm, whether its glass
        // looked dark or bright in the photo, but keeps its own light and
        // dark (a curtain, a glazing bar) RELATIVE to its average: a pixel
        // twice as bright as the window's average is lit a little brighter.
        float average = Math.Max(0.02f, (float)found.Average(f => f.Lit / 1000f));
        return found.Select(f =>
        {
            float k = Math.Clamp(0.78f + 0.30f * (f.Lit / 1000f / average - 1), 0.45f, 1.05f);
            uint lit = FrameBuffer.Rgb((int)Math.Min(255, 255 * k), (int)Math.Min(255, 182 * k), (int)Math.Min(255, 96 * k));
            return f with { Lit = lit };
        }).ToArray();
    }

    /// <summary>
    /// "Day for night": the same photo as dusk. Darker, most of the colour
    /// gone, tinted toward blue, and darker still toward the bottom (the sky
    /// at the top is where the last light is).
    /// </summary>
    private uint[] MakeDusk(uint[] src, int w, int h)
    {
        var dst = new uint[src.Length];
        PointF top = _photo.ToScreen(0, 0);
        for (int y = 0; y < h; y++)
        {
            float fy = Math.Clamp((y - top.Y) / _photo.P, 0, 1);        // 0 top of the photo ... 1 bottom
            float light = 0.66f - 0.30f * fy;                           // how much light is left
            // The sky at dusk: deep blue overhead, paling to a dusty rose
            // glow toward the horizon.
            float toHorizon = Math.Clamp(fy / _look.Horizon, 0, 1);
            toHorizon *= toHorizon * (3 - 2 * toHorizon);
            for (int x = 0; x < w; x++)
            {
                uint c = src[y * w + x];
                float r = (c >> 16) & 0xFF, g = (c >> 8) & 0xFF, b = c & 0xFF;
                float lum = 0.30f * r + 0.59f * g + 0.11f * b;
                // How surely this pixel is sky: clearly bluer than it is red,
                // and bright. Twigs and leaves against the sky come out part
                // sky, which is what lets the dusk sky show between them.
                float sky = Math.Clamp((b - r - 15) / 45f, 0, 1) * Math.Clamp((lum - 100) / 50f, 0, 1);
                // Water is not sky, though it mirrors it: it takes the
                // overhead blue (not the rose of the horizon) at half strength.
                bool isWater = _photo.Water[y * w + x] > 0;
                if (isWater) sky *= 0.5f;
                // Keep a quarter of the colour, so warm things stay a little warm.
                r = lum + 0.25f * (r - lum);
                g = lum + 0.25f * (g - lum);
                b = lum + 0.25f * (b - lum);
                // The blue tint, then the dimming. Brights are pulled down a
                // little harder than darks (the "* (1 - lum/600)"), so sunlit
                // walls do not still glow as if the sun were out.
                float k = light * (1 - lum / 600f);
                // Leaves and grass go darker still: against a dusk sky a tree
                // is close to a silhouette, not a grey-blue cut-out.
                if (g > r) k *= Math.Max(0.55f, 1 - (g - r) / 40f);
                r *= 0.62f * k; g *= 0.74f * k; b *= 1.05f * k;
                if (sky > 0)
                {
                    // The sky's own texture (wisps of cloud) is kept as a
                    // little lighter or darker than its neighbours. The
                    // afterglow is warmer on the left, where the sun set.
                    float tex = 0.8f + 0.4f * (lum - 150) / 105f;
                    float west = 1 - x / (float)w;
                    float glow = isWater ? 0.2f * toHorizon : toHorizon;
                    float sr = (26 + 120 * glow + 30 * west * glow) * tex;
                    float sg = (34 + 78 * glow) * tex;
                    float sb = (74 + 46 * glow) * tex;
                    r += (sr - r) * sky; g += (sg - g) * sky; b += (sb - b) * sky;
                }
                dst[y * w + x] = (uint)(((int)Math.Min(255, r) << 16) | ((int)Math.Min(255, g) << 8) | (int)Math.Min(255, b));
            }
        }
        return dst;
    }
}

using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Photo;

namespace CotswoldBrook;

/// <summary>
/// A real photograph of stone cottages beside a brook at golden hour, brought
/// to life. The other savers paint their whole picture with code; no code
/// paints honey-coloured stone and a thousand leaves as well as a camera
/// does, so this one starts from a photo and only animates what moves:
///
///   - The WATER (Core/Photo/WaterRipples.cs): ripples run through the
///     reflections, and now and then a trout rises and leaves spreading rings.
///   - The TROUT (Trout.cs): dark shapes just under the surface, holding
///     against the current, darting now and then.
///   - The SMOKE (Core/ChimneySmoke.cs): a thin plume from one chimney,
///     warm-tinted by the low sun.
///   - The EVENING (Core/Photo/PhotoEvening.cs): over several minutes the
///     gold drains out of the light and dusk falls; windows light up and
///     fireflies come out over the reeds. After a while at dusk the gold
///     slowly returns, and around it goes again.
///   - The MIST: a still sheet lying on the far water, painted into the
///     backdrop once (PhotoEvening.LayMist), with soft banks drifting
///     through it (Core/Photo/PhotoMist.cs). It thickens as the evening cools.
///
/// THE DEPTH MAP (brook_depth.png, made by scripts/depthmap.py): a grey
/// copy of the photo where white is near and black is far. It is what puts
/// the mist and the fireflies IN the picture rather than on the glass: the
/// near reeds stand in front of the mist, and a firefly deep in the reed
/// bed slips behind the blades nearer to us than it is.
///
/// What this file holds is the knowledge of THIS photo: where its water,
/// chimney and windows are, all measured by eye on a grid laid over it.
/// </summary>
internal sealed class CotswoldBrookScene : IScreensaverScene
{
    private readonly Random _rng = HappeningDirector.SceneRandom();
    private readonly PhotoBackdrop _photo;
    private readonly WaterRipples _water;
    private readonly Trout _trout;
    private readonly ChimneySmoke _smoke;
    private readonly PhotoEvening _evening;
    private readonly PhotoMist _mist;

    // How near the reed tips along the near bank are on the depth map (0 far
    // to 255 near; read off the map: the far water is about 50, the near
    // water 70 to 95, the reed tips 100 to 110). Mist shows only on scenery
    // farther than this, so the near reeds stay crisp in front of it.
    private const int MistNearest = 98;

    // THE WATER'S OUTLINE, as fractions of the photo (x, y), going round it
    // clockwise from the far left bank. It follows the reeds' tips on the near
    // side and the stone wall, the road's edge and the bridge on the far side.
    private static readonly PointF[] WaterOutline =
    [
        new(0.00f, 0.722f), new(0.08f, 0.722f), new(0.18f, 0.715f), new(0.28f, 0.700f),
        new(0.38f, 0.690f), new(0.425f, 0.665f), new(0.44f, 0.628f), new(0.47f, 0.614f), new(0.55f, 0.612f),
        new(0.61f, 0.600f), new(0.66f, 0.598f), new(0.72f, 0.602f), new(0.79f, 0.604f),
        new(0.86f, 0.608f), new(0.905f, 0.615f),
        new(0.905f, 0.635f), new(0.86f, 0.645f), new(0.82f, 0.660f), new(0.81f, 0.700f),
        new(0.79f, 0.735f), new(0.72f, 0.745f), new(0.62f, 0.748f), new(0.55f, 0.755f),
        new(0.50f, 0.775f), new(0.46f, 0.790f), new(0.38f, 0.800f), new(0.28f, 0.815f),
        new(0.18f, 0.835f), new(0.09f, 0.865f), new(0.00f, 0.880f),
    ];

    // The bridge's two stone piers stand in the water.
    private static readonly PointF[][] Piers =
    [
        PhotoBackdrop.Box(0.725f, 0.590f, 0.035f, 0.050f),
        PhotoBackdrop.Box(0.790f, 0.590f, 0.030f, 0.050f),
    ];

    // The top of the chimney that smokes.
    private static readonly PointF SmokingChimney = new(0.201f, 0.068f);

    // The windows, as boxes on the photo (left, top, width, height). Dormers
    // in the roof first, then the ground floor, then the cottage across the green.
    private static readonly RectangleF[] WindowBoxes =
    [
        new(0.004f, 0.152f, 0.034f, 0.047f), new(0.186f, 0.232f, 0.023f, 0.038f),
        new(0.292f, 0.260f, 0.016f, 0.037f), new(0.389f, 0.250f, 0.013f, 0.040f),
        new(0.420f, 0.261f, 0.011f, 0.038f), new(0.449f, 0.270f, 0.010f, 0.036f),
        new(0.006f, 0.302f, 0.057f, 0.059f), new(0.189f, 0.346f, 0.031f, 0.030f),
        new(0.296f, 0.370f, 0.023f, 0.035f), new(0.394f, 0.358f, 0.013f, 0.024f),
        new(0.452f, 0.369f, 0.012f, 0.035f), new(0.196f, 0.465f, 0.023f, 0.040f),
        new(0.813f, 0.470f, 0.011f, 0.025f),
    ];

    // Where the fireflies live, near to far: the reeds in front; the water
    // and the reeds along it; the green across the brook by the cottages.
    private static readonly FireflyZone[] FireflyZones =
    [
        new(new RectangleF(0.00f, 0.800f, 1.00f, 0.140f), Depth: 1.00f, Share: 3),
        new(new RectangleF(0.00f, 0.640f, 1.00f, 0.120f), Depth: 0.65f, Share: 2),
        new(new RectangleF(0.50f, 0.520f, 0.45f, 0.070f), Depth: 0.35f, Share: 1),
    ];

    public CotswoldBrookScene(int width, int height, CotswoldBrookSettings settings)
    {
        using (Stream file = Resource("brook.jpg"))
        using (Stream depth = Resource("brook_depth.png"))
        {
            // On a portrait screen the sides are trimmed a little more from
            // the right (focusX 0.45), so the cottages and the brook stay in
            // and what goes is mostly hedge.
            _photo = new PhotoBackdrop(width, height, file, WaterOutline, Piers, focusX: 0.45f, depthFile: depth);
        }
        float p = _photo.P;
        _water = new WaterRipples(_photo, WaterLook.Brook, settings.RipplePercent / 100f, settings.RisePercent / 100f, _rng);
        _trout = new Trout(_photo, settings.TroutCount, _rng);

        // The smoke is sized by the photo, not the screen: "u" here is a
        // fraction of the photo's height, tuned so the plume is about a
        // chimney stack wide where it leaves the pot.
        PointF chimney = _photo.ToScreen(SmokingChimney.X, SmokingChimney.Y);
        _smoke = new ChimneySmoke(chimney, p * 0.9f, settings.SmokePercent / 100f, 1.0f, _rng,
            Color.FromArgb(232, 220, 204));     // pale, warmed a little by the low sun

        _evening = new PhotoEvening(_photo, EveningLook.SunlitStone, WindowBoxes, FireflyZones,
            settings.FireflyPercent / 100f, settings.EveningMinutes, _rng);

        // Mist: a cool pale sheet lying on the water, plus four soft banks
        // drifting through it so it never sits still (the sheet does the main
        // work, so the banks can be few: at 4K each one is a few hundred
        // thousand pixels to blend, every frame). Shown only beyond the reeds.
        float mistAmount = settings.MistPercent / 100f;
        bool[] beyondReeds = _photo.Depth!.Select(d => d <= MistNearest).ToArray();
        Color mistColour = Color.FromArgb(222, 228, 236);
        _evening.LayMist(MistSheet(mistAmount), mistColour, golden: 0.45f, dusk: 0.95f);
        _mist = new PhotoMist(_photo, new RectangleF(0f, 0.60f, 1f, 0.13f), beyondReeds, 0.7f * mistAmount, _rng,
            mistColour, size: 2.0f, stretch: 1.8f);
    }

    /// <summary>
    /// How much mist each screen pixel always has (0 to 255). Mist gathers
    /// over the far water and thins toward us, so it comes from the depth
    /// map: thickest at the far water (about 55 on the map), fading to none
    /// at the reed tips (MistNearest). It lies LOW: none above the foot of the
    /// cottages (36% from the bottom of the photo), rising over a short band
    /// so it has no hard top edge.
    /// </summary>
    private byte[] MistSheet(float amount)
    {
        const int Far = 55;
        int w = _photo.Width, h = _photo.Height;
        float top = _photo.ToScreen(0, 0.565f).Y, full = _photo.ToScreen(0, 0.60f).Y;
        var sheet = new byte[w * h];
        for (int y = 0; y < h; y++)
        {
            float rise = Math.Clamp((y - top) / Math.Max(1f, full - top), 0f, 1f);
            if (rise <= 0) continue;
            rise = rise * rise * (3 - 2 * rise);                  // smoothstep: eases in, no crease where it starts
            for (int x = 0; x < w; x++)
            {
                int d = _photo.Depth![y * w + x];
                float far = Math.Clamp((MistNearest - d) / (float)(MistNearest - Far), 0f, 1f);
                sheet[y * w + x] = (byte)Math.Min(255f, 70f * amount * far * rise);
            }
        }
        return sheet;
    }

    private static Stream Resource(string name) =>
        typeof(CotswoldBrookScene).Assembly.GetManifestResourceStream("CotswoldBrook." + name)
        ?? throw new InvalidOperationException($"{name} is missing from the program (it is not embedded).");

    public void Update(double elapsedSeconds)
    {
        _water.Update(elapsedSeconds);
        _trout.Update(elapsedSeconds);
        _smoke.Update((float)elapsedSeconds);
        _evening.Update(elapsedSeconds);
        _mist.Update(elapsedSeconds);
    }

    public void Render(FrameBuffer fb)
    {
        float dusk = _evening.Dusk;

        // 1. The photo, at the current hour.
        Array.Copy(_evening.Backdrop, fb.Pixels, fb.Pixels.Length);

        // 2. The water, rippled.
        _water.Draw(fb, _evening.Backdrop);

        // 3. The trout, just under the surface. At dusk there is less light to
        //    see into the water, so they fade (but never quite vanish).
        _trout.Draw(fb, 1 - 0.7f * dusk);

        // 4. The mist's drifting banks. (Its still sheet is already painted
        //    into the backdrop by LayMist.) Evening mist gathers as the air
        //    cools, so it is faint in the golden light and fuller at dusk.
        _mist.Draw(fb, 0.45f + 0.5f * dusk);

        // 5. The smoke. At dusk there is less light to show it, so it fades to a third.
        _smoke.Draw(fb, 0.5f * (1 - 0.65f * dusk));   // half strength: a summer evening fire, banked low

        // 6. Lamps in the windows, then 7. fireflies.
        _evening.DrawWindows(fb);
        _evening.DrawFireflies(fb);
    }

    public void Dispose() { }
}

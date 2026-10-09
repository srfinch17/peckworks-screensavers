namespace Peckworks.Screensavers.Core.Photo;

/// <summary>
/// A place on the photo where fireflies live, and how far away it is.
/// </summary>
/// <param name="Box">The area, as fractions of the photo (left, top, width, height).</param>
/// <param name="Depth">How near it is: 1 = the nearest part of the picture, 0.3 = far back. Near fireflies are bigger and faster.</param>
/// <param name="Share">How many of the fireflies live here, relative to the other places.</param>
public sealed record FireflyZone(RectangleF Box, float Depth, float Share = 1f);

/// <summary>
/// Fireflies as little insects in the picture's own space, not blinking
/// stickers.
///
/// WHAT REAL FIREFLIES DO: each is a small dark beetle drifting over the
/// grass on a lazy, wandering path; every few seconds it flashes, a quick
/// swell of yellow-green light that fades over a second, and goes dark
/// again while it keeps flying. Between flashes you can just make out the
/// faint glow of its tail.
///
/// THE DEPTH: the picture has a near garden and a far riverbank. A firefly
/// is given a depth from its place (FireflyZone): a near one is drawn
/// bigger and crosses the screen faster (the same real speed covers more
/// pixels close up), a far one is a pinprick drifting slowly. That is what
/// makes them sit IN the scene instead of on the glass in front of it.
///
/// HOW ONE FLIES: it has a heading (a direction) that keeps turning a
/// little, by an amount that rises and falls on two slow waves of its own;
/// so its path curves this way and that and never repeats. If it wanders
/// out of its place it turns back toward the middle of it.
///
/// AMONG THE REEDS: when the photo has a depth map (PhotoBackdrop.Depth),
/// each firefly also gets a real distance, and is hidden wherever the
/// scenery is nearer than that. Its distance is picked from the distances
/// of the scenery in its own place: most are as near as the nearer part of
/// it (so they fly in front of most of the reeds), a few sit deeper in (so
/// they slip behind blades as they go). A rectangle on the screen is not a
/// slab of the same distance (the bottom of a reed bed is much nearer than
/// its top), which is why the distance comes from the map and not the zone.
/// </summary>
public sealed class PhotoFireflies
{
    private struct Fly
    {
        public float X, Y, Heading, Speed, Depth;
        public float TurnA, TurnB, PhaseA, PhaseB;     // the two waves that steer it
        public int Zone, Size;
        public byte Nearness;                          // how near it is, 0 to 255, on the depth map's scale
        public double FlashAt, FlashLength;            // when the next flash starts, and how long it lasts
    }

    private const int Sizes = 3;
    private readonly Fly[] _flies;
    private readonly RectangleF[] _zones;              // in screen pixels
    private readonly Sprite[] _core = new Sprite[Sizes], _halo = new Sprite[Sizes], _tail = new Sprite[Sizes];
    private readonly Random _rng;
    private readonly byte[]? _depth;
    private readonly int _screenW, _screenH;
    private readonly float _u;
    private double _time;

    /// <param name="amount">1 = about eighteen on a 16:9 screen, 0 = none.</param>
    public PhotoFireflies(PhotoBackdrop photo, FireflyZone[] zones, float amount, Random rng)
    {
        _rng = rng;
        _depth = photo.Depth;
        _screenW = photo.Width;
        _screenH = photo.Height;
        // Sized by the SCREEN (u, as in the painted savers), not the photo: a
        // tall photo shown in a band is far taller than the screen.
        _u = Math.Min(photo.Height, photo.Width * 9f / 16f);
        _zones = zones.Select(z =>
        {
            PointF a = photo.ToScreen(z.Box.Left, z.Box.Top), b = photo.ToScreen(z.Box.Right, z.Box.Bottom);
            return new RectangleF(a.X, a.Y, b.X - a.X, b.Y - a.Y);
        }).ToArray();

        // Three sizes of light, far to near. The core is the bright spark; the
        // halo is the soft glow it throws into the air; the tail is the faint
        // light it keeps between flashes.
        for (int s = 0; s < Sizes; s++)
        {
            float k = 0.45f + 0.55f * s / (Sizes - 1);
            _core[s] = Sprite.Glow(Math.Max(2, (int)(_u * 0.0045f * k)), Color.FromArgb(205, 255, 110));
            _halo[s] = Sprite.Glow(Math.Max(5, (int)(_u * 0.026f * k)), Color.FromArgb(150, 255, 70));
            _tail[s] = Sprite.Glow(Math.Max(2, (int)(_u * 0.0038f * k)), Color.FromArgb(160, 215, 95));
        }

        // Each zone's scenery distances, nearest last, to pick each firefly's distance from.
        byte[][] zoneDepths = _zones.Select(DepthsIn).ToArray();

        int count = Math.Max(0, (int)Math.Round(18 * amount * photo.Width / (_u * 16f / 9f)));
        float totalShare = zones.Sum(z => z.Share);
        _flies = new Fly[zones.Length == 0 ? 0 : count];
        for (int i = 0; i < _flies.Length; i++)
        {
            // Pick a zone by its share, then a spot in it.
            float pick = (float)rng.NextDouble() * totalShare;
            int zi = 0;
            while (zi < zones.Length - 1 && pick > zones[zi].Share) { pick -= zones[zi].Share; zi++; }
            RectangleF box = _zones[zi];
            float depth = Math.Clamp(zones[zi].Depth + ((float)rng.NextDouble() - 0.5f) * 0.15f, 0.15f, 1f);
            _flies[i] = new Fly
            {
                X = box.X + box.Width * (float)rng.NextDouble(),
                Y = box.Y + box.Height * (float)rng.NextDouble(),
                Heading = (float)(rng.NextDouble() * Math.Tau),
                Speed = _u * (0.010f + 0.012f * (float)rng.NextDouble()) * depth,   // pixels per second: near ones cross faster
                Depth = depth,
                TurnA = 0.6f + 0.8f * (float)rng.NextDouble(), TurnB = 1.7f + 1.5f * (float)rng.NextDouble(),
                PhaseA = (float)(rng.NextDouble() * Math.Tau), PhaseB = (float)(rng.NextDouble() * Math.Tau),
                Zone = zi,
                Size = Math.Clamp((int)(depth * Sizes), 0, Sizes - 1),
                FlashAt = rng.NextDouble() * 6,
                FlashLength = 0.8 + 0.5 * rng.NextDouble(),
                // As near as the scenery at 60% to 97% of its place: in front of
                // most of it, behind its nearest bits. No depth map: in front of all.
                Nearness = zoneDepths[zi].Length == 0 ? (byte)255
                    : zoneDepths[zi][(int)((0.60 + 0.37 * rng.NextDouble()) * (zoneDepths[zi].Length - 1))],
            };
        }
    }

    /// <summary>
    /// The depth map's values inside a box on the screen, sorted far to near
    /// (every fourth pixel each way is plenty). Empty with no depth map, or
    /// when the box is off the screen.
    /// </summary>
    private byte[] DepthsIn(RectangleF box)
    {
        if (_depth == null) return [];
        var found = new List<byte>();
        int x0 = Math.Max(0, (int)box.Left), x1 = Math.Min(_screenW, (int)box.Right);
        int y0 = Math.Max(0, (int)box.Top), y1 = Math.Min(_screenH, (int)box.Bottom);
        for (int y = y0; y < y1; y += 4)
            for (int x = x0; x < x1; x += 4)
                found.Add(_depth[y * _screenW + x]);
        found.Sort();
        return found.ToArray();
    }

    public void Update(double dt)
    {
        _time += dt;
        float t = (float)(_time % 10000.0), step = (float)dt;
        for (int i = 0; i < _flies.Length; i++)
        {
            ref Fly f = ref _flies[i];
            // Steering: the heading turns by an amount that swings both ways.
            f.Heading += (1.1f * MathF.Sin(t * f.TurnA + f.PhaseA) + 0.6f * MathF.Sin(t * f.TurnB + f.PhaseB)) * step;
            // Strayed out of its place? Turn back toward the middle of it.
            RectangleF box = _zones[f.Zone];
            if (!box.Contains(f.X, f.Y))
            {
                float home = MathF.Atan2(box.Y + box.Height / 2 - f.Y, box.X + box.Width / 2 - f.X);
                float diff = MathF.IEEERemainder(home - f.Heading, MathF.Tau);
                f.Heading += Math.Clamp(diff, -2.5f * step, 2.5f * step);
            }
            f.Heading %= MathF.Tau;
            f.X += MathF.Cos(f.Heading) * f.Speed * step;
            // Seen from the side at a low angle, up-and-down motion looks
            // smaller than side-to-side: flatten the vertical part.
            f.Y += MathF.Sin(f.Heading) * f.Speed * 0.45f * step;

            // A flash finished: schedule the next one, two to seven seconds on.
            if (_time > f.FlashAt + f.FlashLength)
            {
                f.FlashAt = _time + 2 + 5 * _rng.NextDouble();
                f.FlashLength = 0.8 + 0.5 * _rng.NextDouble();
            }
        }
    }

    /// <param name="light">0 = none yet (still daylight) ... 1 = full dusk.</param>
    public void Draw(FrameBuffer fb, float light)
    {
        if (light <= 0) return;
        foreach (ref readonly Fly f in _flies.AsSpan())
        {
            float x = MathF.Round(f.X), y = MathF.Round(f.Y);
            // The faint tail glow it keeps between flashes: just enough to follow it.
            _tail[f.Size].DrawCentered(fb, x, y, 0.6f * light, depth: _depth, nearness: f.Nearness);

            // The flash: a quick swell (a fifth of the flash) and a slower fade.
            double since = _time - f.FlashAt;
            if (since < 0 || since > f.FlashLength) continue;
            float q = (float)(since / f.FlashLength);
            float b = (q < 0.2f ? q / 0.2f : MathF.Pow(1 - (q - 0.2f) / 0.8f, 1.8f)) * light;
            if (b < 0.02f) continue;
            _halo[f.Size].DrawCentered(fb, x, y, 0.75f * b, depth: _depth, nearness: f.Nearness);
            _core[f.Size].DrawCentered(fb, x, y, b, depth: _depth, nearness: f.Nearness);
        }
    }
}

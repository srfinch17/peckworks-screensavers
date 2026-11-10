using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;
using Peckworks.Screensavers.Core.Sakura;
using Christmas.Happenings;
using Christmas.Stories;

namespace Christmas;

/// <summary>
/// Everything a story may need to reach: the scenery's facts, the
/// noticeboard, Santa, and each resident (so a story can BORROW one: the
/// mice for the tree trimming, the snowman for the carrot heist). One bag,
/// handed to every story's constructor.
/// </summary>
internal sealed class ChristmasWorld
{
    public required ChristmasScenery Scenery { get; init; }
    public required Noticeboard Board { get; init; }
    public required SantaRig Santa { get; init; }
    public required ReindeerHerd Reindeer { get; init; }
    public required FoxAndHares Field { get; init; }
    public required YardSnowman Snowman { get; init; }
    public required FeederBirds Birds { get; init; }
    public required LogMice Mice { get; init; }
    public required Otters Otters { get; init; }
}

/// <summary>
/// Christmas Eve at a cabin in the north woods: snow falling, pastel lights
/// twinkling in the pines, a village glowing down in the valley, Santa's
/// sleigh crossing the sky with Rudolph out front, and a snowfield full of
/// animals getting on with their night.
///
/// The same theater as Sakura and Thanksgiving: a BACKDROP painted once
/// (ChristmasPainter.cs) and ACTORS drawn over it every frame:
///
///   - The SNOW is the shared petal engine (Core/Sakura/PetalField.cs) told
///     to draw soft white dots instead of petals. Same fall, sway and depth.
///   - The LIGHTS: the backdrop has every bulb painted as a small dull dot.
///     Each frame a soft glow of the bulb's color is stamped over it,
///     stronger or weaker on each bulb's own slow rhythm: the twinkle. The
///     brighter stars, the village's windows, the cabin's windows, the lamp
///     post and the glitter on the snow are the same trick.
///   - The SMOKE from the cabin's chimney (Core/ChimneySmoke.cs).
///   - SANTA (SantaRig.cs): a flip-book of the sleigh sliding across the
///     sky, behind the corner branches.
///   - The RESIDENTS (Resident.cs): the reindeer, the fox and the hares,
///     the snowman, the birds at the table, the mice in the log, the otters
///     on the pond. Each has a mind (Core/Minds) and reads the NOTICEBOARD,
///     where everything that happens is pinned, so they react to each
///     other, to the happenings and to Santa going over. They are drawn far
///     to near through one queue (DepthQueue), each hidden by the props
///     nearer than its feet.
///   - The STORIES (Stories folder): little scenes of half a minute, dealt
///     one every minute or so by the engine's StoryDirector.
///   - The HAPPENINGS: small surprises of a few seconds (fireworks, the
///     northern lights, an owl), dealt by the engine's HappeningDirector.
/// </summary>
internal sealed class ChristmasScene : IScreensaverScene
{
    /// <summary>One bulb's twinkle: where the bulb is, which color, and its own rhythm.</summary>
    private struct Light
    {
        public float X, Y;
        public int Color;
        public float Phase, Speed;
    }

    /// <summary>The residents do nothing before this many seconds: their flip-books are painting on a helper thread, and a start pinned to a time makes every test run begin the same way.</summary>
    private const double ResidentsStart = 6.0;

    private readonly Random _rng = HappeningDirector.SceneRandom();   // new dice every launch, unless PECKWORKS_SEED asks for a repeat
    private readonly ChristmasScenery _scenery;
    private readonly PetalField _snow;
    private readonly Light[] _lights;
    private readonly Sprite[] _bulbGlows;           // one soft glow per pastel color
    private readonly SantaRig _santa;
    private readonly HappeningDirector _happenings;
    private readonly ChimneySmoke _smoke;           // the cabin's chimney, live (Core/ChimneySmoke.cs)
    private readonly Sprite _starGlow, _villageGlow, _windowGlow, _lampGlow, _lampHalo, _glint;
    private readonly float[] _starPhase, _starSpeed, _villagePhase;
    private readonly (float X, float Y, float Phase, float Speed)[] _glitter;   // the snow's sparkles: where, and each one's own rhythm
    private readonly Noticeboard _board;            // what is going on, for the residents' senses (Core/Minds/Senses.cs)
    private readonly Resident[] _residents;
    private readonly DepthQueue _queue = new();     // draws the residents far to near (Core/Stories.cs)
    private readonly StoryDirector _stories;
    private readonly (Sense Kind, float X, float Y, float Reach, float At)? _testNotice;
    private readonly float _u;
    private readonly int _w, _h;
    // A "double" (a decimal number with about 15 digits of precision), not a
    // "float" (about 7 digits). A screensaver can run for days. A float clock
    // that large can no longer register a 16 millisecond step, and every
    // bulb would freeze.
    private double _time;
    private double _santaTold;                      // when Santa was last pinned on the noticeboard
    // Test aid: PECKWORKS_NOSNOW=1 leaves the falling snow out, so a frame-by-frame comparison (the kit's pop detector)
    // sees only what the animals do, not eight hundred flakes changing every pixel.
    private readonly bool _noSnow = Environment.GetEnvironmentVariable("PECKWORKS_NOSNOW") == "1";
    private bool _started;                          // true once every resident's flip-books are painted: they all begin on that frame

    public ChristmasScene(int width, int height, ChristmasSettings settings)
    {
        _w = width;
        _h = height;
        _scenery = ChristmasPainter.Paint(width, height, _rng);
        _u = Math.Min(height, width * 9f / 16f);   // the size unit: height, or less on a tall screen

        // ---- Snow: the petal engine drawing soft white dots ----
        // Snowflakes are small, so there are more of them than petals.
        _snow = new PetalField(width, height,
            PetalField.CountFor(width, height, settings.DensityPercent, _u) * 2,
            settings.FallSpeedPercent / 100f, settings.WindPercent / 100f, settings.FlakeSizePercent / 100f,
            [], _rng, sizeUnit: _u)
        {
            Shape = PetalField.FlakeShape.Snow,
            Colors = (Color.White, Color.FromArgb(222, 234, 255)),   // white, through to a faintly blue white
        };

        // ---- Lights: every bulb gets its own rhythm, so they do not blink in step ----
        _bulbGlows = ChristmasPainter.Pastels.Select(c => Sprite.Glow((int)(_u * 0.012f), c)).ToArray();
        _lights = _scenery.Lights.Select(l => new Light
        {
            X = l.At.X, Y = l.At.Y, Color = l.Color,
            Phase = (float)(_rng.NextDouble() * Math.PI * 2),
            Speed = 1.0f + 2.6f * (float)_rng.NextDouble(),
        }).ToArray();

        _santa = new SantaRig(_scenery);

        // ---- The happenings: hand the director the cast list and how often to deal ----
        _happenings = new HappeningDirector(ChristmasHappenings.Cast(_scenery), _rng, settings.SurprisePercent / 100f);

        // ---- The small live lights. Everything from here on takes its dice from "amb", a
        //      second set seeded by one roll of the scene's, so adding a sparkle later never
        //      shifts what the director and the snow rolled above. ----
        var amb = new Random(_rng.Next());
        _smoke = new ChimneySmoke(_scenery.ChimneyTop, _u, 0.8f, 0.6f, amb);
        _starGlow = Sprite.Glow(Math.Max(2, (int)(_u * 0.006f)), Color.FromArgb(236, 244, 255));
        _villageGlow = Sprite.Glow(Math.Max(2, (int)(_u * 0.007f)), Color.FromArgb(255, 200, 110));
        _windowGlow = Sprite.Glow(Math.Max(3, (int)(_u * 0.030f)), Color.FromArgb(255, 196, 100));
        _lampGlow = Sprite.Glow(Math.Max(3, (int)(_u * 0.030f)), Color.FromArgb(255, 220, 150));
        _lampHalo = Sprite.Glow(Math.Max(6, (int)(_u * 0.12f)), Color.FromArgb(255, 204, 130));
        _glint = SantaRig.PaintSparkle(Math.Max(5, (int)(_u * 0.012f)));
        _starPhase = new float[_scenery.Stars.Count];
        _starSpeed = new float[_scenery.Stars.Count];
        for (int i = 0; i < _starPhase.Length; i++)
        {
            _starPhase[i] = (float)(amb.NextDouble() * Math.PI * 2);
            _starSpeed[i] = 0.6f + 1.8f * (float)amb.NextDouble();
        }
        _villagePhase = new float[_scenery.VillageWindows.Count];
        for (int i = 0; i < _villagePhase.Length; i++) _villagePhase[i] = (float)(amb.NextDouble() * 40);

        // The snow's glitter: points on the open snowfield (not on a prop, not on the ice),
        // each flashing briefly on its own slow rhythm, as moonlit snow does when you move your head.
        var glitter = new List<(float, float, float, float)>();
        for (int tries = 0; tries < 900 && glitter.Count < 150; tries++)
        {
            float gx = (float)amb.NextDouble() * width;
            PointF p = _scenery.FieldPoint(gx, (float)amb.NextDouble());
            int px = (int)p.X, py = (int)p.Y;
            if (px < 0 || px >= width || py < 0 || py >= height) continue;
            if (_scenery.NearDepth[py * width + px] != 0 || _scenery.OnPond(p.X, p.Y, 1.1f)) continue;
            glitter.Add((p.X, p.Y, (float)(amb.NextDouble() * Math.PI * 2), 0.25f + 0.5f * (float)amb.NextDouble()));
        }
        _glitter = [.. glitter];

        // ---- The noticeboard: whatever happens is pinned here, and every resident reads it ----
        // The board is asked for CALM (Core/Minds/Senses.cs): a glance is followed by a rest of about a quarter of
        // a minute, the same source is not interesting again for three quarters of a minute, and the whole field
        // may begin two glances in any ten seconds. Fear is never rationed.
        _board = new Noticeboard { LookRest = 14f, Habituate = 45f, Looks = 2, Window = 10f };
        _happenings.Began = Tell;

        // ---- The residents. Each takes ONE roll of the scene's dice for its own set, in this
        //      order; a new resident goes at the END of the list so nobody else's luck moves. ----
        var reindeer = new ReindeerHerd(_scenery, settings.ReindeerCount, _rng, _board);
        var field = new FoxAndHares(_scenery, settings.HareCount, _rng, _board);
        var snowman = new YardSnowman(_scenery, 1, _rng, _board);
        var birds = new FeederBirds(_scenery, settings.BirdCount, _rng, _board);
        var mice = new LogMice(_scenery, settings.MouseCount, _rng, _board);
        var otters = new Otters(_scenery, settings.OtterCount, _rng, _board);
        _residents = [reindeer, field, snowman, birds, mice, otters];

        // ---- The stories ----
        var world = new ChristmasWorld
        {
            Scenery = _scenery, Board = _board, Santa = _santa,
            Reindeer = reindeer, Field = field, Snowman = snowman, Birds = birds, Mice = mice, Otters = otters,
        };
        _stories = new StoryDirector(_happenings, _rng,
            [new SantaDelivery(world), new MiceTrimTree(world), new CarrotHeist(world)], settings.StoryPercent / 100f);

        // Test aid: PECKWORKS_NOTICE="Loud;0.5;0.8;0.8;6" pins that kind of notice at (x, y as
        // fractions of the screen) with a reach in U at that second, to watch everybody react.
        string? notice = Environment.GetEnvironmentVariable("PECKWORKS_NOTICE");
        if (!string.IsNullOrEmpty(notice))
        {
            string[] p = notice.Split(';');
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            _testNotice = (Enum.Parse<Sense>(p[0], true), float.Parse(p[1], inv), float.Parse(p[2], inv), float.Parse(p[3], inv), float.Parse(p[4], inv));
        }
    }

    // ==================================================================
    //  UPDATE AND RENDER
    // ==================================================================

    public void Update(double elapsedSeconds)
    {
        float dt = (float)elapsedSeconds;
        _time += dt;
        _snow.Update(dt);
        _smoke.Update(dt);
        _santa.Update(dt);
        _board.Tick(dt);

        // Santa going over is something to look up at: pinned once a second
        // while he is in view (a notice lasts one second), high in the sky
        // with a long reach and little weight, so it is a glance, not a fright.
        if (_scenery.SantaOnScreen && _time - _santaTold >= 2.5)
        {
            _santaTold = _time;
            _board.Post(Sense.Movement, _scenery.SantaSleigh, 1.6f * _u, 0.35f, _santa);
        }
        if (_testNotice is { } tn && _time >= tn.At && _time - dt < tn.At)
            _board.Post(tn.Kind, new PointF(tn.X * _w, tn.Y * _h), tn.Reach * _u, 1f, this);

        // ONE START FOR EVERYBODY: nobody begins until every resident's flip-books are painted. (At first each
        // began when its own were done: the mice at 4 s, the herd at 15 to 18, each popping into the scene fully
        // formed, and the same seed played differently from run to run.) Each resident then makes its own
        // entrance: the herd walks in, the birds fly in, the hares come up out of their burrows.
        // The start is PINNED to a time, not to "whenever the painting happens to finish": at ResidentsStart
        // seconds the scene simply waits for any resident still painting (a pause of a moment, once, on a slow
        // machine; none on a normal one, where the painting takes two or three seconds). A start that followed
        // the painting moved by a second from run to run, so the same seed played a different film each time
        // and no test could be repeated.
        if (!_started && _time >= ResidentsStart)
        {
            var waited = System.Diagnostics.Stopwatch.StartNew();
            while (waited.ElapsedMilliseconds < 30000)
            {
                bool all = true;
                foreach (Resident r in _residents) if (!r.Ready) { all = false; break; }
                if (all) { _started = true; break; }
                Thread.Sleep(5);
            }
            if (!_started) { _started = true; }     // half a minute and still not painted: something failed; carry on with whoever is ready
        }
        if (_started) _stories.Update(dt);          // before the residents: a story may borrow one this very frame
        if (_started)
        {
            _scenery.TurnPage();                    // last frame's "who stands where" becomes the sheet to read
            foreach (Resident r in _residents) if (r.Ready) { r.Lull = _stories.Playing; r.Update(dt); }
        }
        _happenings.Update(elapsedSeconds);
    }

    /// <summary>
    /// Tells the residents a surprise has begun: each happening that an
    /// animal could notice is pinned on the board as a kind, a place and a
    /// reach (fireworks are loud and carry across the whole field; an owl on
    /// a branch is a threat to the small things; a snowball rolling by is a
    /// movement to glance at). The animals' own senses decide what they make of it.
    /// </summary>
    private void Tell(Happening h)
    {
        ChristmasScenery s = _scenery;
        float u = s.U;
        var mid = new PointF(s.Width * 0.5f, s.WalkY(s.Width * 0.5f));
        switch (h.GetType().Name)
        {
            case "Fireworks": _board.Post(Sense.Loud, new PointF(s.Width * 0.5f, s.Height * 0.25f), 2.0f * u, 0.75f, h); break;
            case "SnowyOwl": _board.Post(Sense.Threat, new PointF(s.Width * 0.5f, s.Height * 0.3f), 1.2f * u, 0.6f, h); break;
            case "ToyTrain": _board.Post(Sense.Loud, new PointF(s.Width * 0.5f, s.Horizon), 1.0f * u, 0.3f, h); break;
            case "SnowSlide": _board.Post(Sense.Loud, new PointF(s.Pines[s.StarPine].X, s.Pines[s.StarPine].Foot), 0.6f * u, 0.5f, h); break;
            case "RollingSnowball": _board.Post(Sense.Movement, mid, 0.8f * u, 0.7f, h); break;
            case "SnowGust": _board.Post(Sense.Movement, mid, 1.2f * u, 0.5f, h); break;
            case "PresentDrop": _board.Post(Sense.Movement, s.SantaSleigh, 1.6f * u, 0.6f, h); break;
            case "ChimneyRings": _board.Post(Sense.Movement, s.ChimneyTop, 0.5f * u, 0.4f, h); break;
        }
    }

    public void Render(FrameBuffer fb)
    {
        // 1. The backdrop. Array.Copy is one fast block copy of every pixel.
        fb.Restore(_scenery.Pixels);

        // 2. The FAR happenings (layer 0): the ones in the sky and on the
        //    mountains, before Santa so a firework or the northern lights
        //    are behind him.
        _happenings.Draw(fb, 0);

        // 2b. The stars twinkle, through the sky stencil so a star behind a
        //     branch never shines through it. sin() swings between -1 and 1;
        //     squaring the "0 to 1" version keeps a star dim most of the time
        //     with a short bright peak.
        for (int i = 0; i < _starPhase.Length; i++)
        {
            float bright = 0.5f + 0.5f * (float)Math.Sin(_time * _starSpeed[i] + _starPhase[i]);
            PointF at = _scenery.Stars[i];
            _starGlow.DrawCentered(fb, at.X, at.Y, bright * bright * 0.9f, _scenery.OpenSky);
        }

        // 2c. The village: every window's lamp wanders a little, and now and
        //     then one goes dark for a while (somebody went to bed) and comes back.
        for (int i = 0; i < _villagePhase.Length; i++)
        {
            float p = _villagePhase[i];
            float awake = (float)Math.Sin(_time * 0.05 + p) > -0.86f ? 1f : 0.15f;
            float lamp = 0.55f + 0.20f * (float)Math.Sin(_time * 1.3 + p * 3) + 0.12f * (float)Math.Sin(_time * 4.1 + p);
            PointF at = _scenery.VillageWindows[i];
            _villageGlow.DrawCentered(fb, at.X, at.Y, lamp * awake);
        }

        // 2d. The cabin's smoke and its windows' lamplight.
        _smoke.Draw(fb);
        for (int i = 0; i < _scenery.CabinWindows.Length; i++)
        {
            RectangleF win = _scenery.CabinWindows[i];
            float lamp = 0.36f + 0.07f * (float)Math.Sin(_time * 1.7 + i * 2.1) + 0.04f * (float)Math.Sin(_time * 5.3 + i);
            _windowGlow.DrawCentered(fb, win.X + win.Width / 2, win.Y + win.Height / 2, lamp);
        }

        // 3. Santa, behind everything that is not sky (in practice, the corner branches).
        _santa.Draw(fb);

        // 4. The lights. sin() swings between -1 and 1; the arithmetic turns
        //    that into "between 35% and 100% bright", so a bulb dims but
        //    never goes fully out.
        foreach (ref readonly Light l in _lights.AsSpan())
        {
            float bright = 0.35f + 0.65f * (0.5f + 0.5f * (float)Math.Sin(_time * l.Speed + l.Phase));
            _bulbGlows[l.Color].DrawCentered(fb, l.X, l.Y, bright);
        }

        // 4a. The snow's glitter and the lamp. A sparkle is sin() raised to a
        //     high power: nothing most of the time, one short flash per cycle.
        foreach (ref readonly var gl in _glitter.AsSpan())
        {
            float flash = MathF.Pow(MathF.Max(0f, (float)Math.Sin(_time * gl.Speed + gl.Phase)), 24);
            if (flash > 0.02f && !_noSnow) _glint.DrawCentered(fb, gl.X, gl.Y, flash * 0.9f);   // off with the snow in the pop detector's runs: a sparkle beside an animal read as the animal popping
        }
        {
            // The lamp's flame gutters: two quick waves and a slow one, so it never ticks like a clock.
            float flick = 0.80f + 0.08f * (float)Math.Sin(_time * 7.3) + 0.06f * (float)Math.Sin(_time * 13.1 + 1.0) + 0.05f * (float)Math.Sin(_time * 2.1);
            _lampHalo.DrawCentered(fb, _scenery.Lamp.X, _scenery.Lamp.Y, 0.50f * flick);
            _lampGlow.DrawCentered(fb, _scenery.Lamp.X, _scenery.Lamp.Y, 0.75f * flick);
        }

        // 5. The residents, far to near: each hands the queue one line per
        //    animal, and the queue draws them in order of their feet.
        if (_started)
            foreach (Resident r in _residents) if (r.Ready) r.Collect(_queue);
        _queue.Flush(fb);

        // 5b. The story that is on, if any: its actors and props, over the residents.
        _stories.Draw(fb);

        // 6. The NEAR happenings (layer 1): everything that is in front of
        //    the scenery, like the owl on its twig. After Santa and the
        //    glows, so the sleigh (which flies behind the branches) cannot
        //    be painted over an owl sitting on one.
        _happenings.Draw(fb, 1);

        // 7. The snow, far to near, in front of everything.
        if (!_noSnow) _snow.Draw(fb);

        // 8. Happenings on the GLASS (layer 2): frost on the window pane is
        //    nearer than the falling snow outside it.
        _happenings.Draw(fb, 2);
    }

    public void Dispose() { }
}

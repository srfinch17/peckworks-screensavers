using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// Sakura Pond's knobs. Saved under HKEY_CURRENT_USER\Software\Peckworks\Screensavers\SakuraPond.
/// The base class does the saving; the settings dialog makes one slider per knob.
/// </summary>
public sealed class SakuraPondSettings : ScreensaverSettings
{
    private readonly IntSetting _petals, _koi, _goldfish, _koiPlay, _minnows, _turtles, _dragonflies, _birds, _snake, _lizard, _lizards, _mice, _frogs, _tadpoles, _crayfish, _prawns, _deep, _ducks, _log, _drawWidth;

    public SakuraPondSettings() : base("SakuraPond")
    {
        _petals = Add(nameof(PetalPercent), "Petals", 0, 300, 100, "%");
        _koi = Add(nameof(KoiCount), "Koi", 0, 14, 10);   // ten since the pond went deep: they have the depth to spread out in
        _goldfish = Add(nameof(GoldfishCount), "Goldfish", 0, 6, 3);   // a small shoal among the koi
        _koiPlay = Add(nameof(KoiPlayPercent), "Koi play", 0, 300, 100, "%");
        _minnows = Add(nameof(MinnowSchools), "Minnow schools", 0, 4, 2);
        _turtles = Add(nameof(TurtleCount), "Turtles", 0, 3, 1);
        _dragonflies = Add(nameof(DragonflyCount), "Dragonflies", 0, 6, 2);
        _birds = Add(nameof(BirdPercent), "Swallows", 0, 300, 100, "%");
        _snake = Add(nameof(SnakePercent), "Snake", 0, 300, 100, "%");
        _lizards = Add(nameof(LizardCount), "Lizards", 0, 3, 2);
        _lizard = Add(nameof(LizardPercent), "Lizard visits", 0, 300, 100, "%");
        _mice = Add(nameof(MouseCount), "Mice", 0, 3, 1);
        _frogs = Add(nameof(FrogCount), "Frogs", 0, 4, 2);
        _tadpoles = Add(nameof(TadpoleClutches), "Tadpole clutches", 0, 3, 1);
        _crayfish = Add(nameof(CrayfishPercent), "Crayfish", 0, 300, 100, "%");
        _prawns = Add(nameof(PrawnCount), "Prawns", 0, 6, 3);           // a few freshwater prawns on the bed of the shallows (round thirty-two)
        _deep = Add(nameof(DeepDwellerCount), "Deep dwellers", 0, 6, 4);   // the crayfish and long-armed prawns of the deep bed (round thirty-seven): every third from the first is a crayfish, so 4 is two of each
        _ducks = Add(nameof(DuckPercent), "Ducks", 0, 300, 0, "%");   // off by default since 2026-10-23 (his word: "turn off the ducks for now"); the slider brings them back
        _log = Add(nameof(FallenLog), "Fallen log", 0, 1, 0);          // off by default since 2026-10-23 ("the log just feels outta place"; the code stays, 1 brings it back); a small rock formation stands where it lay
        // 1920 since the boughs sway (round twenty-five): on a 4K screen the new build at 2048 took two pages on one
        // frame in ten during a gust where the old took one in forty; at 1920 it costs what the old did at 2048,
        // and 1920 against 2048 enlarged to 3840 is not a difference the eye finds on this soft, painterly scene.
        _drawWidth = Add(nameof(DrawWidth), "Draw width", 1600, 3840, 1920, "px");
    }

    /// <summary>How many lizards live on the bank (each with its own temper). 0 = none.</summary>
    public int LizardCount => _lizards.Value;

    /// <summary>How many frogs live on the lily pads and the bank (each with its own temper). 0 = none.</summary>
    public int FrogCount => _frogs.Value;

    /// <summary>How many clutches of tadpoles wriggle in the shallows. 0 = none (and no froglets ever swim away).</summary>
    public int TadpoleClutches => _tadpoles.Value;

    /// <summary>How often the crayfish comes out from under its rock. 100 = every few minutes, 0 = never.</summary>
    public int CrayfishPercent => _crayfish.Value;

    /// <summary>How many freshwater prawns live on the bed of the shallows. 0 = none.</summary>
    public int PrawnCount => _prawns.Value;

    /// <summary>How many crayfish and long-armed prawns live on the deep bed (every third from the first a crayfish). 0 = none.</summary>
    public int DeepDwellerCount => _deep.Value;

    /// <summary>How often a duck (often with ducklings) swims by. 100 = every several minutes, 0 = never.</summary>
    public int DuckPercent => _ducks.Value;

    /// <summary>1 = a fallen log lies at the lower left (beached or afloat); 0 = no log, a small rock formation stands out in the water there instead.</summary>
    public bool FallenLog => _log.Value != 0;

    /// <summary>How many field mice live in the bank's grass (each with its own nerve). 0 = none.</summary>
    public int MouseCount => _mice.Value;

    /// <summary>How often each lizard comes out on the bank. 100 = every couple of minutes, 0 = never.</summary>
    public int LizardPercent => _lizard.Value;

    /// <summary>How many petals fall. 0 = none (the ones already floating stay).</summary>
    public int PetalPercent => _petals.Value;

    /// <summary>How many koi swim in the pond.</summary>
    public int KoiCount => _koi.Value;

    /// <summary>How many goldfish swim among the koi (small, orange, shoaling). 0 = none.</summary>
    public int GoldfishCount => _goldfish.Value;

    /// <summary>How playful the koi are: how often they go and look at things, follow a friend or chase one. 0 = they only cruise and rest.</summary>
    public int KoiPlayPercent => _koiPlay.Value;

    /// <summary>How many schools of minnows there are for the dragonflies to tease. 0 = none (and no game).</summary>
    public int MinnowSchools => _minnows.Value;

    /// <summary>How many turtles paddle about.</summary>
    public int TurtleCount => _turtles.Value;

    /// <summary>How many dragonflies patrol the pond.</summary>
    public int DragonflyCount => _dragonflies.Value;

    /// <summary>How often swallows swoop down to drink. 100 = every minute or so, 0 = never.</summary>
    public int BirdPercent => _birds.Value;

    /// <summary>How often a snake swims across. 100 = every few minutes, 0 = never.</summary>
    public int SnakePercent => _snake.Value;

    /// <summary>
    /// How wide a picture to draw, in pixels, on a screen wider than this
    /// (Windows enlarges it to fill the screen). Fewer pixels = smoother. On a
    /// 4K screen 2048 wide keeps a steady 60 frames a second on the owner's
    /// laptop; 2560 is sharper and runs at an even 30. Narrower screens are
    /// drawn at their own size whatever this says.
    /// </summary>
    public int DrawWidth => _drawWidth.Value;

    public static SakuraPondSettings LoadSaved()
    {
        var s = new SakuraPondSettings();
        s.Load();
        return s;
    }
}

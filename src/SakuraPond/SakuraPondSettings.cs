using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// Sakura Pond's knobs. Saved under HKEY_CURRENT_USER\Software\Peckworks\Screensavers\SakuraPond.
/// The base class does the saving; the settings dialog makes one slider per knob.
/// </summary>
public sealed class SakuraPondSettings : ScreensaverSettings
{
    private readonly IntSetting _petals, _koi, _koiPlay, _minnows, _turtles, _dragonflies, _birds, _snake, _lizard, _lizards, _drawWidth;

    public SakuraPondSettings() : base("SakuraPond")
    {
        _petals = Add(nameof(PetalPercent), "Petals", 0, 300, 100, "%");
        _koi = Add(nameof(KoiCount), "Koi", 0, 14, 10);   // ten since the pond went deep: they have the depth to spread out in
        _koiPlay = Add(nameof(KoiPlayPercent), "Koi play", 0, 300, 100, "%");
        _minnows = Add(nameof(MinnowSchools), "Minnow schools", 0, 4, 2);
        _turtles = Add(nameof(TurtleCount), "Turtles", 0, 3, 1);
        _dragonflies = Add(nameof(DragonflyCount), "Dragonflies", 0, 6, 2);
        _birds = Add(nameof(BirdPercent), "Swallows", 0, 300, 100, "%");
        _snake = Add(nameof(SnakePercent), "Snake", 0, 300, 100, "%");
        _lizards = Add(nameof(LizardCount), "Lizards", 0, 3, 2);
        _lizard = Add(nameof(LizardPercent), "Lizard visits", 0, 300, 100, "%");
        _drawWidth = Add(nameof(DrawWidth), "Draw width", 1600, 3840, 2048, "px");
    }

    /// <summary>How many lizards live on the bank (each with its own temper). 0 = none.</summary>
    public int LizardCount => _lizards.Value;

    /// <summary>How often each lizard comes out on the bank. 100 = every couple of minutes, 0 = never.</summary>
    public int LizardPercent => _lizard.Value;

    /// <summary>How many petals fall. 0 = none (the ones already floating stay).</summary>
    public int PetalPercent => _petals.Value;

    /// <summary>How many koi swim in the pond.</summary>
    public int KoiCount => _koi.Value;

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

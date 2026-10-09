using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// Sakura Pond's knobs. Saved under HKEY_CURRENT_USER\Software\Peckworks\Screensavers\SakuraPond.
/// The base class does the saving; the settings dialog makes one slider per knob.
/// </summary>
public sealed class SakuraPondSettings : ScreensaverSettings
{
    private readonly IntSetting _petals, _koi, _dragonflies, _birds, _snake;

    public SakuraPondSettings() : base("SakuraPond")
    {
        _petals = Add(nameof(PetalPercent), "Petals", 0, 300, 100, "%");
        _koi = Add(nameof(KoiCount), "Koi", 0, 12, 7);
        _dragonflies = Add(nameof(DragonflyCount), "Dragonflies", 0, 6, 2);
        _birds = Add(nameof(BirdPercent), "Swallows", 0, 300, 100, "%");
        _snake = Add(nameof(SnakePercent), "Snake", 0, 300, 100, "%");
    }

    /// <summary>How many petals fall. 0 = none (the ones already floating stay).</summary>
    public int PetalPercent => _petals.Value;

    /// <summary>How many koi swim in the pond.</summary>
    public int KoiCount => _koi.Value;

    /// <summary>How many dragonflies patrol the pond.</summary>
    public int DragonflyCount => _dragonflies.Value;

    /// <summary>How often swallows swoop down to drink. 100 = every minute or so, 0 = never.</summary>
    public int BirdPercent => _birds.Value;

    /// <summary>How often a snake swims across. 100 = every few minutes, 0 = never.</summary>
    public int SnakePercent => _snake.Value;

    public static SakuraPondSettings LoadSaved()
    {
        var s = new SakuraPondSettings();
        s.Load();
        return s;
    }
}

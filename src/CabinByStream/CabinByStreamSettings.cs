using Peckworks.Screensavers.Core;

namespace CabinByStream;

/// <summary>
/// Cabin by Stream's knobs. Saved under
/// HKEY_CURRENT_USER\Software\Peckworks\Screensavers\CabinByStream. The base
/// class does the saving; the settings dialog makes one slider per knob.
/// </summary>
public sealed class CabinByStreamSettings : ScreensaverSettings
{
    private readonly IntSetting _fireflies, _fish, _squirrels, _smoke, _breeze;

    public CabinByStreamSettings() : base("CabinByStream")
    {
        _fireflies = Add(nameof(FireflyPercent), "Fireflies", 0, 300, 100, "%");
        _fish = Add(nameof(FishCount), "Fish", 0, 8, 4);
        _squirrels = Add(nameof(SquirrelCount), "Squirrels", 0, 4, 2);
        _smoke = Add(nameof(SmokePercent), "Chimney smoke", 0, 300, 100, "%");
        _breeze = Add(nameof(BreezePercent), "Breeze", 0, 300, 100, "%");
    }

    /// <summary>How many fireflies are out over the meadow. 100 = a few dozen on a wide screen, 0 = none.</summary>
    public int FireflyPercent => _fireflies.Value;

    /// <summary>How many fish swim in the stream.</summary>
    public int FishCount => _fish.Value;

    /// <summary>How many squirrels run around the trees.</summary>
    public int SquirrelCount => _squirrels.Value;

    /// <summary>How much smoke the chimney gives off. 0 = the fire is out.</summary>
    public int SmokePercent => _smoke.Value;

    /// <summary>How hard the breeze leans the smoke and nudges the fireflies. 0 = still air.</summary>
    public int BreezePercent => _breeze.Value;

    public static CabinByStreamSettings LoadSaved()
    {
        var s = new CabinByStreamSettings();
        s.Load();
        return s;
    }
}

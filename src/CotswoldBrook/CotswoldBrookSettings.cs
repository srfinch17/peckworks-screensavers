using Peckworks.Screensavers.Core;

namespace CotswoldBrook;

/// <summary>
/// Cotswold Brook's knobs. Saved under
/// HKEY_CURRENT_USER\Software\Peckworks\Screensavers\CotswoldBrook. The base
/// class does the saving; the settings dialog makes one slider per knob.
/// </summary>
public sealed class CotswoldBrookSettings : ScreensaverSettings
{
    private readonly IntSetting _ripples, _trout, _rises, _smoke, _mist, _fireflies, _evening;

    public CotswoldBrookSettings() : base("CotswoldBrook")
    {
        _ripples = Add(nameof(RipplePercent), "Ripples", 0, 300, 100, "%");
        _trout = Add(nameof(TroutCount), "Trout", 0, 8, 4);
        _rises = Add(nameof(RisePercent), "Trout rising", 0, 300, 100, "%");
        _smoke = Add(nameof(SmokePercent), "Chimney smoke", 0, 300, 100, "%");
        _mist = Add(nameof(MistPercent), "Mist on the water", 0, 300, 100, "%");
        _fireflies = Add(nameof(FireflyPercent), "Fireflies", 0, 300, 100, "%");
        _evening = Add(nameof(EveningMinutes), "Evening falls every", 0, 30, 8, " min");
    }

    /// <summary>How much the water moves. 0 = a perfect mirror.</summary>
    public int RipplePercent => _ripples.Value;

    /// <summary>How many trout swim in the brook.</summary>
    public int TroutCount => _trout.Value;

    /// <summary>How often a trout rises and leaves rings on the water.</summary>
    public int RisePercent => _rises.Value;

    /// <summary>How much smoke the chimney gives off. 0 = the fire is out.</summary>
    public int SmokePercent => _smoke.Value;

    /// <summary>How much mist lies on the far water. 0 = a clear evening.</summary>
    public int MistPercent => _mist.Value;

    /// <summary>How many fireflies come out over the reeds once it is dusk.</summary>
    public int FireflyPercent => _fireflies.Value;

    /// <summary>
    /// How long one full turn of the light takes: golden evening, dusk falling,
    /// a while at dusk, then the gold slowly back. 0 = always golden.
    /// </summary>
    public int EveningMinutes => _evening.Value;

    public static CotswoldBrookSettings LoadSaved()
    {
        var s = new CotswoldBrookSettings();
        s.Load();
        return s;
    }
}

using Peckworks.Screensavers.Core;

namespace Thanksgiving;

/// <summary>
/// Thanksgiving's knobs: the same four as Halloween (for leaves), plus how
/// many geese are in the V and how often a surprise happens. Saved under
/// HKEY_CURRENT_USER\Software\Peckworks\Screensavers\Thanksgiving. The base
/// class does the saving; the settings dialog makes one slider per knob.
/// </summary>
public sealed class ThanksgivingSettings : ScreensaverSettings
{
    private readonly IntSetting _density, _speed, _wind, _size, _geese, _turkeys, _deer, _surprises;

    public ThanksgivingSettings() : base("Thanksgiving")
    {
        _density = Add(nameof(DensityPercent), "Leaves", 20, 300, 100, "%");
        _speed = Add(nameof(FallSpeedPercent), "Fall speed", 25, 300, 100, "%");
        _wind = Add(nameof(WindPercent), "Breeze", 0, 300, 100, "%");
        _size = Add(nameof(LeafSizePercent), "Leaf size", 50, 200, 100, "%");
        _geese = Add(nameof(GeeseCount), "Geese", 0, 15, 7);
        _turkeys = Add(nameof(TurkeyCount), "Turkeys", 0, 6, 3);
        _deer = Add(nameof(DeerCount), "Deer", 0, 3, 2);
        _surprises = Add(nameof(SurprisePercent), "Surprises", 0, 300, 100, "%");
    }

    /// <summary>How many leaves are falling at once.</summary>
    public int DensityPercent => _density.Value;

    /// <summary>How fast leaves fall. 100 = a slow drift.</summary>
    public int FallSpeedPercent => _speed.Value;

    /// <summary>How hard the breeze pushes leaves sideways. 0 = still air.</summary>
    public int WindPercent => _wind.Value;

    /// <summary>Leaf size.</summary>
    public int LeafSizePercent => _size.Value;

    /// <summary>How many geese fly in the V across the sky. 0 = none.</summary>
    public int GeeseCount => _geese.Value;

    /// <summary>How many wild turkeys live in the field. 0 = none.</summary>
    public int TurkeyCount => _turkeys.Value;

    /// <summary>How many deer live at the far side of the field: 1 a doe, 2 a doe and her fawn, 3 two does and a fawn. 0 = none.</summary>
    public int DeerCount => _deer.Value;

    /// <summary>How often a happening (a turkey, a tractor, fireworks...) comes on. 100 = one every 5 to 12 seconds, 0 = never.</summary>
    public int SurprisePercent => _surprises.Value;

    public static ThanksgivingSettings LoadSaved()
    {
        var s = new ThanksgivingSettings();
        s.Load();
        return s;
    }
}

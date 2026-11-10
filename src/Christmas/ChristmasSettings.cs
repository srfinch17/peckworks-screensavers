using Peckworks.Screensavers.Core;

namespace Christmas;

/// <summary>
/// Christmas's knobs: the same four as Sakura, but for snow. Saved under
/// HKEY_CURRENT_USER\Software\Peckworks\Screensavers\Christmas. The base class
/// does the saving; the settings dialog makes one slider per knob.
/// </summary>
public sealed class ChristmasSettings : ScreensaverSettings
{
    private readonly IntSetting _density, _speed, _wind, _size, _surprises, _stories, _reindeer, _hares, _birds, _mice, _otters, _drawWidth;

    public ChristmasSettings() : base("Christmas")
    {
        _density = Add(nameof(DensityPercent), "Snow", 20, 300, 100, "%");
        _speed = Add(nameof(FallSpeedPercent), "Fall speed", 25, 300, 100, "%");
        _wind = Add(nameof(WindPercent), "Breeze", 0, 300, 100, "%");
        _size = Add(nameof(FlakeSizePercent), "Flake size", 50, 200, 100, "%");
        _surprises = Add(nameof(SurprisePercent), "Surprises", 0, 300, 100, "%");
        _stories = Add(nameof(StoryPercent), "Stories", 0, 300, 100, "%");
        _reindeer = Add(nameof(ReindeerCount), "Reindeer", 0, 6, 3, "");
        _hares = Add(nameof(HareCount), "Hares", 0, 6, 3, "");
        _birds = Add(nameof(BirdCount), "Birds", 0, 10, 5, "");
        _mice = Add(nameof(MouseCount), "Mice", 0, 6, 3, "");
        _otters = Add(nameof(OtterCount), "Otters", 0, 3, 2, "");
        _drawWidth = Add(nameof(DrawWidth), "Draw width", 1600, 3840, 1920, "px");
    }

    /// <summary>How many snowflakes are falling at once.</summary>
    public int DensityPercent => _density.Value;

    /// <summary>How fast the snow falls. 100 = a slow, gentle snowfall.</summary>
    public int FallSpeedPercent => _speed.Value;

    /// <summary>How hard the breeze pushes the snow sideways. 0 = still air.</summary>
    public int WindPercent => _wind.Value;

    /// <summary>Snowflake size.</summary>
    public int FlakeSizePercent => _size.Value;

    /// <summary>How often a happening (a fox, a snowman, fireworks...) comes on. 100 = one every 5 to 12 seconds, 0 = never.</summary>
    public int SurprisePercent => _surprises.Value;

    /// <summary>How often a story (a little scene of half a minute) plays. 100 = one every minute or so, 0 = never.</summary>
    public int StoryPercent => _stories.Value;

    /// <summary>How many reindeer graze by the pines.</summary>
    public int ReindeerCount => _reindeer.Value;

    /// <summary>How many snowshoe hares live on the snowfield (the fox comes with them; 0 hares = no fox).</summary>
    public int HareCount => _hares.Value;

    /// <summary>How many birds visit the bird table.</summary>
    public int BirdCount => _birds.Value;

    /// <summary>How many mice live in the fallen log.</summary>
    public int MouseCount => _mice.Value;

    /// <summary>How many otters play on the frozen pond.</summary>
    public int OtterCount => _otters.Value;

    /// <summary>
    /// The widest the scene is ever drawn. On a wider screen (a 4K one) it is drawn at this width and Windows
    /// enlarges the picture. Drawing all eight million pixels of a 4K screen took 21 to 23 thousandths of a second
    /// a frame, more than the 16.7 a smooth 60 frames a second allows, so nothing could move smoothly there.
    /// </summary>
    public int DrawWidth => _drawWidth.Value;

    public static ChristmasSettings LoadSaved()
    {
        var s = new ChristmasSettings();
        s.Load();
        return s;
    }
}

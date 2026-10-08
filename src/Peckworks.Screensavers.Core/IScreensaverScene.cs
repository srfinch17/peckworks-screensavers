namespace Peckworks.Screensavers.Core;

/// <summary>
/// One running animation. This is the only thing a new screensaver has to write.
///
/// FEYNMAN VERSION: every animation, from a flip-book to a video game, is the
/// same two-step loop repeated about 60 times a second:
///
///     1. Update: move the world forward a little bit of time.
///                ("The raindrop was at row 10; 0.016 seconds passed at 20 rows
///                 per second, so now it is at row 10.32.")
///     2. Render: draw what the world looks like right now.
///
/// The engine (SceneView) owns the clock and calls these two methods over and
/// over. The scene never has to think about windows, monitors, timers, or
/// quitting when the mouse moves. It just moves things and draws them.
///
/// Why pass "seconds elapsed" instead of assuming 1/60th of a second? Because
/// computers are not metronomes. A frame can take 10ms or 30ms. If you move a
/// raindrop "one step per frame", it rains faster on a faster PC. If you move it
/// "speed x elapsed time", it rains at the same speed everywhere.
/// </summary>
public interface IScreensaverScene : IDisposable
{
    /// <summary>Advance the world by this many seconds (usually about 0.016).</summary>
    void Update(double elapsedSeconds);

    /// <summary>Draw the current state of the world into the given picture.</summary>
    void Render(FrameBuffer target);
}

/// <summary>
/// Describes one screensaver to the engine: its name, how to build its scene,
/// and how to build its settings dialog.
/// </summary>
public abstract class ScreensaverDefinition
{
    /// <summary>Friendly name, shown in window titles.</summary>
    public abstract string DisplayName { get; }

    /// <summary>
    /// Build a fresh scene sized for a surface of width x height pixels.
    /// isPreview is true when we are drawing into the tiny monitor picture in
    /// Screen Saver Settings (about 150 x 110 pixels), so the scene can scale
    /// itself down to something that still looks right at that size.
    /// </summary>
    public abstract IScreensaverScene CreateScene(int width, int height, bool isPreview);

    /// <summary>
    /// The widest this saver ever needs to DRAW. On a wider screen it draws at
    /// this width and Windows stretches the picture to fill the screen. A
    /// saver built on a small photo gains nothing from drawing every one of a
    /// 4K screen's eight million pixels (the photo has no detail that fine),
    /// and drawing a quarter as many keeps it smooth. Most savers leave this
    /// alone (no limit).
    /// </summary>
    public virtual int MaxRenderWidth => int.MaxValue;

    /// <summary>
    /// Build the dialog shown when the user clicks "Settings..." in Windows.
    /// </summary>
    public abstract Form CreateSettingsForm();
}

namespace Peckworks.Screensavers.Core;

/// <summary>What Windows (or you, for testing) asked the program to do.</summary>
public enum LaunchMode
{
    /// <summary>/s : "Show the screensaver now, full screen, on every monitor."</summary>
    Screensaver,

    /// <summary>/p 12345 : "Draw a tiny live preview inside window number 12345."</summary>
    Preview,

    /// <summary>/c or no arguments : "Show your settings dialog."</summary>
    Configure,

    /// <summary>/window : our own extra mode. Runs in a normal resizable window for development.</summary>
    Window,

    /// <summary>/snapshot file.png [w h seconds] : our own extra mode. Renders off-screen and saves a picture.</summary>
    Snapshot,
}

/// <summary>
/// Reads the command line Windows hands us and works out the LaunchMode.
///
/// FEYNMAN VERSION: Windows does not "know" anything about your screensaver. It
/// just runs your .scr file like any program and passes a short note on the
/// command line saying what it wants. There are only three notes:
///
///     /s          "the user went idle, take over the screen"
///     /p 12345    "draw a little preview in the Settings dialog; 12345 is
///                  the ID number (HWND) of the tiny box to draw inside"
///     /c          "the user clicked the Settings button"
///
/// Windows is sloppy about the exact spelling, so real-world versions include
/// "/S", "-s", "/c:12345", "/p:12345", and plain nothing (which means /c).
/// We accept all of them.
///
/// HWND means "Handle to a WiNDow": Windows' ID number for a window.
/// </summary>
public sealed record CommandLine(LaunchMode Mode, IntPtr WindowHandle, string[] Extra)
{
    public static CommandLine Parse(string[] args)
    {
        if (args.Length == 0)
            return new CommandLine(LaunchMode.Configure, IntPtr.Zero, []);

        // "/c:12345" becomes key "c" and value "12345".
        // "/p 12345" becomes key "p" and value from the next argument.
        string first = args[0].Trim().TrimStart('/', '-').ToLowerInvariant();
        string key = first;
        string? value = null;
        int colon = first.IndexOf(':');
        if (colon >= 0)
        {
            key = first[..colon];
            value = first[(colon + 1)..];
        }
        else if (args.Length > 1)
        {
            value = args[1];
        }

        string[] rest = args.Skip(1).ToArray();

        return key switch
        {
            "s" => new CommandLine(LaunchMode.Screensaver, IntPtr.Zero, []),
            "p" or "l" => new CommandLine(LaunchMode.Preview, ParseHandle(value), []),
            "c" => new CommandLine(LaunchMode.Configure, ParseHandle(value), []),
            "window" or "w" => new CommandLine(LaunchMode.Window, IntPtr.Zero, rest),
            "snapshot" => new CommandLine(LaunchMode.Snapshot, IntPtr.Zero, rest),
            // Unknown note: the safe, polite thing is to show settings.
            _ => new CommandLine(LaunchMode.Configure, IntPtr.Zero, []),
        };
    }

    private static IntPtr ParseHandle(string? text) =>
        long.TryParse(text, out long n) ? new IntPtr(n) : IntPtr.Zero;
}

using System.Diagnostics;

namespace Peckworks.Screensavers.Core;

/// <summary>
/// A stopwatch split into named sections, for finding out WHAT in a frame
/// costs the time. Off (and free) unless the environment variable
/// PECKWORKS_SECTIONS names a file; then every scene that calls
/// <see cref="Mark"/> between the parts of its Render gets a table in that
/// file when the program ends: each section's average milliseconds per frame
/// and its share of the whole.
///
/// FEYNMAN VERSION: a frame's draw time is one number, and one number cannot
/// say whether the grass or the fish is the slow part. So the scene calls
/// Mark("grass") after drawing the grass, Mark("koi") after the koi, and so
/// on: each call puts the time since the previous call into that section's
/// bucket, like a runner pressing the lap button at each marker post. The
/// buckets are totalled at the end. The repo rule is to time the parts
/// INSIDE one run, because the machine drifts 20 to 40 percent between runs.
///
/// Usage in a scene's Render:
///     Sections.Begin();           // the frame starts: the stopwatch resets
///     DrawGrass(fb); Sections.Mark("grass");
///     DrawKoi(fb);   Sections.Mark("koi");
/// Then run a snapshot with PECKWORKS_SECTIONS=C:\somewhere\sections.txt.
/// </summary>
public static class Sections
{
    private static readonly string? Path = Environment.GetEnvironmentVariable("PECKWORKS_SECTIONS");
    private static readonly bool On = !string.IsNullOrEmpty(Path);
    private static readonly Stopwatch Clock = new();
    private static readonly Dictionary<string, double> Totals = new();
    private static readonly List<string> Order = new();
    private static int _frames;
    private static long _last;

    static Sections()
    {
        if (On) AppDomain.CurrentDomain.ProcessExit += (_, _) => Write();
    }

    /// <summary>The frame begins: start the laps from here.</summary>
    public static void Begin()
    {
        if (!On) return;
        if (!Clock.IsRunning) Clock.Start();
        _last = Clock.ElapsedTicks;
        _frames++;
    }

    /// <summary>The part called "name" has just finished: its time is the time since the last mark (or Begin).</summary>
    public static void Mark(string name)
    {
        if (!On) return;
        long now = Clock.ElapsedTicks;
        double ms = (now - _last) * 1000.0 / Stopwatch.Frequency;
        _last = now;
        if (!Totals.TryGetValue(name, out double total)) Order.Add(name);
        Totals[name] = total + ms;
    }

    private static void Write()
    {
        if (_frames == 0) return;
        double all = Totals.Values.Sum();
        var lines = new List<string> { $"{_frames} frames, {all / _frames:F2} ms per frame in sections" };
        foreach (string name in Order)
        {
            double ms = Totals[name] / _frames;
            lines.Add($"{name,-16} {ms,7:F2} ms  {(all > 0 ? ms * _frames / all * 100 : 0),5:F1}%");
        }
        try { File.WriteAllLines(Path!, lines); } catch (IOException) { }
    }
}

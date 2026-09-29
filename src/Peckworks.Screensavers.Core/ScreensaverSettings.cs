using Microsoft.Win32;

namespace Peckworks.Screensavers.Core;

/// <summary>
/// One whole-number knob, like "Speed: 100%". It knows its own name, range,
/// default, and current value, which is everything needed to both save it and
/// build a slider for it.
/// </summary>
public sealed class IntSetting
{
    public required string Key { get; init; }      // name used in the registry
    public required string Label { get; init; }    // text shown next to the slider
    public required int Min { get; init; }
    public required int Max { get; init; }
    public required int Default { get; init; }
    public string Unit { get; init; } = "";        // shown after the number, like "%"

    private int _value;
    public int Value
    {
        get => _value;
        set => _value = Math.Clamp(value, Min, Max);   // never let a value escape its range
    }
}

/// <summary>
/// Base class for a screensaver's settings: a list of IntSettings plus
/// load/save to the Windows Registry.
///
/// FEYNMAN VERSION: think of it as a labeled row of dials on a control panel.
/// A subclass just says which dials exist ("Speed, 25 to 300, starts at 100").
/// This class handles the boring parts every screensaver needs: remembering
/// the dial positions between runs, and handing the list to the settings
/// dialog so it can draw one slider per dial automatically.
///
/// WHERE SETTINGS LIVE: the Windows Registry, a built-in database of settings
/// organized like folders ("keys") holding named values. We use
///     HKEY_CURRENT_USER\Software\Peckworks\Screensavers\(screensaver name)
/// "Current user" means each person who logs into the PC gets their own copy.
/// Why not a file next to the .scr? Because the .scr is installed in
/// C:\Windows\System32, where normal programs are (rightly) not allowed to
/// write. The registry's per-user area is always writable.
/// Look for yourself: Win+R, type regedit, browse to the path above.
/// </summary>
public abstract class ScreensaverSettings
{
    private readonly List<IntSetting> _all = [];

    protected ScreensaverSettings(string registryName)
    {
        RegistryPath = @"Software\Peckworks\Screensavers\" + registryName;
    }

    public string RegistryPath { get; }

    /// <summary>Every knob, in the order the dialog should show them.</summary>
    public IReadOnlyList<IntSetting> All => _all;

    /// <summary>Subclasses call this in their constructor to declare a knob.</summary>
    protected IntSetting Add(string key, string label, int min, int max, int defaultValue, string unit = "")
    {
        var s = new IntSetting { Key = key, Label = label, Min = min, Max = max, Default = defaultValue, Unit = unit };
        s.Value = defaultValue;
        _all.Add(s);
        return s;
    }

    /// <summary>Put every knob back to its default.</summary>
    public void ResetToDefaults()
    {
        foreach (IntSetting s in _all) s.Value = s.Default;
    }

    /// <summary>Read saved values. Anything missing or unreadable keeps its default.</summary>
    public void Load()
    {
        try
        {
            // "using" guarantees the key is closed again when we are done,
            // like a door that shuts itself behind you.
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            if (key == null) return;
            foreach (IntSetting s in _all)
                if (key.GetValue(s.Key) is int v) s.Value = v;   // Value's setter clamps to range
        }
        catch
        {
            // A screensaver must never crash over a settings problem.
            // If the registry cannot be read, the defaults are fine.
        }
    }

    /// <summary>Write every knob to the registry (creating the key the first time).</summary>
    public void Save()
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath);
        foreach (IntSetting s in _all)
            key.SetValue(s.Key, s.Value, RegistryValueKind.DWord);   // DWord = a 32-bit whole number
    }
}

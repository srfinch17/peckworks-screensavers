<#
.SYNOPSIS
    Helpers for Windows' "Screen Saver Settings" dialog. Dot-source it:  . .\scripts\ScreenSaverDialog.ps1

.DESCRIPTION
    Why this exists: Screen Saver Settings builds its dropdown list ONCE, when it
    opens. If it is already open and you ask Windows to open it again, Windows just
    brings the OLD window forward, still showing the OLD list, so a screensaver you
    installed a minute ago looks missing. (This really happened: the file was in
    System32, the install had succeeded, and the list still did not show it.)

    So instead of trusting "the file is in System32, therefore it is in the list",
    these helpers close any open copy, open a fresh one, and then READ the list the
    user actually sees.

    How the list is read: the dropdown is a standard Windows "ComboBox" control.
    Any program can send it messages: CB_GETCOUNT ("how many items?") and
    CB_GETLBTEXT ("give me item number i"). That is the same mechanism the dialog
    itself uses, so what we read is exactly what is on screen.

    Note: the dialog is found by its English title, "Screen Saver Settings".
#>

if (-not ('Peckworks.SsDialog' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Peckworks
{
    public static class SsDialog
    {
        const string Title = "Screen Saver Settings";
        const uint WM_CLOSE = 0x0010;
        const int CB_GETCOUNT = 0x0146, CB_GETLBTEXT = 0x0148;

        delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc f, IntPtr l);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, StringBuilder l);

        public static bool IsOpen() { return FindWindow(null, Title) != IntPtr.Zero; }

        public static void Close()
        {
            IntPtr h = FindWindow(null, Title);
            if (h != IntPtr.Zero) PostMessage(h, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }

        /// Items in the dialog's screensaver dropdown, or null if the dialog is not open.
        public static string[] Items()
        {
            IntPtr dlg = FindWindow(null, Title);
            if (dlg == IntPtr.Zero) return null;
            var items = new List<string>();
            EnumChildWindows(dlg, (h, l) =>
            {
                var cls = new StringBuilder(64);
                GetClassName(h, cls, cls.Capacity);
                if (cls.ToString() == "ComboBox")
                {
                    int n = (int)SendMessage(h, CB_GETCOUNT, IntPtr.Zero, IntPtr.Zero);
                    for (int i = 0; i < n; i++)
                    {
                        var text = new StringBuilder(256);
                        SendMessage(h, CB_GETLBTEXT, (IntPtr)i, text);
                        items.Add(text.ToString());
                    }
                }
                return true;
            }, IntPtr.Zero);
            return items.ToArray();
        }
    }
}
'@
}

<# Close any open Screen Saver Settings window and wait until it is gone. #>
function Close-ScreenSaverDialog {
    if (-not [Peckworks.SsDialog]::IsOpen()) { return }
    [Peckworks.SsDialog]::Close()
    for ($i = 0; $i -lt 40 -and [Peckworks.SsDialog]::IsOpen(); $i++) { Start-Sleep -Milliseconds 250 }
}

<# Open a FRESH Screen Saver Settings window and return the names in its dropdown. #>
function Open-ScreenSaverDialog {
    Close-ScreenSaverDialog
    Start-Process 'control.exe' -ArgumentList 'desk.cpl,,@screensaver'
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Milliseconds 250
        $items = [Peckworks.SsDialog]::Items()
        if ($items -and $items.Count -gt 0) { return $items }
    }
    return @()
}

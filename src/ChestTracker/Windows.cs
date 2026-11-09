using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ChestTracker;

/// <summary>Starts with Windows (HKCU Run key, no admin rights) in background mode.</summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "chest-tracker";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    /// <summary>Adds or removes the Run entry. Rewritten on each start so it follows the exe if it was moved.</summary>
    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled && Environment.ProcessPath is { } exe)
        {
            key.SetValue(ValueName, $"\"{exe}\" --background");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}

public static class Fortnite
{
    private const string GameProcess = "FortniteClient-Win64-Shipping";

    public static bool IsRunning()
    {
        var found = Process.GetProcessesByName(GameProcess);
        foreach (var p in found) p.Dispose();
        return found.Length > 0;
    }
}

/// <summary>System-wide shortcuts (work in game). Ctrl+Shift+O = overlay on/off, Ctrl+Shift+L = lock/unlock.</summary>
public sealed class Hotkeys : NativeWindow, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000;
    private readonly Dictionary<int, Action> _actions = new();

    public Hotkeys()
    {
        CreateHandle(new CreateParams());
    }

    /// <returns>False when another app already uses the shortcut.</returns>
    public bool Register(Keys key, Action action)
    {
        var id = _actions.Count + 1;
        if (!RegisterHotKey(Handle, id, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, (uint)key))
        {
            return false;
        }
        _actions[id] = action;
        return true;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && _actions.TryGetValue((int)m.WParam, out var action))
        {
            action();
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys) UnregisterHotKey(Handle, id);
        DestroyHandle();
    }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}

/// <summary>Readable names for playlists, placements and dates.</summary>
public static class Pretty
{
    public static string Playlist(string? playlist)
    {
        if (string.IsNullOrEmpty(playlist)) return "—";
        var p = playlist.ToLowerInvariant();
        var mode = p.Contains("nobuild") || p.Contains("zerobuild") ? Text.T("Zero Build", "Cero construcción") : "Battle Royale";
        var team = p.Contains("solo") ? "Solo" : p.Contains("duo") ? Text.T("Duos", "Dúos") : p.Contains("trio") ? Text.T("Trios", "Tríos") : p.Contains("squad") ? Text.T("Squads", "Escuadrones") : "";
        if (p.Contains("ranked") || p.Contains("habanero")) mode = Text.T("Ranked ", "Clasificatoria ") + mode;
        return team.Length > 0 ? $"{mode} · {team}" : mode;
    }

    public static string Placement(MatchRecord m) =>
        m.Placement is int p && p > 0 ? $"#{p}{(m.TotalPlayers is int t && t > 0 ? $" / {t}" : "")}" : "—";

    public static string When(MatchRecord m) => m.When.ToLocalTime().ToString("ddd d MMM, HH:mm", Text.Culture);
}

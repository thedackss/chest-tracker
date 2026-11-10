namespace ChestTracker.Ui;

/// <summary>What the windows need from the running app (the tray app, or a stand-in for --preview).</summary>
public interface IAppHost
{
    Settings Settings { get; }
    Totals Totals { get; }
    /// <summary>Copy of the match history.</summary>
    List<MatchRecord> Matches { get; }
    /// <summary>One line for the side bar: watching, match in progress...</summary>
    string Status { get; }
    bool OverlayLocked { get; }

    /// <summary>Raised on the UI thread when totals, matches or the overlay state change.</summary>
    event Action? Changed;

    /// <summary>Saves <see cref="Settings"/> after the windows changed it and applies it (language, autostart, overlay...).</summary>
    void SettingsChanged();
    void ToggleOverlay();
    void ToggleLock();
    /// <summary>Writes report.html and opens it in the browser.</summary>
    void ExportReport();
    void OpenDataFolder();
    void Quit();

    /// <summary>The real overlay drawing as PNG, at <paramref name="scale"/> (1 = 100 %), for the settings preview.</summary>
    byte[] OverlayPng(float scale);

    /// <summary>Address of the OBS overlay page while it is served (null when off or no port was free).</summary>
    string? ObsUrl { get; }
}

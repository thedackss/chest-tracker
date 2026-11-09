using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChestTracker;

/// <summary>Settings and history as JSON in %APPDATA%\chest-tracker (or a folder given on the command line).</summary>
public sealed class Store
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Folder { get; }
    public string SettingsPath => Path.Combine(Folder, "settings.json");
    public string DataPath => Path.Combine(Folder, "history.json");
    public string ReportPath => Path.Combine(Folder, "report.html");
    public string LogPath => Path.Combine(Folder, "errors.log");

    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "chest-tracker");

    public Store(string? folder = null)
    {
        Folder = folder ?? DefaultFolder;
        Directory.CreateDirectory(Folder);
    }

    public bool HasSettings => File.Exists(SettingsPath);

    public Settings LoadSettings() => Load<Settings>(SettingsPath) ?? new Settings();
    public void SaveSettings(Settings s) => Save(SettingsPath, s);

    public TrackerData LoadData() => Load<TrackerData>(DataPath) ?? new TrackerData();
    public void SaveData(TrackerData d) => Save(DataPath, d);

    public void Log(string message, Exception? ex = null)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{(ex != null ? Environment.NewLine + ex : "")}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never break the app.
        }
    }

    private static T? Load<T>(string path) where T : class
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) : null;
        }
        catch (JsonException)
        {
            // Corrupt file: keep it aside instead of silently losing it.
            File.Copy(path, path + $".broken-{DateTime.Now:yyyyMMddHHmmss}", overwrite: true);
            return null;
        }
    }

    /// <summary>Write to a temp file then replace, so a crash never leaves half a file.</summary>
    public static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }

    private static void Save<T>(string path, T value) => WriteAtomic(path, JsonSerializer.Serialize(value, Json));
}

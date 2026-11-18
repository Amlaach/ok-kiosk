using System.Text.Json;
using OkKiosk.Contracts;

namespace OkKiosk.App.Core;

public sealed class AppConfig
{
    public string KioskTitle { get; set; } = "OK Kiosk";
    public string Language { get; set; } = "he";
    public bool BlockTaskManager { get; set; } = true;
    public bool KillExplorer { get; set; } = true;
    public bool InstallKeyboardHook { get; set; } = true;
    public bool EnforceForeground { get; set; } = true;
    public bool StrictShell { get; set; } = false;
    public bool BlockUsbStorage { get; set; } = false;
    public bool AutoLockOnStart { get; set; } = false;
    public List<AllowedProgram> AllowedPrograms { get; set; } = new();
    public List<string> AllowedFolders { get; set; } = new();

    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OkKiosk");

    private static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                if (cfg is not null) return cfg;
            }
        }
        catch
        {
            // Fall back to defaults on corrupt config
        }
        return new AppConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(this, JsonOpts);
        File.WriteAllText(ConfigPath, json);
    }
}

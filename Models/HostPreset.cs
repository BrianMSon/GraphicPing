using System.Text.Json;

namespace GraphicPing.Models;

public class HostPreset
{
    public string Name { get; set; } = "";
    public List<string> Hosts { get; set; } = new();
    public int Interval { get; set; } = 1000;
    public int Timeout { get; set; } = 10000;
}

public static class PresetManager
{
    private static string PresetFile => Path.Combine(
        AppContext.BaseDirectory, "presets.json");

    public static List<HostPreset> Load()
    {
        try
        {
            if (File.Exists(PresetFile))
                return JsonSerializer.Deserialize<List<HostPreset>>(
                    File.ReadAllText(PresetFile)) ?? new();
        }
        catch { }
        return GetDefaults();
    }

    public static void Save(List<HostPreset> presets)
    {
        try
        {
            File.WriteAllText(PresetFile,
                JsonSerializer.Serialize(presets, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static List<HostPreset> GetDefaults() =>
    [
        new() { Name = "Google DNS", Hosts = ["8.8.8.8", "8.8.4.4"] },
        new() { Name = "Cloudflare DNS", Hosts = ["1.1.1.1", "1.0.0.1"] },
        new() { Name = "Global CDN Test", Hosts = ["8.8.8.8", "1.1.1.1", "208.67.222.222", "9.9.9.9"] },
        new() { Name = "Korea ISP", Hosts = ["168.126.63.1", "164.124.101.2", "210.220.163.82"] },
        new() { Name = "Localhost", Hosts = ["127.0.0.1"] },
    ];
}

using System.Text.Json;

namespace TinyClips.Windows.Models;

public sealed class CaptureSettings
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TinyClips.Windows");

    private static readonly string ConfigPath = Path.Combine(ConfigDirectory, "settings.json");

    public string SaveDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "TinyClips");
    public bool CopyToClipboard { get; set; } = true;
    public bool OpenFolderAfterSave { get; set; }

    public int GifFrameRate { get; set; } = 10;
    public int GifMaxWidth { get; set; } = 640;
    public int VideoFrameRate { get; set; } = 30;
    public bool ShowTrimmer { get; set; } = false;
    public bool ShowScreenshotEditor { get; set; } = false;
    public bool ShowGifTrimmer { get; set; } = false;
    public bool VideoCountdownEnabled { get; set; } = true;
    public int VideoCountdownDuration { get; set; } = 3;
    public bool GifCountdownEnabled { get; set; } = true;
    public int GifCountdownDuration { get; set; } = 3;

    public static CaptureSettings LoadDefault()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<CaptureSettings>(json);
                if (loaded is not null)
                {
                    loaded.Normalize();
                    Directory.CreateDirectory(loaded.SaveDirectory);
                    return loaded;
                }
            }
        }
        catch
        {
        }

        var settings = new CaptureSettings();
        settings.Normalize();
        Directory.CreateDirectory(settings.SaveDirectory);
        return settings;
    }

    public void Save()
    {
        Normalize();
        Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    private void Normalize()
    {
        GifFrameRate = Math.Clamp(GifFrameRate, 5, 30);
        GifMaxWidth = Math.Clamp(GifMaxWidth, 320, 1920);
        VideoFrameRate = VideoFrameRate is 24 or 30 or 60 ? VideoFrameRate : 30;
        VideoCountdownDuration = Math.Clamp(VideoCountdownDuration, 1, 10);
        GifCountdownDuration = Math.Clamp(GifCountdownDuration, 1, 10);
    }
}

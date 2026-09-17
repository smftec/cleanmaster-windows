using System.IO;
using System.Text.Json;
using CleanMaster.Core.Store;

namespace CleanMaster.App.Services;

/// <summary>首次启动引导标记。</summary>
public static class FirstRunWizard
{
    private static string Marker => Path.Combine(SettingsService.DataDir, "welcomed.flag");

    public static bool WasWelcomed()
    {
        try { return File.Exists(Marker); } catch { return false; }
    }

    public static void MarkWelcomed()
    {
        try
        {
            Directory.CreateDirectory(SettingsService.DataDir);
            File.WriteAllText(Marker, DateTime.Now.ToString("s"));
        }
        catch { }
    }
}

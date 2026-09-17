using System.IO;
using CleanMaster.Core.Store;

namespace CleanMaster.App.Services;

/// <summary>极简文件日志（排障用），写入 %LOCALAPPDATA%\CleanMaster\app.log。</summary>
public static class AppLog
{
    private static readonly object Lock = new();
    private static string File => Path.Combine(SettingsService.DataDir, "app.log");

    public static void Write(string tag, string message)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(SettingsService.DataDir);
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{tag}] {message}{Environment.NewLine}";
                System.IO.File.AppendAllText(File, line);
                // 超过 512KB 时截断，只保留后半
                var fi = new FileInfo(File);
                if (fi.Length > 512 * 1024)
                {
                    var text = System.IO.File.ReadAllText(File);
                    System.IO.File.WriteAllText(File, text[(text.Length / 2)..]);
                }
            }
        }
        catch { }
    }

    public static void Error(string where, Exception ex) => Write("ERROR", $"{where}: {ex}");
}

using System.Diagnostics;
using System.Xml.Linq;

namespace CleanMaster.Core.Analysis;

/// <summary>
/// 开机耗时(竞品对照:360 开机时间 / 管家开机加速)。
/// 读取事件日志 Microsoft-Windows-Diagnostics-Performance/Operational 事件 100
/// 的 BootTime 属性(毫秒,Windows 官方启动性能指标)。该日志在部分系统上未启用,
/// 读不到返回 null——绝不估算、不承诺虚假数字。
/// </summary>
public static class BootTimeService
{
    private const string LogName = "Microsoft-Windows-Diagnostics-Performance/Operational";

    public static double? LastBootSeconds()
    {
        try
        {
            var psi = new ProcessStartInfo("wevtutil.exe",
                $"qe \"{LogName}\" \"/q:*[System[(EventID=100)]]\" /c:1 /rd:true /f:xml")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            using var p = Process.Start(psi)!;
            var xml = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            if (p.ExitCode != 0 || xml.Length == 0) return null;

            var doc = XDocument.Parse(xml.Contains("<Events>") ? xml : $"<Events>{xml}</Events>");
            var ns = doc.Root?.Elements().FirstOrDefault()?.Name.Namespace ?? XNamespace.None;
            var boot = doc.Descendants(ns + "BootTime").FirstOrDefault();
            return boot != null && double.TryParse(boot.Value, out var ms) && ms > 0
                ? ms / 1000.0
                : null;
        }
        catch
        {
            return null;
        }
    }
}

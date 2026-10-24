using System.Diagnostics;
using Microsoft.Win32;

namespace CleanMaster.Core.Analysis;

public sealed class MaintenanceItem
{
    /// <summary>i18n key 前缀:maint.{id}.*</summary>
    public string Id { get; init; } = "";
    /// <summary>on / off / unknown</summary>
    public string State { get; init; } = "unknown";
    /// <summary>详情(已本地化的原始文本,如当前电源计划名)。</summary>
    public string Detail { get; init; } = "";
    /// <summary>可跳转的设置 URI(ms-settings: / cpl),为空则无跳转。</summary>
    public string? TargetUri { get; init; }
}

/// <summary>
/// 系统维护状态检查(PRD §17.1):只读展示,不做任何"优化魔改"。
/// - Storage Sense 开关(HKCU StoragePolicy\01)
/// - TRIM(HKLM DisableDeleteNotification == 0 表示开启)
/// - 当前电源计划(powercfg /getactivescheme)
/// </summary>
public static class SystemMaintenanceService
{
    public static List<MaintenanceItem> CheckAll()
    {
        return new List<MaintenanceItem>
        {
            CheckStorageSense(),
            CheckTrim(),
            CheckPowerPlan(),
        };
    }

    public static MaintenanceItem CheckStorageSense()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy");
            var v = k?.GetValue("01");
            var on = v is int i ? i == 1 : v is string s && s == "1";
            return new MaintenanceItem
            {
                Id = "storagesense",
                State = on ? "on" : "off",
                TargetUri = "ms-settings:storagesense",
            };
        }
        catch
        {
            return new MaintenanceItem { Id = "storagesense", State = "unknown", TargetUri = "ms-settings:storagesense" };
        }
    }

    public static MaintenanceItem CheckTrim()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\FileSystem");
            var v = k?.GetValue("DisableDeleteNotification");
            var disabled = v is int i && i == 1;
            return new MaintenanceItem
            {
                Id = "trim",
                State = disabled ? "off" : "on",
                Detail = disabled ? "" : "",
            };
        }
        catch
        {
            return new MaintenanceItem { Id = "trim", State = "unknown" };
        }
    }

    public static MaintenanceItem CheckPowerPlan()
    {
        try
        {
            var psi = new ProcessStartInfo("powercfg", "/getactivescheme")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            // 输出形如: GUID XXXX-...  (名称)
            var idx = output.LastIndexOf('(');
            var name = idx >= 0 && output.EndsWith(")\n") || output.EndsWith(")\r\n") || output.TrimEnd().EndsWith(")")
                ? output[idx..].Trim('(', ')', '\r', '\n', ' ')
                : "";
            return new MaintenanceItem { Id = "powerplan", State = "on", Detail = name, TargetUri = "powercfg.cpl" };
        }
        catch
        {
            return new MaintenanceItem { Id = "powerplan", State = "unknown", TargetUri = "powercfg.cpl" };
        }
    }
}

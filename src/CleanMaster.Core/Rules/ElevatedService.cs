using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using CleanMaster.Core.Utils;

namespace CleanMaster.Core.Rules;

public sealed class ElevatedResult
{
    public bool Ok { get; set; }
    public string TaskId { get; set; } = "";
    public string Message { get; set; } = "";
}

/// <summary>
/// 按需提权：主界面进程保持普通权限，仅在需要时拉起带 UAC 的自身实例执行
/// 白名单内的管理任务（清传递优化缓存 / 刷新 DNS / 修改 HKLM 启动项 / 计划任务开关）。
/// 结果通过 %TEMP% 下的结果文件回传。
/// </summary>
public static class ElevatedService
{
    public static bool IsCurrentElevated()
    {
        using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    public static async Task<bool> RunTaskAsync(string taskId, string argsJson)
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return false;
            var resultFile = Path.Combine(Path.GetTempPath(), $"CleanMaster_elev_{Guid.NewGuid():N}.json");
            var psi = new ProcessStartInfo(exe)
            {
                Arguments = $"--elevated \"{taskId}\" \"{argsJson.Replace("\"", "\\\"")}\" \"{resultFile}\"",
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            using var p = Process.Start(psi);
            if (p == null) return false;

            // 等待结果文件（最多 90 秒）
            for (int i = 0; i < 900; i++)
            {
                if (File.Exists(resultFile)) break;
                await Task.Delay(100);
                if (i == 899) return false;
            }
            await Task.Delay(200);
            var json = File.ReadAllText(resultFile);
            try { File.Delete(resultFile); } catch { }
            var result = JsonSerializer.Deserialize<ElevatedResult>(json);
            return result?.Ok == true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false; // 用户取消 UAC
        }
        catch
        {
            return false;
        }
    }

    /// <summary>被提权实例调用：执行任务并写出结果。白名单之外的任务一律拒绝。</summary>
    public static void ExecuteElevatedTask(string taskId, string argsJson, string resultFile)
    {
        var result = new ElevatedResult { TaskId = taskId };
        try
        {
            if (!IsCurrentElevated())
                throw new InvalidOperationException("not elevated");

            switch (taskId)
            {
                case "clear-do-cache":
                {
                    var psi = new ProcessStartInfo("powershell.exe",
                        "-NoProfile -NonInteractive -Command \"Delete-DeliveryOptimizationCache -Force; exit 0\"")
                    { CreateNoWindow = true, UseShellExecute = false };
                    using var p = Process.Start(psi)!;
                    p.WaitForExit(120000);
                    result.Ok = p.ExitCode == 0;
                    break;
                }
                case "toggle-hklm-startup":
                {
                    var args = JsonSerializer.Deserialize<Dictionary<string, string>>(argsJson);
                    var sub = args!["sub"];            // e.g. Software\Microsoft\Windows\CurrentVersion\Run
                    var value = args["value"];
                    var disable = args["disable"] == "1";
                    using var k = Registry.LocalMachine.OpenSubKey(sub, writable: true)
                        ?? throw new InvalidOperationException("cannot open key");
                    var data = k.GetValue(value) as byte[];
                    if (data == null) throw new InvalidOperationException("value missing");
                    var nv = (byte[])data.Clone();
                    nv[0] = disable ? (byte)3 : (byte)2;
                    for (int i = 1; i < nv.Length; i++) nv[i] = 0;
                    k.SetValue(value, nv, RegistryValueKind.Binary);
                    result.Ok = true;
                    break;
                }
                case "schtasks-toggle":
                {
                    var args = JsonSerializer.Deserialize<Dictionary<string, string>>(argsJson);
                    var name = args!["task"];
                    var disable = args["disable"] == "1";
                    var psi = new ProcessStartInfo("schtasks.exe",
                        $"/Change /TN \"{name}\" /{(disable ? "DISABLE" : "ENABLE")}")
                    { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
                    using var p = Process.Start(psi)!;
                    p.WaitForExit(30000);
                    result.Ok = p.ExitCode == 0;
                    break;
                }
                case "clean-windows-temp":
                {
                    var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
                    var (ok, fail, _) = FileUtil.DeleteDirectoryChildren(dir);
                    result.Ok = ok > 0;
                    result.Message = $"ok={ok},fail={fail}";
                    break;
                }
                default:
                    throw new InvalidOperationException("unknown task");
            }
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.Message = ex.Message;
        }
        try
        {
            File.WriteAllText(resultFile,
                JsonSerializer.Serialize(result, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        }
        catch { }
    }
}

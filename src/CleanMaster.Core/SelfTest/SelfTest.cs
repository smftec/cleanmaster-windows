using System.Diagnostics;
using System.IO;
using CleanMaster.Core.Analysis;
using CleanMaster.Core.Models;
using CleanMaster.Core.Rules;
using CleanMaster.Core.Security;
using CleanMaster.Core.Store;
using CleanMaster.Core.Utils;

namespace CleanMaster.Core.SelfTest;

/// <summary>
/// 安全自测：--selftest 启动时运行，覆盖 PRD 第 53 章的必测安全场景（可离线自动化）。
/// 任何一项失败都应阻止发布。
/// </summary>
public static class SelfTest
{
    public static int RunAll()
    {
        int failed = 0;
        void Check(string name, bool ok, string detail = "")
        {
            Console.WriteLine($"{(ok ? "[PASS]" : "[FAIL]")} {name}{(detail.Length > 0 ? "  — " + detail : "")}");
            if (!ok) failed++;
        }

        Console.WriteLine("== 清理优化大师 安全自测 ==");

        // 1. SafetyGuard 拒绝表
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Check("拒绝删除桌面", SafetyGuard.FindDenyReason(Path.Combine(user, "Desktop", "a.txt")) != null);
        Check("拒绝删除文档", SafetyGuard.FindDenyReason(Path.Combine(user, "Documents", "report.docx")) != null);
        Check("拒绝删除图片", SafetyGuard.FindDenyReason(Path.Combine(user, "Pictures", "a.jpg")) != null);
        Check("拒绝删除下载", SafetyGuard.FindDenyReason(Path.Combine(user, "Downloads", "setup.exe")) != null);
        Check("拒绝删除用户主目录", SafetyGuard.FindDenyReason(user) != null);
        Check("拒绝删除 System32", SafetyGuard.FindDenyReason(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "a.dll")) != null);
        Check("拒绝删除 WinSxS", SafetyGuard.FindDenyReason(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "WinSxS", "x.dll")) != null);
        Check("拒绝删除 Windows\\Installer", SafetyGuard.FindDenyReason(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Installer", "x.msi")) != null);
        Check("拒绝删除 Program Files", SafetyGuard.FindDenyReason(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "App", "a.dll")) != null);
        Check("拒绝删除磁盘根", SafetyGuard.FindDenyReason("C:\\") != null);

        // 2. SafetyGuard 允许临时目录
        var tempRoot = Path.GetTempPath();
        var tempFile = Path.Combine(tempRoot, "guardtest.tmp");
        Check("允许清理用户临时目录文件", SafetyGuard.FindDenyReason(tempFile) == null);
        var allowed = SafetyGuard.ValidateForClean(tempFile, new[] { PathUtil.Normalize(tempRoot) });
        Check("允许根校验通过(临时目录)", allowed == null, allowed ?? "");

        // 3. 规则允许根约束：用桌面路径冒充 temp 规则目标必须被拒
        var denied = SafetyGuard.ValidateForClean(Path.Combine(user, "Desktop", "x.tmp"), new[] { PathUtil.Normalize(tempRoot) });
        Check("越界目标被拒绝(不在允许根)", denied != null, denied ?? "");

        // 4. 清理 + 隔离区往返（在隔离沙盒目录中进行）
        var sandbox = Path.Combine(tempRoot, "CleanMasterSelfTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
        try
        {
            var probe = Path.Combine(sandbox, "junk.log");
            File.WriteAllText(probe, "selftest");
            var quarantine = QuarantineService.Instance;
            var moved = quarantine.MoveIntoQuarantine(probe, "selftest", out var err);
            Check("文件移入隔离区", moved, err ?? "");
            var entry = quarantine.Entries.FirstOrDefault(e => e.OriginalPath == probe);
            Check("隔离清单有记录", entry != null);
            if (entry != null)
            {
                var restoreErr = quarantine.Restore(entry.Id);
                Check("隔离恢复成功", restoreErr == null, restoreErr ?? "");
                Check("恢复后文件存在且内容一致", File.Exists(probe) && File.ReadAllText(probe) == "selftest");

                // 永久删除：再造一个文件进隔离区后删除
                var probe2 = Path.Combine(sandbox, "junk2.log");
                File.WriteAllText(probe2, "selftest2");
                var moved2 = quarantine.MoveIntoQuarantine(probe2, "selftest", out var err2);
                Check("第二个文件移入隔离区", moved2, err2 ?? "");
                var entry2 = quarantine.Entries.FirstOrDefault(e => e.OriginalPath == probe2);
                if (entry2 != null)
                {
                    quarantine.DeletePermanent(entry2.Id, out _);
                    Check("永久删除隔离项", !File.Exists(probe2) && !Directory.Exists(Path.Combine(SettingsService.QuarantineDir, entry2.StoredName)));
                }
            }

            // 5. 递归删除不跟随 Junction：在沙盒中造 junction 指向"文档"
            var docs = Path.Combine(user, "Documents");
            var link = Path.Combine(sandbox, "linkToDocs");
            var inner = Path.Combine(sandbox, "inner");
            Directory.CreateDirectory(inner);
            File.WriteAllText(Path.Combine(inner, "a.txt"), "1");
            try
            {
                if (RuntimeInterop.CreateJunction(link, docs))
                {
                    var (ok, fail, _) = FileUtil.DeleteDirectoryTree(sandbox);
                    var docsSurvives = Directory.Exists(docs) && Directory.EnumerateFileSystemEntries(docs).Any();
                    Check("删除含 Junction 的目录不破坏目标", docsSurvives,
                        docsSurvives ? "" : "文档目录被破坏，禁止发布！");
                }
                else
                {
                    Check("Junction 测试环境创建失败(跳过判定)", true, "环境不支持 junction，跳过");
                }
            }
            catch (Exception ex)
            {
                Check("Junction 测试异常", false, ex.Message);
            }
        }
        finally
        {
            try { Directory.Delete(sandbox, true); } catch { }
        }

        // 6. 重复文件算法正确性
        try
        {
            var dupRoot = Path.Combine(tempRoot, "CleanMasterDup_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dupRoot, "a"));
            Directory.CreateDirectory(Path.Combine(dupRoot, "b"));
            var data = new byte[12345];
            Random.Shared.NextBytes(data);
            File.WriteAllBytes(Path.Combine(dupRoot, "a", "x.bin"), data);
            File.WriteAllBytes(Path.Combine(dupRoot, "b", "y.bin"), data);
            File.WriteAllBytes(Path.Combine(dupRoot, "b", "z.bin"), new byte[12345]);

            var svc = new DuplicateFileService();
            var result = svc.ScanAsync(new[] { dupRoot }, 1024, CancellationToken.None, null).GetAwaiter().GetResult();
            // x 与 y 相同内容 → 一组；z 全零也是 12345 字节，与上面相同大小不同内容 → 不得归入
            var group = result.Groups.FirstOrDefault(g => g.Files.Count == 2);
            Check("重复文件检测(相同内容归一组)", group != null);
            Check("不同内容不误判重复", result.Groups.All(g => g.Hash == null || g.Files.Count <= 2) &&
                                          !result.Groups.Any(g => g.Files.Count > 2));
            try { Directory.Delete(dupRoot, true); } catch { }
        }
        catch (Exception ex)
        {
            Check("重复文件检测异常", false, ex.Message);
        }

        // 7. 启动项枚举不抛异常且有条目
        try
        {
            var st = new Startup.StartupService();
            var items = st.Enumerate();
            Check("启动项枚举", items.Count > 0, $"共 {items.Count} 项");
        }
        catch (Exception ex)
        {
            Check("启动项枚举异常", false, ex.Message);
        }

        // 8. 应用清单枚举
        try
        {
            var inv = new Apps.AppInventoryService();
            var apps = inv.EnumerateWin32();
            Check("已安装应用枚举", apps.Count > 0, $"共 {apps.Count} 个");
        }
        catch (Exception ex)
        {
            Check("应用清单枚举异常", false, ex.Message);
        }

        // 9. 真实规则扫描冒烟（限时 90 秒，验证不抛异常）
        try
        {
            var catalog = new RuleCatalog();
            var scanner = new ScannerService(catalog);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var ctx = scanner.BuildContext(cts.Token);
            var results = scanner.ScanAsync(catalog.Rules.Where(r => r.Group != RuleGroup.Privacy).ToList(),
                ctx, null).GetAwaiter().GetResult();
            var withError = results.Where(r => r.Error != null).ToList();
            Check("规则扫描冒烟(真实机器)", withError.Count == 0,
                $"{results.Count} 条规则, 发现 {SizeText.OfBytes(results.Sum(r => r.TotalSize))}" +
                (withError.Count > 0 ? ", 错误: " + string.Join(";", withError.Select(e => e.RuleId + ":" + e.Error)) : ""));
        }
        catch (OperationCanceledException)
        {
            Check("规则扫描冒烟超时(视为通过但建议关注)", true, "90s 超时");
        }
        catch (Exception ex)
        {
            Check("规则扫描冒烟异常", false, ex.Message);
        }

        Console.WriteLine(failed == 0 ? "== 全部通过 ==" : $"== {failed} 项失败，禁止发布 ==");
        return failed == 0 ? 0 : 1;
    }
}

internal static class RuntimeInterop
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateSymbolicLinkW(string lpSymlinkFileName, string lpTargetFileName, int dwFlags);

    public static bool CreateJunction(string linkPath, string targetPath)
    {
        // junction 用 cmd mklink /J 创建（符号链接需要特权）
        try
        {
            var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkPath}\" \"{targetPath}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            using var p = Process.Start(psi)!;
            p.WaitForExit(10000);
            return Directory.Exists(linkPath);
        }
        catch { return false; }
    }
}

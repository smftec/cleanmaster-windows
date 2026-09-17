using System.IO;
using System.Text.Json;
using CleanMaster.Core.Models;
using CleanMaster.Core.Store;
using CleanMaster.Core.Utils;

namespace CleanMaster.Core.Rules;

public sealed class QuarantineEntry
{
    public Guid Id { get; set; }
    public string OriginalPath { get; set; } = "";
    public string StoredName { get; set; } = "";
    public long Size { get; set; }
    public string RuleId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ExpireAt { get; set; } = DateTime.Now.AddDays(30);
    public bool IsDirectory { get; set; }
}

/// <summary>
/// 清理隔离区：非纯缓存文件默认移入隔离区，保留 N 天可随时恢复，到期自动永久删除。
/// </summary>
public sealed class QuarantineService
{
    public static QuarantineService Instance { get; } = new();

    private static string Dir => SettingsService.QuarantineDir;
    private static string ManifestFile => Path.Combine(SettingsService.DataDir, "quarantine.json");

    private static readonly object Lock = new();
    private List<QuarantineEntry>? _entries;

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public IReadOnlyList<QuarantineEntry> Entries
    {
        get { lock (Lock) { Load(); return _entries!.ToList(); } }
    }

    public long TotalBytes => Entries.Sum(e => e.Size);

    private void Load()
    {
        if (_entries != null) return;
        try
        {
            _entries = File.Exists(ManifestFile)
                ? JsonSerializer.Deserialize<List<QuarantineEntry>>(File.ReadAllText(ManifestFile)) ?? new()
                : new();
        }
        catch { _entries = new(); }
    }

    private void Persist()
    {
        try
        {
            Directory.CreateDirectory(SettingsService.DataDir);
            File.WriteAllText(ManifestFile, JsonSerializer.Serialize(_entries, JsonOpt));
        }
        catch { }
    }

    /// <summary>把文件/目录移动到隔离区。返回是否成功。</summary>
    public bool MoveIntoQuarantine(string path, string ruleId, out string? error)
    {
        error = null;
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path)) { error = "文件不存在"; return false; }
            Directory.CreateDirectory(Dir);

            long size = 0;
            var isDir = Directory.Exists(path);
            if (isDir) size = FileUtil.DirSize(path);
            else size = new FileInfo(path).Length;

            var id = Guid.NewGuid();
            var storedName = id.ToString("N") + ".bin";
            var target = Path.Combine(Dir, storedName);
            if (isDir) Directory.Move(path, target);
            else File.Move(path, target);

            lock (Lock)
            {
                Load();
                _entries!.Add(new QuarantineEntry
                {
                    Id = id,
                    OriginalPath = path,
                    StoredName = storedName,
                    Size = size,
                    RuleId = ruleId,
                    CreatedAt = DateTime.Now,
                    ExpireAt = DateTime.Now.AddDays(Math.Max(1, SettingsService.Current.QuarantineDays)),
                    IsDirectory = isDir,
                });
                Persist();
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message.Contains("另一个程序正使用") || ex.Message.Contains("being used")
                ? "文件被占用"
                : ex.Message;
            return false;
        }
    }

    /// <summary>恢复到原始位置。原目录不存在会自动创建；重名自动加后缀。返回 null=成功，否则错误信息。</summary>
    public string? Restore(Guid id)
    {
        lock (Lock)
        {
            Load();
            var e = _entries!.FirstOrDefault(x => x.Id == id);
            if (e == null) return "记录不存在";
            var stored = Path.Combine(Dir, e.StoredName);
            if (!File.Exists(stored) && !Directory.Exists(stored)) return "隔离文件已丢失";

            try
            {
                var targetDir = System.IO.Path.GetDirectoryName(e.OriginalPath);
                if (!string.IsNullOrEmpty(targetDir)) Directory.CreateDirectory(targetDir);
                var target = e.OriginalPath;
                if (File.Exists(target) || Directory.Exists(target))
                {
                    var ext = System.IO.Path.GetExtension(target);
                    var stem = System.IO.Path.GetFileNameWithoutExtension(target);
                    var dir = System.IO.Path.GetDirectoryName(target);
                    target = Path.Combine(dir ?? "", $"{stem} (已恢复){ext}");
                    int n = 1;
                    while (File.Exists(target) || Directory.Exists(target))
                    {
                        target = Path.Combine(dir ?? "", $"{stem} (已恢复 {++n}){ext}");
                    }
                }
                if (e.IsDirectory) Directory.Move(stored, target);
                else File.Move(stored, target);

                _entries!.Remove(e);
                Persist();
                HistoryService.Add("Restore", $"恢复了 1 个隔离项目", e.OriginalPath, e.Size);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }

    public bool DeletePermanent(Guid id, out long size)
    {
        size = 0;
        lock (Lock)
        {
            Load();
            var e = _entries!.FirstOrDefault(x => x.Id == id);
            if (e == null) return false;
            try
            {
                var stored = Path.Combine(Dir, e.StoredName);
                size = e.Size;
                if (Directory.Exists(stored)) Directory.Delete(stored, true);
                else if (File.Exists(stored)) File.Delete(stored);
            }
            catch { }
            _entries!.Remove(e);
            Persist();
            return true;
        }
    }

    /// <summary>启动时调用：永久删除到期项目。</summary>
    public void PurgeExpired()
    {
        lock (Lock)
        {
            Load();
            var expired = _entries!.Where(e => e.ExpireAt <= DateTime.Now).ToList();
            foreach (var e in expired)
            {
                try
                {
                    var stored = Path.Combine(Dir, e.StoredName);
                    if (Directory.Exists(stored)) Directory.Delete(stored, true);
                    else if (File.Exists(stored)) File.Delete(stored);
                    _entries!.Remove(e);
                }
                catch { }
            }
            if (expired.Count > 0) Persist();
        }
    }

    public int PurgeAll()
    {
        lock (Lock)
        {
            Load();
            int n = _entries!.Count;
            foreach (var e in _entries.ToList())
            {
                try
                {
                    var stored = Path.Combine(Dir, e.StoredName);
                    if (Directory.Exists(stored)) Directory.Delete(stored, true);
                    else if (File.Exists(stored)) File.Delete(stored);
                }
                catch { }
            }
            _entries!.Clear();
            Persist();
            if (n > 0) HistoryService.Add("QuarantinePurge", $"清空了隔离区（{n} 个项目）");
            return n;
        }
    }
}

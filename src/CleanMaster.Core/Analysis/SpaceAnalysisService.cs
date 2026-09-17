using System.Collections.Concurrent;
using System.IO;
using CleanMaster.Core.Store;
using CleanMaster.Core.Utils;

namespace CleanMaster.Core.Analysis;

public enum FileCategory { System, Apps, Images, Videos, Audio, Documents, Archives, Other }

public static class CategoryText
{
    public static string Of(FileCategory c) => c switch
    {
        FileCategory.System => "系统",
        FileCategory.Apps => "应用",
        FileCategory.Images => "图片",
        FileCategory.Videos => "视频",
        FileCategory.Audio => "音频",
        FileCategory.Documents => "文档",
        FileCategory.Archives => "压缩包",
        _ => "其他",
    };

    /// <summary>类别展示色（与首页视觉一致）。</summary>
    public static string HexOf(FileCategory c) => c switch
    {
        FileCategory.System => "#3B82F6",
        FileCategory.Apps => "#14B8A6",
        FileCategory.Images => "#8B5CF6",
        FileCategory.Videos => "#F97316",
        FileCategory.Documents => "#EC4899",
        FileCategory.Archives => "#EAB308",
        _ => "#94A3B8",
    };
}

public sealed class CategoryStat
{
    public FileCategory Category { get; set; }
    public long Bytes { get; set; }
    public int Files { get; set; }
    public double Percent { get; set; }
}

public sealed class FolderNode
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public long Size { get; set; }
    public int FileCount { get; set; }
    public double PercentOfParent { get; set; }
    public List<FolderNode> Children { get; } = new();
    public bool IsInaccessible { get; set; }
}

public sealed class LargeFileInfo
{
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public DateTime ModifiedTime { get; set; }
    public bool Whitelisted { get; set; }
}

public sealed class DriveAnalysis
{
    public string DriveName { get; set; } = "";
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long UsedBytes { get; set; }
    public double UsedPercent { get; set; }
    public long ScannedBytes { get; set; }
    public int ScannedFiles { get; set; }
    public TimeSpan Duration { get; set; }
    public List<CategoryStat> Categories { get; } = new();
    public FolderNode Root { get; set; } = new();
    public List<LargeFileInfo> LargeFiles { get; } = new();
    public bool Completed { get; set; }

    /// <summary>目录大小索引（供目录树懒展开），仅内存持有。</summary>
    internal ConcurrentDictionary<string, (long size, int files)>? DirSizes { get; set; }

    /// <summary>取某目录下按大小排序的子目录（懒展开用）。</summary>
    public List<FolderNode> GetChildren(string path, int top = 40)
    {
        var result = new List<FolderNode>();
        if (DirSizes == null) return result;
        try
        {
            if (!Directory.Exists(path)) return result;
            foreach (var d in Directory.GetDirectories(path))
            {
                try
                {
                    if (PathUtil.IsReparsePoint(d)) continue;
                    if (!DirSizes.TryGetValue(d, out var v)) continue;
                    result.Add(new FolderNode
                    {
                        Name = System.IO.Path.GetFileName(d),
                        FullPath = d,
                        Size = v.size,
                        FileCount = v.files,
                    });
                }
                catch { }
            }
        }
        catch { }
        return result.OrderByDescending(n => n.Size).Take(top).ToList();
    }
}

/// <summary>
/// 多线程磁盘分析：统计分类占用、目录树与Top大文件。
/// 不进入 Reparse Point；跳过无权限目录；支持暂停/取消。
/// </summary>
public sealed class SpaceAnalysisService
{
    public sealed class ProgressInfo
    {
        public string CurrentDir { get; set; } = "";
        public long ScannedBytes { get; set; }
        public int ScannedFiles { get; set; }
    }

    public async Task<DriveAnalysis> AnalyzeAsync(
        string driveRoot,
        long largeFileThreshold,
        CancellationToken ct,
        IProgress<ProgressInfo>? progress,
        Action? onPausePoint = null)
    {
        return await Task.Run(() =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var drive = new DriveInfo(driveRoot);
            var analysis = new DriveAnalysis
            {
                DriveName = drive.Name,
                TotalBytes = drive.TotalSize,
                FreeBytes = drive.AvailableFreeSpace,
                UsedBytes = drive.TotalSize - drive.AvailableFreeSpace,
                UsedPercent = drive.TotalSize > 0 ? (drive.TotalSize - drive.AvailableFreeSpace) * 100.0 / drive.TotalSize : 0,
            };

            var dirSizes = new ConcurrentDictionary<string, (long size, int files)>();
            var dirsQueue = new ConcurrentQueue<string>();
            var settings = SettingsService.Current;
            var excluded = settings.ExcludedPaths;
            var largeFiles = new ConcurrentBag<LargeFileInfo>();
            long scannedBytes = 0;
            int scannedFiles = 0;
            int threads = Math.Clamp(settings.ScanThreads, 2, 32);

            dirsQueue.Enqueue(driveRoot);
            int activeWorkers = threads;
            long _lastReportTick = 0;
            var workers = new Thread[threads];
            for (int i = 0; i < threads; i++)
            {
                var worker = new Thread(() => WorkerLoop());
                worker.IsBackground = true;
                workers[i] = worker;
                worker.Start();
            }

            void WorkerLoop()
            {
                while (dirsQueue.TryDequeue(out var dir))
                {
                    ct.ThrowIfCancellationRequested();
                    long size = 0, files = 0;
                    string[] subDirs = Array.Empty<string>();
                    try
                    {
                        if (PathUtil.IsReparsePoint(dir)) { dirSizes[dir] = (0, 0); goto done; }
                        var di = new DirectoryInfo(dir);
                        foreach (var f in di.EnumerateFiles())
                        {
                            try
                            {
                                var len = f.Length;
                                size += len; files++;
                                if (len >= largeFileThreshold && !IsExcluded(f.FullName))
                                    largeFiles.Add(new LargeFileInfo
                                    {
                                        Path = f.FullName,
                                        Size = len,
                                        ModifiedTime = f.LastWriteTime,
                                    });
                            }
                            catch { }
                        }
                        subDirs = di.GetDirectories().Select(d => d.FullName).ToArray();
                    }
                    catch
                    {
                        dirSizes[dir] = (size, (int)files);
                        goto done;
                    }
                    dirSizes[dir] = (size, (int)files);
                    foreach (var sub in subDirs)
                    {
                        if (PathUtil.IsReparsePoint(sub)) continue;
                        if (IsExcluded(sub)) continue;
                        dirsQueue.Enqueue(sub);
                    }
                done:
                    Interlocked.Add(ref scannedBytes, size);
                    var fc = (int)files;
                    Interlocked.Add(ref scannedFiles, fc);
                    // 限频：约 5 次/秒，避免刷爆 UI 线程
                    var now = Environment.TickCount64;
                    var last = Volatile.Read(ref _lastReportTick);
                    if (now - last > 200 && Interlocked.CompareExchange(ref _lastReportTick, now, last) == last)
                    {
                        progress?.Report(new ProgressInfo
                        {
                            CurrentDir = dir,
                            ScannedBytes = Interlocked.Read(ref scannedBytes),
                            ScannedFiles = scannedFiles,
                        });
                    }
                }
                Interlocked.Decrement(ref activeWorkers);
            }

            // 等待所有 worker 退出（队列耗尽后各自退出）
            while (Volatile.Read(ref activeWorkers) > 0)
            {
                ct.ThrowIfCancellationRequested();
                Thread.Sleep(60);
            }

            analysis.ScannedBytes = scannedBytes;
            analysis.DirSizes = dirSizes;
            analysis.ScannedFiles = scannedFiles;
            analysis.Duration = sw.Elapsed;
            analysis.Completed = true;

            // 大文件排序取 Top 1000
            foreach (var f in largeFiles.OrderByDescending(f => f.Size).Take(1000))
                analysis.LargeFiles.Add(f);

            // 自底向上聚合目录大小（先快照，避免边遍历边修改）
            // 聚合前的"直接大小"用于分类统计，避免子目录重复计入
            var directSizes = dirSizes.ToArray()
                .Select(kv => (path: kv.Key, v: kv.Value))
                .ToArray();
            foreach (var kv in directSizes)
            {
                // 向上传播
                var cur = kv.path;
                long addSize = kv.v.size;
                int addFiles = kv.v.files;
                while (true)
                {
                    var parent = Directory.GetParent(cur)?.FullName;
                    if (parent == null || !dirSizes.ContainsKey(parent)) break;
                    if (parent != kv.path)
                    {
                        dirSizes[parent] = (dirSizes[parent].size + addSize, dirSizes[parent].files + addFiles);
                    }
                    cur = parent;
                }
            }

            // 构建目录树
            analysis.Root = BuildTree(driveRoot, dirSizes);

            // 分类统计：用聚合前的直接大小，每个文件的字节只计一次
            BuildCategories(analysis, directSizes);
            return analysis;
        }, ct);
    }

    private static bool IsExcluded(string path)
    {
        var s = SettingsService.Current;
        foreach (var e in s.ExcludedPaths)
            if (PathUtil.IsUnder(path, e)) return true;
        return false;
    }

    private static FolderNode BuildTree(string rootPath, ConcurrentDictionary<string, (long size, int files)> dirSizes)
    {
        var root = new FolderNode
        {
            Name = rootPath,
            FullPath = rootPath,
        };
        if (dirSizes.TryGetValue(rootPath, out var rv))
        {
            root.Size = rv.size;
            root.FileCount = rv.files;
        }
        AddChildren(root, dirSizes);
        return root;

        static void AddChildren(FolderNode node, ConcurrentDictionary<string, (long size, int files)> sizes)
        {
            try
            {
                var children = Directory.GetDirectories(node.FullPath)
                    .Where(d => !PathUtil.IsReparsePoint(d))
                    .Select(d =>
                    {
                        var n = new FolderNode
                        {
                            FullPath = d,
                            Name = System.IO.Path.GetFileName(d),
                            IsInaccessible = !sizes.ContainsKey(d),
                        };
                        if (sizes.TryGetValue(d, out var v))
                        {
                            n.Size = v.size;
                            n.FileCount = v.files;
                        }
                        return n;
                    })
                    .Where(n => n.Size > 0 || n.IsInaccessible)
                    .OrderByDescending(n => n.Size)
                    .Take(40)
                    .ToList();
                var total = children.Sum(c => c.Size);
                foreach (var c in children)
                    c.PercentOfParent = total > 0 ? c.Size * 100.0 / total : 0;
                node.Children.AddRange(children);
            }
            catch { }
        }
    }

    private static void BuildCategories(DriveAnalysis analysis, (string path, (long size, int files) v)[] directSizes)
    {
        var stats = new Dictionary<FileCategory, CategoryStat>();
        foreach (var (path, v) in directSizes)
        {
            if (v.size <= 0 && v.files <= 0) continue;
            var cat = ClassifyDir(path);
            if (!stats.TryGetValue(cat, out var st))
                stats[cat] = st = new CategoryStat { Category = cat };
            st.Bytes += v.size;
            st.Files += v.files;
        }
        long total = stats.Values.Sum(s => s.Bytes);
        foreach (var s in stats.Values.OrderByDescending(s => s.Bytes))
        {
            s.Percent = total > 0 ? s.Bytes * 100.0 / total : 0;
            analysis.Categories.Add(s);
        }
    }

    private static FileCategory ClassifyDir(string dir)
    {
        var rel = dir;
        if (rel.EndsWith('\\')) rel = rel[..^1];
        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var progdata = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var appdataLocal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appdataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // 系统目录 → 系统
        if (PathUtil.IsUnder(rel, windir)) return FileCategory.System;
        // 程序目录与 AppData → 应用
        if (PathUtil.IsUnder(rel, pf) || PathUtil.IsUnder(rel, pf86) || PathUtil.IsUnder(rel, progdata))
            return FileCategory.Apps;
        if (PathUtil.IsUnder(rel, appdataLocal) || PathUtil.IsUnder(rel, appdataRoaming))
            return FileCategory.Apps;

        // 用户目录下按扩展名聚合困难（这是目录级统计），对用户已知媒体目录做路径判断，
        // 其余用户文件在"按文件类型"里展示；目录级粗略归入其他。
        var relToUser = PathUtil.IsUnder(rel, userProfile);
        if (relToUser)
        {
            var seg = rel.Substring(userProfile.Length).TrimStart('\\').Split('\\')[0];
            if (seg.Equals("Pictures", StringComparison.OrdinalIgnoreCase)) return FileCategory.Images;
            if (seg.Equals("Videos", StringComparison.OrdinalIgnoreCase)) return FileCategory.Videos;
            if (seg.Equals("Music", StringComparison.OrdinalIgnoreCase)) return FileCategory.Audio;
            if (seg.Equals("Documents", StringComparison.OrdinalIgnoreCase)) return FileCategory.Documents;
            if (seg.Equals("Downloads", StringComparison.OrdinalIgnoreCase)) return FileCategory.Archives;
        }
        // 一级目录按名字猜
        var name = System.IO.Path.GetFileName(rel);
        if (name.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Windows", StringComparison.OrdinalIgnoreCase))
            return FileCategory.System;
        return FileCategory.Other;
    }

    /// <summary>文件级类型统计（对大文件列表聚合，轻量快速）。</summary>
    public static List<CategoryStat> SummarizeByExtension(IEnumerable<LargeFileInfo> files)
    {
        var map = new Dictionary<FileCategory, CategoryStat>();
        foreach (var f in files)
        {
            var cat = ClassifyFile(f.Path);
            if (!map.TryGetValue(cat, out var st))
                map[cat] = st = new CategoryStat { Category = cat };
            st.Bytes += f.Size;
            st.Files++;
        }
        return map.Values.OrderByDescending(s => s.Bytes).ToList();
    }

    public static FileCategory ClassifyFile(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" or ".heic" or ".raw" or ".svg" => FileCategory.Images,
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".flv" or ".m4v" or ".ts" or ".rmvb" => FileCategory.Videos,
            ".mp3" or ".wav" or ".flac" or ".ape" or ".aac" or ".ogg" or ".m4a" => FileCategory.Audio,
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".iso" => FileCategory.Archives,
            ".exe" or ".msi" or ".dll" or ".sys" => FileCategory.System,
            ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".pdf" or ".txt" or ".md" or ".csv" or ".epub" => FileCategory.Documents,
            _ => FileCategory.Other,
        };
    }
}

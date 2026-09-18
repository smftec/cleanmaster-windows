using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CleanMaster.Core.Analysis;
using CleanMaster.Core.Rules;
using CleanMaster.Core.Store;
using CleanMaster.Core.Utils;
using Microsoft.Win32;

namespace CleanMaster.App.Pages;

public partial class ToolboxPage : Page, IParamPage
{
    private readonly MainWindow _main;
    private DuplicateScanResult? _dupResult;

    public ToolboxPage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        Loaded += (s, e) => { BuildTools(); RefreshQuarantine(); RefreshHistory(); };
    }

    public void OnNavigated(object? param)
    {
        RefreshQuarantine();
        RefreshHistory();
        if (param as string == "quarantine")
            QuarantineCard.BringIntoView();
        else if (param as string == "history")
            HistoryCard.BringIntoView();
        else if (param as string == "dup")
        {
            Dispatcher.BeginInvoke(() => DuplicateCard.BringIntoView(),
                System.Windows.Threading.DispatcherPriority.Loaded);
            BtnDupScan_Click(this, new RoutedEventArgs());
        }
    }

    // ————— 隔离区 —————

    private void RefreshQuarantine()
    {
        var entries = QuarantineService.Instance.Entries;
        QuarantineList.Children.Clear();
        long total = entries.Sum(e => e.Size);
        QuarantineSummaryText.Text = entries.Count == 0
            ? Services.Loc.T("tool.quar.empty")
            : string.Format(Services.Loc.T("tool.quar.summary"), entries.Count, SizeText.OfBytes(total), SettingsService.Current.QuarantineDays);
        BtnPurgeQuarantine.IsEnabled = entries.Count > 0;

        foreach (var e in entries.OrderByDescending(x => x.CreatedAt))
        {
            QuarantineList.Children.Add(BuildQuarantineRow(e));
        }
    }

    private Border BuildQuarantineRow(QuarantineEntry e)
    {
        var row = new Border
        {
            Background = (Brush)FindResource("ItemHoverBrush"),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(12, 9, 12, 9),
            Margin = new Thickness(0, 4, 0, 0),
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var name = System.IO.Path.GetFileName(e.OriginalPath.TrimEnd('\\'));
        sp.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 12.5,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        var meta = new TextBlock
        {
            Text = $"{e.OriginalPath} · {e.CreatedAt:yyyy-MM-dd HH:mm} 入库",
            FontSize = 11,
            Foreground = (Brush)FindResource("TextTertiaryBrush"),
            Margin = new Thickness(0, 2, 0, 0),
        };
        ToolTipService.SetToolTip(meta, e.OriginalPath);
        sp.Children.Add(meta);
        Grid.SetColumn(sp, 0);

        var size = new TextBlock
        {
            Text = SizeText.OfBytes(e.Size),
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(size, 1);

        var remain = new TextBlock
        {
            Text = string.Format(Services.Loc.T("tool.quar.daysleft"), Math.Max(0, (e.ExpireAt - DateTime.Now).Days)),
            FontSize = 11,
            Foreground = (Brush)FindResource("TextTertiaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
        };
        Grid.SetColumn(remain, 2);

        var restoreBtn = new Button
        {
            Content = Services.Loc.T("btn.restore"),
            Style = (Style)FindResource("SmallPrimaryButton"),
            Margin = new Thickness(14, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        restoreBtn.Click += (s, a) =>
        {
            var err = QuarantineService.Instance.Restore(e.Id);
            if (err == null) _main.ShowToast(string.Format(Services.Loc.T("toast.restored"), name));
            else _main.ShowToast(Services.Loc.T("toast.restorefailed") + err, ToastType.Warning);
            RefreshQuarantine();
        };
        Grid.SetColumn(restoreBtn, 3);

        var delBtn = new Button
        {
            Content = Services.Loc.T("btn.deleteperm"),
            Style = (Style)FindResource("SmallSecondaryButton"),
            Foreground = (Brush)FindResource("DangerBrush"),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        delBtn.Click += (s, a) =>
        {
            if (!Services.DialogService.Confirm(_main, Services.Loc.T("dlg.deleteperm.title"), string.Format(Services.Loc.T("dlg.deleteperm.msg"), name), Services.Loc.T("btn.deleteperm"), Services.Loc.T("btn.cancel"), danger: true))
                return;
            QuarantineService.Instance.DeletePermanent(e.Id, out _);
            RefreshQuarantine();
        };
        Grid.SetColumn(delBtn, 4);

        grid.Children.Add(sp);
        grid.Children.Add(size);
        grid.Children.Add(remain);
        grid.Children.Add(restoreBtn);
        grid.Children.Add(delBtn);
        row.Child = grid;
        return row;
    }

    private void BtnPurgeQuarantine_Click(object sender, RoutedEventArgs e)
    {
        var n = QuarantineService.Instance.Entries.Count;
        if (n == 0) return;
        if (!Services.DialogService.Confirm(_main, Services.Loc.T("tool.purge.title"),
            string.Format(Services.Loc.T("tool.purge.msg"), n), Services.Loc.T("btn.deleteperm"), Services.Loc.T("btn.cancel"), danger: true))
            return;
        QuarantineService.Instance.PurgeAll();
        RefreshQuarantine();
        _main.ShowToast(Services.Loc.T("toast.purgedone"));
    }

    // ————— 文件粉碎 —————

    private void BtnShredFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "选择要粉碎的文件", Multiselect = false };
        if (dlg.ShowDialog() != true) return;
        Shred(dlg.FileName, isDir: false);
    }

    private void BtnShredDir_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "选择要粉碎的文件夹" };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        Shred(dlg.SelectedPath, isDir: true);
    }

    private async void Shred(string path, bool isDir)
    {
        var size = isDir ? FileUtil.DirSize(path) : new FileInfo(path).Length;
        if (!Services.DialogService.Confirm(_main, Services.Loc.T("tool.shred.confirm.title"),
            string.Format(Services.Loc.T("tool.shred.confirm.msg"), path, SizeText.OfBytes(size)),
            Services.Loc.T("btn.shred"), Services.Loc.T("btn.cancel"), danger: true))
            return;

        BtnShredOK();
        var ok = await Task.Run(() => Shredder.Shred(path));
        HistoryService.Add("Shred", string.Format(Services.Loc.T("tool.shred.log"), isDir ? Services.Loc.T("tool.folder") : Services.Loc.T("tool.file"), System.IO.Path.GetFileName(path)), path, size);
        _main.ShowToast(ok ? Services.Loc.T("toast.shredok") : Services.Loc.T("toast.shrefail"), ok ? ToastType.Success : ToastType.Warning);
    }

    private void BtnShredOK() { }

    // ————— 重复文件 —————

    private async void BtnDupScan_Click(object sender, RoutedEventArgs e)
    {
        var roots = SettingsService.Current.DuplicateScanDirs;
        if (roots.Count == 0)
        {
            roots = DefaultDupDirs();
            SettingsService.Current.DuplicateScanDirs = roots;
            SettingsService.Save();
        }
        roots = roots.Where(Directory.Exists).ToList();
        if (roots.Count == 0)
        {
            _main.ShowToast("没有可扫描的目录", ToastType.Info);
            return;
        }

        BtnDupScan.IsEnabled = false;
        BtnDupScan.Content = "扫描中…";
        DupList.Children.Clear();
        DupSummaryText.Text = Services.Loc.T("common.scanning");
        try
        {
            var svc = new DuplicateFileService();
            _dupResult = await svc.ScanAsync(roots, 1024 * 1024, CancellationToken.None,
                new Progress<(string dir, int files)>(p => DupSummaryText.Text = string.Format(Services.Loc.T("tool.dup.progress"), p.files)));
        }
        catch (Exception ex)
        {
            _main.ShowToast(Services.Loc.T("toast.scanfailed") + ex.Message, ToastType.Warning);
            _dupResult = null;
        }
        BtnDupScan.IsEnabled = true;
        BtnDupScan.Content = Services.Loc.T("btn.dup.rescan");
        RenderDup();
    }

    private static List<string> DefaultDupDirs()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new[] { "Pictures", "Videos", "Downloads" }
            .Select(d => Path.Combine(profile, d))
            .Where(Directory.Exists)
            .ToList();
    }

    private void RenderDup()
    {
        DupList.Children.Clear();
        BtnDupKeepNewest.Visibility = Visibility.Collapsed;
        BtnDupClean.Visibility = Visibility.Collapsed;
        if (_dupResult == null) return;
        var groups = _dupResult.Groups.Where(g => g.Files.Count > 1).ToList();
        DupSummaryText.Text = groups.Count == 0
            ? Services.Loc.T("tool.dup.none")
            : string.Format(Services.Loc.T("tool.dup.summary"), groups.Count, SizeText.OfBytes(_dupResult.TotalWastedBytes));
        if (groups.Count == 0) return;
        BtnDupKeepNewest.Visibility = Visibility.Visible;
        BtnDupClean.Visibility = Visibility.Visible;

        foreach (var g in groups.Take(50))
        {
            DupList.Children.Add(BuildDupGroup(g));
        }
        if (groups.Count > 50)
        {
            DupList.Children.Add(new TextBlock
            {
                Text = string.Format(Services.Loc.T("tool.dup.more"), groups.Count - 50),
                FontSize = 12,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                Margin = new Thickness(0, 8, 0, 0),
            });
        }
    }

    private Border BuildDupGroup(DuplicateGroup g)
    {
        var card = new Border
        {
            Background = (Brush)FindResource("ItemHoverBrush"),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 6, 0, 0),
        };
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = string.Format(Services.Loc.T("tool.dup.group"), g.Files.Count, SizeText.OfBytes(g.Size), SizeText.OfBytes(g.WastedBytes)),
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
        });
        foreach (var f in g.Files)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            var cb = new CheckBox
            {
                Style = (Style)FindResource("RoundCheckOnly"),
                IsChecked = f.Selected,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = Services.Loc.T("tool.dup.tip"),
            };
            cb.Checked += (s, e) => f.Selected = true;
            cb.Unchecked += (s, e) => f.Selected = false;
            line.Children.Add(cb);
            var sp2 = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            sp2.Children.Add(new TextBlock
            {
                Text = System.IO.Path.GetFileName(f.Path),
                FontSize = 12,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
            });
            var path = new TextBlock
            {
                Text = f.Path + $" · {f.ModifiedTime:yyyy-MM-dd HH:mm}",
                FontSize = 11,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 760,
            };
            ToolTipService.SetToolTip(path, f.Path);
            sp2.Children.Add(path);
            line.Children.Add(sp2);
            sp.Children.Add(line);
        }
        card.Child = sp;
        return card;
    }

    private void BtnDupKeepNewest_Click(object sender, RoutedEventArgs e)
    {
        if (_dupResult == null) return;
        DuplicateFileService.AutoSelect(_dupResult.Groups.Where(g => g.Files.Count > 1), keepNewest: true);
        RenderDup();
    }

    private async void BtnDupClean_Click(object sender, RoutedEventArgs e)
    {
        if (_dupResult == null) return;
        var selected = _dupResult.Groups.SelectMany(g => g.Files).Where(f => f.Selected).ToList();
        if (selected.Count == 0)
        {
            _main.ShowToast("请先勾选要删除的副本", ToastType.Info);
            return;
        }
        long bytes = selected.Sum(f => f.Size);
        if (!Services.DialogService.Confirm(_main, Services.Loc.T("tool.dup.clean.title"),
            string.Format(Services.Loc.T("tool.dup.clean.msg"), selected.Count, SizeText.OfBytes(bytes)), Services.Loc.T("btn.recycle")))
            return;
        int ok = 0;
        await Task.Run(() =>
        {
            foreach (var f in selected)
            {
                if (ShellUtil.RecycleToBin(f.Path)) ok++;
            }
        });
        HistoryService.Add("Clean", $"删除了 {ok} 个重复文件", "", bytes);
        _main.ShowToast(string.Format(Services.Loc.T("toast.dupcleaned"), ok, SizeText.OfBytes(bytes)));
        // 重新扫描更新
        BtnDupScan_Click(sender, e);
    }

    // ————— 历史 —————

    private void RefreshHistory()
    {
        HistoryList.Children.Clear();
        var acts = HistoryService.LoadAll().Take(30).ToList();
        if (acts.Count == 0)
        {
            HistoryList.Children.Add(new TextBlock
            {
                Text = Services.Loc.T("history.empty"),
                FontSize = 12,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
            });
            return;
        }
        foreach (var a in acts)
        {
            var line = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var typeChip = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 2, 8, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Background = (Brush)FindResource("PrimarySoftBrush"),
            };
            typeChip.Child = new TextBlock
            {
                Text = HistoryTypeText.Of(a.Type),
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("PrimaryBrush"),
            };
            Grid.SetColumn(typeChip, 0);

            var title = new TextBlock
            {
                Text = a.Title + (string.IsNullOrEmpty(a.Detail) ? "" : $"（{a.Detail}）"),
                FontSize = 12.5,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetColumn(title, 1);

            var bytes = new TextBlock
            {
                Text = a.Bytes > 0 ? SizeText.OfBytes(a.Bytes) : "",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("SuccessBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0),
            };
            Grid.SetColumn(bytes, 2);

            var time = new TextBlock
            {
                Text = a.Time.ToString("MM-dd HH:mm"),
                FontSize = 11,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(time, 3);

            line.Children.Add(typeChip);
            line.Children.Add(title);
            line.Children.Add(bytes);
            line.Children.Add(time);
            HistoryList.Children.Add(line);
        }
    }

    private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (!Services.DialogService.Confirm(_main, Services.Loc.T("history.clear.title"), Services.Loc.T("history.clear.msg"), Services.Loc.T("btn.clearhistory")))
            return;
        HistoryService.Clear();
        RefreshHistory();
    }

    // ————— Windows 快捷工具 —————

    private void BuildTools()
    {
        if (ToolGrid.Children.Count > 0) return;
        void AddTool(string name, string desc, Action launch)
        {
            var btn = new Button
            {
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 10, 10),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            btn.Style = (Style)FindResource("SecondaryButton");
            var sp = new StackPanel { Margin = new Thickness(14, 12, 8, 12) };
            sp.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
            });
            sp.Children.Add(new TextBlock
            {
                Text = desc,
                FontSize = 11,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                Margin = new Thickness(0, 3, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            btn.Content = sp;
            btn.Click += (s, e) => { try { launch(); } catch (Exception ex) { _main.ShowToast("无法打开：" + ex.Message, ToastType.Warning); } };
            ToolGrid.Children.Add(btn);
        }

        AddTool(Services.Loc.T("tool.win.taskmgr"), Services.Loc.T("tool.win.taskmgr.d"), () => Process.Start("taskmgr.exe"));
        AddTool(Services.Loc.T("tool.win.storage"), Services.Loc.T("tool.win.storage.d"), () => Process.Start(new ProcessStartInfo("ms-settings:storagesense") { UseShellExecute = true }));
        AddTool(Services.Loc.T("tool.win.diskmgmt"), Services.Loc.T("tool.win.diskmgmt.d"), () => Process.Start("diskmgmt.msc"));
        AddTool(Services.Loc.T("tool.win.devmgmt"), Services.Loc.T("tool.win.devmgmt.d"), () => Process.Start("devmgmt.msc"));
        AddTool(Services.Loc.T("tool.win.control"), Services.Loc.T("tool.win.control.d"), () => Process.Start("control.exe"));
        AddTool(Services.Loc.T("tool.win.msinfo"), Services.Loc.T("tool.win.msinfo.d"), () => Process.Start("msinfo32.exe"));
        AddTool(Services.Loc.T("tool.win.update"), Services.Loc.T("tool.win.update.d"), () => Process.Start(new ProcessStartInfo("ms-settings:windowsupdate") { UseShellExecute = true }));
        AddTool(Services.Loc.T("tool.win.restore"), Services.Loc.T("tool.win.restore.d"), () => Process.Start("rstrui.exe"));
    }
}

/// <summary>文件粉碎器。</summary>
public static class Shredder
{
    public static bool Shred(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                ShredFile(path);
                return true;
            }
            if (Directory.Exists(path))
            {
                bool all = true;
                foreach (var f in SafeWalk(path))
                {
                    try { ShredFile(f); } catch { all = false; }
                }
                try { Directory.Delete(path, true); } catch { all = false; }
                return all;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static void ShredFile(string file)
    {
        try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
        using var fs = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.None);
        long length = fs.Length;
        var buf = new byte[1024 * 1024];
        var rnd = Random.Shared;
        // 3 次覆写：随机 → 零 → 随机
        for (int pass = 0; pass < 3; pass++)
        {
            fs.Position = 0;
            long remaining = length;
            while (remaining > 0)
            {
                int n = (int)Math.Min(buf.Length, remaining);
                if (pass == 1)
                    Array.Clear(buf, 0, n);
                else
                    rnd.NextBytes(buf.AsSpan(0, n));
                fs.Write(buf, 0, n);
                remaining -= n;
            }
            fs.Flush();
        }
        fs.SetLength(0);
        fs.Close();
        File.Delete(file);
    }

    private static IEnumerable<string> SafeWalk(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var d = stack.Pop();
            string[] files = Array.Empty<string>(), dirs = Array.Empty<string>();
            try { files = Directory.GetFiles(d); dirs = Directory.GetDirectories(d); } catch { continue; }
            foreach (var f in files) yield return f;
            foreach (var sub in dirs) stack.Push(sub);
        }
    }
}

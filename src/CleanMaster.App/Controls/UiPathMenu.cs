using System.Windows;
using System.Windows.Controls;
using CleanMaster.Core.Utils;

namespace CleanMaster.App.Controls;

/// <summary>文件/目录路径右键菜单：打开位置、打开、复制路径。</summary>
public static class UiPathMenu
{
    public static ContextMenu Build(string path)
    {
        var menu = new ContextMenu();
        var mi1 = new MenuItem { Header = Services.Loc.T("ctx.openloc") };
        mi1.Click += (s, e) => { try { ShellUtil.ShowInExplorer(path); } catch { } };
        var mi2 = new MenuItem { Header = Services.Loc.T("ctx.open") };
        mi2.Click += (s, e) => { try { ShellUtil.OpenPath(path); } catch { } };
        var mi3 = new MenuItem { Header = Services.Loc.T("ctx.copypath") };
        mi3.Click += (s, e) =>
        {
            try { System.Windows.Clipboard.SetText(path); } catch { }
        };
        menu.Items.Add(mi1);
        menu.Items.Add(mi2);
        menu.Items.Add(mi3);
        return menu;
    }

    /// <summary>给元素挂上路径菜单。</summary>
    public static void Attach(FrameworkElement el, string path)
        => el.ContextMenu = Build(path);
}

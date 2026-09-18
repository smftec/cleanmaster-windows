using System.Windows;
using System.Windows.Input;
using CleanMaster.Core.Store;

namespace CleanMaster.App.Windows;

public partial class FirstRunWizard : Window
{
    private int _step = 1;

    public FirstRunWizard()
    {
        InitializeComponent();
        MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        _step++;
        if (_step == 2)
        {
            Step1.Visibility = Visibility.Collapsed;
            Step2.Visibility = Visibility.Visible;
            Dot2.Fill = (System.Windows.Media.Brush)FindResource("PrimaryBrush");
            BtnNext.Content = Services.Loc.T("wizard.next2");
        }
        else if (_step == 3)
        {
            Step2.Visibility = Visibility.Collapsed;
            Step3.Visibility = Visibility.Visible;
            Dot3.Fill = (System.Windows.Media.Brush)FindResource("PrimaryBrush");
            BtnNext.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnAutoYes_Click(object sender, RoutedEventArgs e)
    {
        SettingsService.Current.AutoCleanEnabled = true;
        SettingsService.Save();
        Finish();
    }

    private void BtnAutoNo_Click(object sender, RoutedEventArgs e) => Finish();

    private void Finish()
    {
        FirstRunWizard_MarkWelcomed();
        Close();
    }

    private void FirstRunWizard_MarkWelcomed() => Services.FirstRunWizard.MarkWelcomed();
}

using System;
using System.ComponentModel;
using System.Windows;
using Luma.Services;
namespace Luma.Views;

public partial class InstallWindow : Window
{
    private InstallPlan? installed;
    private bool installing;
    public InstallWindow() { InitializeComponent(); InstallPathText.Text = InstallService.DefaultRoot; }
    private async void InstallNow_Click(object sender, RoutedEventArgs e)
    {
        InstallNowButton.IsEnabled = false;
        installing = true;
        InstallStatusText.Text = "Kuruluyor…";
        var desktop = DesktopShortcutToggle.IsChecked == true;
        try { using var worker = new StaWorker("Luma installation"); await worker.RunAsync(() => { installed = InstallService.Install(desktop); }); InstallStatusText.Text = "Kurulum tamamlandı. Luma'yı açarak yeni konumdan devam edebilirsin."; LaunchButton.Visibility = Visibility.Visible; }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        { InstallStatusText.Text = "Kurulum tamamlanamadı: " + ex.Message; InstallNowButton.IsEnabled = true; }
        finally { installing = false; }
    }
    private void Launch_Click(object sender, RoutedEventArgs e) { if (installed is null) return; try { App.LaunchInstalled(installed.Executable); Close(); } catch (System.ComponentModel.Win32Exception ex) { InstallStatusText.Text = "Luma açılamadı: " + ex.Message; } }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    protected override void OnClosing(CancelEventArgs e) { if (installing) e.Cancel = true; base.OnClosing(e); }
}

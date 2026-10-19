using System;
using System.ComponentModel;
using System.Windows;
using Luma.Services;
namespace Luma.Views;

public partial class InstallWindow : Window
{
    private InstallPlan? installed;
    private bool installing;
    private readonly InstallPlan? previous;
    private readonly bool updating;
    public InstallWindow() : this(false, null) { }
    internal InstallWindow(bool updating, string? previousExecutable)
    {
        this.updating = updating;
        previous = UpdateActivation.PreviousInstallation(previousExecutable, InstallService.DefaultRoot);
        InitializeComponent(); InstallPathText.Text = InstallService.DefaultRoot;
        Loaded += (_, _) => { Strings.Apply(this); if (updating) InstallNow_Click(this, new RoutedEventArgs()); };
    }
    private async void InstallNow_Click(object sender, RoutedEventArgs e)
    {
        InstallNowButton.IsEnabled = false;
        installing = true;
        InstallStatusText.Text = Strings.T("Kuruluyor…");
        var desktop = DesktopShortcutToggle.IsChecked == true;
        try
        {
            using var worker = new StaWorker("Luma installation");
            await worker.RunAsync(() => { installed = InstallService.Install(desktop); });
            if (updating)
            {
                var result = await UpdateActivation.ActivateAsync(installed!, previous, new NativeUpdateRuntime());
                if (result.Updated) { installing = false; Close(); return; }
                InstallStatusText.Text = Strings.T(result.Restored ? "Yeni sürüm açılamadı. Önceki sürüm geri yüklendi." : "Yeni sürüm açılamadı. Önceki sürümü kendi klasöründen açabilirsin.");
            }
            else { InstallStatusText.Text = Strings.T("Kurulum tamamlandı. Luma'yı açarak yeni konumdan devam edebilirsin."); LaunchButton.Visibility = Visibility.Visible; }
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        { if (updating && previous is not null) { try { new NativeUpdateRuntime().RestoreRegistration(previous); } catch (Exception rollback) when (rollback is System.IO.IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException or System.Security.SecurityException) { } } InstallStatusText.Text = Strings.T("Kurulum tamamlanamadı: ") + ex.Message; InstallNowButton.IsEnabled = true; }
        finally { installing = false; }
    }
    private void Launch_Click(object sender, RoutedEventArgs e) { if (installed is null) return; try { App.LaunchInstalled(installed.Executable); Close(); } catch (System.ComponentModel.Win32Exception ex) { InstallStatusText.Text = Strings.T("Luma açılamadı: ") + ex.Message; } }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    protected override void OnClosing(CancelEventArgs e) { if (installing) e.Cancel = true; base.OnClosing(e); }
}

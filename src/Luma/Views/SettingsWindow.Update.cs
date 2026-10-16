using System;
using System.Diagnostics;
using System.Windows;
using Luma.Services;
namespace Luma.Views;

public partial class SettingsWindow
{
    private Uri? releasePage;
    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false; OpenReleaseButton.Visibility = Visibility.Collapsed; UpdateStatusText.Text = "GitHub sürümü denetleniyor…";
        try { using var updates = new UpdateService(); var result = await updates.CheckAsync(); UpdateStatusText.Text = result.Message; releasePage = result.Page; OpenReleaseButton.Visibility = result.Available ? Visibility.Visible : Visibility.Collapsed; }
        finally { CheckUpdatesButton.IsEnabled = true; }
    }
    private void OpenRelease_Click(object sender, RoutedEventArgs e)
    {
        if (releasePage is null) return;
        try { Process.Start(new ProcessStartInfo(releasePage.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { UpdateStatusText.Text = "İndirme sayfası açılamadı. github.com/kyrerdm/Luma/releases adresini kullanabilirsin."; }
    }
    private void Install_Click(object sender, RoutedEventArgs e) => new InstallWindow { Owner = this }.ShowDialog();
}

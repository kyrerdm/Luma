using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Windows;
using Luma.Services;
namespace Luma.Views;

public partial class SettingsWindow
{
    private Uri? releasePage;
    private UpdatePackage? updatePackage;
    private CancellationTokenSource? updateCancellation;
    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false; OpenReleaseButton.Visibility = InstallUpdateButton.Visibility = Visibility.Collapsed; UpdateStatusText.Text = Strings.T("GitHub sürümü denetleniyor…");
        try { using var updates = new UpdateService(); var result = await updates.CheckAsync(); UpdateStatusText.Text = Strings.T(result.Message); releasePage = result.Page; updatePackage = result.Package; InstallUpdateButton.Visibility = result.Package is null ? Visibility.Collapsed : Visibility.Visible; OpenReleaseButton.Visibility = result.Available ? Visibility.Visible : Visibility.Collapsed; }
        finally { CheckUpdatesButton.IsEnabled = true; }
    }
    private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (updatePackage is null || updateCancellation is not null) return;
        using var cancellation = new CancellationTokenSource(); updateCancellation = cancellation;
        InstallUpdateButton.IsEnabled = CheckUpdatesButton.IsEnabled = false;
        CancelUpdateButton.Visibility = UpdateProgress.Visibility = Visibility.Visible;
        UpdateProgress.Value = 0;
        UpdateStatusText.Text = Strings.T("İndiriliyor… Luma çalışmaya devam ediyor.");
        try
        {
            using var updates = new UpdateService();
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LumaFlyouts", "updates");
            var executable = await updates.DownloadAsync(updatePackage, directory, new Progress<int>(value => UpdateProgress.Value = value), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            UpdateStatusText.Text = Strings.T("Doğrulandı. Yeni sürüm kuruluyor…");
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = directory, WindowStyle = ProcessWindowStyle.Hidden };
            start.ArgumentList.Add("--apply-update");
            if (Environment.ProcessPath is string previous) { start.ArgumentList.Add("--previous"); start.ArgumentList.Add(previous); }
            using var installer = Process.Start(start);
        }
        catch (OperationCanceledException) { UpdateStatusText.Text = Strings.T("İndirme iptal edildi. Mevcut sürüm çalışmaya devam ediyor."); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or HttpRequestException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { UpdateStatusText.Text = Strings.T("Güncelleme indirilemedi veya doğrulanamadı. Mevcut sürüm korundu."); }
        finally { updateCancellation = null; InstallUpdateButton.IsEnabled = CheckUpdatesButton.IsEnabled = true; CancelUpdateButton.Visibility = UpdateProgress.Visibility = Visibility.Collapsed; }
    }
    private void CancelUpdate_Click(object sender, RoutedEventArgs e) => updateCancellation?.Cancel();
    private void OpenRelease_Click(object sender, RoutedEventArgs e)
    {
        if (releasePage is null) return;
        try { Process.Start(new ProcessStartInfo(releasePage.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { UpdateStatusText.Text = Strings.T("İndirme sayfası açılamadı. github.com/kyrerdm/Luma/releases adresini kullanabilirsin."); }
    }
    private void Install_Click(object sender, RoutedEventArgs e) => new InstallWindow { Owner = this }.ShowDialog();
}

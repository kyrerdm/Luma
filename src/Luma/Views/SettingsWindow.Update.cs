using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Luma.Services;
namespace Luma.Views;

public partial class SettingsWindow
{
    private Uri? releasePage;
    private UpdatePackage? updatePackage;
    private CancellationTokenSource? updateCancellation;
    private UpdateResult? lastVerifiedUpdate;
    private UpdateResult? lastUpdateResult;
    private bool storeUpdates;
    private bool checkingUpdates;
    internal Func<Task<UpdateResult>>? CheckForUpdates { get; set; }
    internal void ConfigureDistribution(bool packaged)
    {
        storeUpdates = packaged;
        AutomaticUpdatesRow.Visibility = packaged ? Visibility.Collapsed : Visibility.Visible;
        UpdateBadge.Visibility = !packaged && lastVerifiedUpdate?.Available == true ? Visibility.Visible : Visibility.Collapsed;
        if (!packaged) return;
        CheckUpdatesButton.Visibility = InstallUpdateButton.Visibility = CancelUpdateButton.Visibility = OpenReleaseButton.Visibility = InstallButton.Visibility = Visibility.Collapsed;
        UpdateProgress.Visibility = Visibility.Collapsed;
        StoreUpdatesButton.Visibility = Visibility.Visible;
        UpdateStatusText.Text = Strings.T(Distribution.StoreUpdatesMessage);
    }

    internal void SetUpdateResult(UpdateResult result)
    {
        if (storeUpdates) return;
        lastUpdateResult = result;
        if (result.Verified) lastVerifiedUpdate = result;
        releasePage = lastVerifiedUpdate?.Page;
        updatePackage = lastVerifiedUpdate?.Package;
        UpdateBadge.Visibility = lastVerifiedUpdate?.Available == true ? Visibility.Visible : Visibility.Collapsed;
        // A scheduled check must not replace download progress or reveal an active install button.
        if (updateCancellation is null)
        {
            InstallUpdateButton.Visibility = updatePackage is null ? Visibility.Collapsed : Visibility.Visible;
            OpenReleaseButton.Visibility = lastVerifiedUpdate?.Available == true ? Visibility.Visible : Visibility.Collapsed;
            RefreshUpdateText();
        }
    }

    private void RefreshUpdateText()
    {
        if (UpdateStatusText is null || updateCancellation is not null || checkingUpdates) return;
        if (storeUpdates) { UpdateStatusText.Text = Strings.T(Distribution.StoreUpdatesMessage); return; }
        UpdateStatusText.Text = Strings.T(lastUpdateResult?.Message ?? (settings.AutomaticUpdateChecks
            ? "Açılıştan sonra ve 6 saatte bir kontrol eder. İndirme ve kurulumu sen başlatırsın."
            : "Yalnızca sen istediğinde GitHub'a bağlanır."));
    }

    internal void ShowUpdates()
    {
        Navigate("General");
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => UpdatesSection.BringIntoView()));
    }
    private void UpdateBadge_Click(object sender, RoutedEventArgs e) => ShowUpdates();
    private void OpenStoreUpdates_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("ms-windows-store://downloadsandupdates") { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { UpdateStatusText.Text = Strings.T("Microsoft Store'u açıp Kitaplık bölümünden güncellemeleri denetle."); }
    }
    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (storeUpdates || Distribution.IsPackaged) { ConfigureDistribution(true); return; }
        if (checkingUpdates || updateCancellation is not null) return;
        checkingUpdates = true;
        CheckUpdatesButton.IsEnabled = InstallUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = Strings.T("GitHub sürümü denetleniyor…");
        try
        {
            if (CheckForUpdates is not null) SetUpdateResult(await CheckForUpdates());
            else { using var updates = new UpdateService(); SetUpdateResult(await updates.CheckAsync()); }
        }
        finally { checkingUpdates = false; CheckUpdatesButton.IsEnabled = InstallUpdateButton.IsEnabled = true; RefreshUpdateText(); }
    }
    private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (storeUpdates || Distribution.IsPackaged || checkingUpdates || updatePackage is null || updateCancellation is not null) return;
        var package = updatePackage;
        using var cancellation = new CancellationTokenSource(); updateCancellation = cancellation;
        InstallUpdateButton.IsEnabled = CheckUpdatesButton.IsEnabled = false;
        CancelUpdateButton.Visibility = UpdateProgress.Visibility = Visibility.Visible;
        UpdateProgress.Value = 0;
        UpdateStatusText.Text = Strings.T("İndiriliyor… Luma çalışmaya devam ediyor.");
        try
        {
            using var updates = new UpdateService();
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LumaFlyouts", "updates");
            var executable = await updates.DownloadAsync(package, directory, new Progress<int>(value => UpdateProgress.Value = value), cancellation.Token);
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
        finally
        {
            updateCancellation = null;
            InstallUpdateButton.IsEnabled = CheckUpdatesButton.IsEnabled = true;
            InstallUpdateButton.Visibility = updatePackage is null ? Visibility.Collapsed : Visibility.Visible;
            OpenReleaseButton.Visibility = lastVerifiedUpdate?.Available == true ? Visibility.Visible : Visibility.Collapsed;
            CancelUpdateButton.Visibility = UpdateProgress.Visibility = Visibility.Collapsed;
        }
    }
    private void CancelUpdate_Click(object sender, RoutedEventArgs e) => updateCancellation?.Cancel();
    private void OpenRelease_Click(object sender, RoutedEventArgs e)
    {
        if (releasePage is null) return;
        try { Process.Start(new ProcessStartInfo(releasePage.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { UpdateStatusText.Text = Strings.T("İndirme sayfası açılamadı. github.com/kyrerdm/Luma/releases adresini kullanabilirsin."); }
    }
    private void Install_Click(object sender, RoutedEventArgs e)
    {
        if (!Distribution.IsPackaged) new InstallWindow { Owner = this }.ShowDialog();
    }
}

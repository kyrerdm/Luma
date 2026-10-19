using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Luma.Models;
using Luma.Services;
using Microsoft.Win32;

namespace Luma.Views;

public partial class SettingsWindow
{
    public Func<string>? CreateSupportReport { get; set; }
    internal string CurrentSection { get; private set; } = "General";
    private static readonly string[] SectionNames = { "General", "Appearance", "Sound", "Media", "Display", "About" };

    internal void SetSettingsRecovery(SettingsRecovery recovery)
    {
        RecoveryNotice.Visibility = recovery == SettingsRecovery.None ? Visibility.Collapsed : Visibility.Visible;
        RecoveryText.Text = recovery == SettingsRecovery.Backup
            ? Strings.T("Ayarların son geçerli yedekten kurtarıldı. Son değişikliklerini kontrol edebilirsin.")
            : recovery == SettingsRecovery.Defaults ? Strings.T("Ayar dosyaları okunamadı; varsayılanlar yüklendi. Tercihlerini yeniden düzenleyebilirsin.") : "";
    }
    private void RecoveryDismiss_Click(object sender, RoutedEventArgs e) => RecoveryNotice.Visibility = Visibility.Collapsed;

    private void Navigation_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string section } && PreferencesScroll is not null) Navigate(section);
    }

    internal void Navigate(string section)
    {
        var pages = new[] { GeneralSection, AppearanceSection, SoundSection, MediaSection, DisplaySection, AboutSection };
        var titles = new[] { "Genel", "Görünüm", "Ses", "Medya", "Ekran", "Hakkında" };
        var descriptions = new[] { "Günlük kullanım, yedekler ve güncellemeler.", "Masaüstüne uyacak küçük dokunuşlar.", "Ses tuşları ve ses değişiklikleri.", "Çalan parçaya yer aç.", "Monitörler ve ekran parlaklığı.", "Luma'nın imzası, bağlantıları ve lisansları." };
        var selected = Array.IndexOf(SectionNames, section);
        if (selected < 0) return;
        CurrentSection = section;
        for (var i = 0; i < pages.Length; i++) pages[i].Visibility = i == selected ? Visibility.Visible : Visibility.Collapsed;
        var navigation = new[] { GeneralNav, AppearanceNav, SoundNav, MediaNav, DisplayNav, AboutNav };
        navigation[selected].IsChecked = true;
        PageTitle.Text = Strings.T(titles[selected]);
        PageDescription.Text = Strings.T(descriptions[selected]);
        PreferencesScroll.ScrollToTop();
    }

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || LanguageCombo.SelectedItem is not ComboBoxItem { Tag: string language }) return;
        settings.Language = language;
        Strings.SetLanguage(language);
        Save();
    }

    private void OnLanguageChanged()
    {
        if (Dispatcher.HasShutdownStarted) return;
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(OnLanguageChanged); return; }
        Strings.Apply(this);
        ReloadMonitors();
        if (settings.ExternalBrightness && externalState.Length > 0) SetExternal(externalState);
        UpdateLabels();
        RefreshLivePreview();
        Navigate(CurrentSection);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Title = Strings.T("Luma ayarlarını dışa aktar"), Filter = "Luma (*.json)|*.json", FileName = "Luma-settings.json", DefaultExt = ".json", AddExtension = true };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, PreferenceTransfer.Export(settings), new UTF8Encoding(false)); FeedbackText.Text = Strings.T("Ayar yedeğin kaydedildi."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { FeedbackText.Text = Strings.T("Dosya kaydedilemedi. Başka bir konum seç."); }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = Strings.T("Luma ayarlarını içe aktar"), Filter = "Luma (*.json)|*.json", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try { ApplyTransferredPreferences(PreferenceTransfer.Read(dialog.FileName)); FeedbackText.Text = Strings.T("Ayarlar içe aktarıldı. Windows başlangıç tercihin korundu."); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException) { FeedbackText.Text = Strings.T("Ayarlar alınamadı. Geçerli bir Luma yedeği seç; mevcut tercihler korundu."); }
    }

    internal void ApplyTransferredPreferences(AppSettings incoming)
    {
        var before = PreferenceTransfer.Parse(PreferenceTransfer.Export(settings));
        before.WelcomeCompleted = settings.WelcomeCompleted;
        incoming.WelcomeCompleted = true;
        settings.Apply(incoming);
        try { save(); }
        catch { settings.Apply(before); Luma.Themes.Palette.Apply(settings); ReloadPreferences(); throw; }
        Luma.Themes.Palette.Apply(settings);
        Strings.SetLanguage(settings.Language);
        ReloadPreferences();
        ResetConfirmation.Visibility = Visibility.Collapsed;
        if (settings.ExternalBrightness) ExternalScanRequested?.Invoke();
        else { ExternalRows.Children.Clear(); externalSliders.Clear(); externalState = Array.Empty<ExternalBrightness>(); }
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => ResetConfirmation.Visibility = Visibility.Visible;
    private void CancelReset_Click(object sender, RoutedEventArgs e) => ResetConfirmation.Visibility = Visibility.Collapsed;
    private void ConfirmReset_Click(object sender, RoutedEventArgs e)
    {
        try { ApplyTransferredPreferences(new AppSettings { Language = settings.Language }); FeedbackText.Text = Strings.T("Varsayılan tercihler uygulandı. Windows başlangıç tercihin korundu."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { FeedbackText.Text = Strings.T("Ayarlar kaydedilemedi; mevcut tercihler korundu."); }
    }

    private void SupportReport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Title = Strings.T("Luma destek raporu"), Filter = "Text (*.txt)|*.txt", FileName = "Luma-support.txt", DefaultExt = ".txt", AddExtension = true };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, CreateSupportReport?.Invoke() ?? "Luma " + AppVersion.Current.ToString(3), new UTF8Encoding(false)); FeedbackText.Text = Strings.T("Destek raporu kaydedildi. Paylaşmadan önce inceleyebilirsin."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { FeedbackText.Text = Strings.T("Dosya kaydedilemedi. Başka bir konum seç."); }
    }

    internal bool ShowWelcome()
    {
        var dialog = new WelcomeWindow(settings, save);
        if (IsVisible) dialog.Owner = this;
        dialog.ShowDialog();
        ReloadPreferences();
        return dialog.Completed;
    }
    private void Welcome_Click(object sender, RoutedEventArgs e) => ShowWelcome();
}

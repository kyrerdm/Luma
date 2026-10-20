using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Luma.Models;
using Luma.Services;
using Luma.Themes;

namespace Luma.Views;

public partial class WelcomeWindow : Window
{
    private readonly AppSettings settings;
    private readonly Action save;
    private readonly string previousTheme;
    private readonly string previousLanguage;
    private bool ready;
    private bool finishing;
    public bool Completed { get; private set; }
    public WelcomeWindow(AppSettings settings, Action save)
    {
        this.settings = settings; this.save = save;
        previousTheme = settings.Theme; previousLanguage = settings.Language;
        InitializeComponent();
        foreach (ComboBoxItem item in LanguageCombo.Items) if ((string)item.Tag == settings.Language) LanguageCombo.SelectedItem = item;
        foreach (ComboBoxItem item in ThemeCombo.Items) if ((string)item.Tag == settings.Theme) ThemeCombo.SelectedItem = item;
        foreach (ComboBoxItem item in PositionCombo.Items) if ((string)item.Tag == settings.Position) PositionCombo.SelectedItem = item;
        StartupToggle.IsChecked = StartupService.IsEnabled();
        Loaded += async (_, _) =>
        {
            Strings.Apply(this);
            if (!Distribution.IsPackaged) return;
            StartupToggle.IsEnabled = FinishButton.IsEnabled = false;
            try { await StartupService.RefreshAsync(); StartupToggle.IsChecked = StartupService.IsEnabled(); }
            catch (InvalidOperationException ex) { ErrorText.Text = ex.Message; }
            finally { StartupToggle.IsEnabled = FinishButton.IsEnabled = true; }
        };
        ready = true;
    }
    private void Language_Changed(object sender, SelectionChangedEventArgs e) { if (ready && LanguageCombo.SelectedItem is ComboBoxItem { Tag: string language }) { Strings.SetLanguage(language); Strings.Apply(this); } }
    private void Theme_Changed(object sender, SelectionChangedEventArgs e) { if (ready && ThemeCombo.SelectedItem is ComboBoxItem { Tag: string theme }) Palette.Apply(new AppSettings { Theme = theme, Accent = settings.Accent, CustomAccent = settings.CustomAccent }); }
    private async void Finish_Click(object sender, RoutedEventArgs e)
    {
        if (!FinishButton.IsEnabled) return;
        finishing = true;
        FinishButton.IsEnabled = false;
        var before = PreferenceTransfer.Parse(PreferenceTransfer.Export(settings)); before.WelcomeCompleted = settings.WelcomeCompleted;
        var startupBefore = StartupService.IsEnabled();
        try
        {
            settings.Language = (string)((ComboBoxItem)LanguageCombo.SelectedItem).Tag;
            settings.Theme = (string)((ComboBoxItem)ThemeCombo.SelectedItem).Tag;
            settings.Position = (string)((ComboBoxItem)PositionCombo.SelectedItem).Tag;
            settings.WelcomeCompleted = true;
            if (startupBefore != (StartupToggle.IsChecked == true)) await StartupService.SetEnabledAsync(StartupToggle.IsChecked == true);
            save(); Completed = true; Close();
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException)
        {
            settings.Apply(before);
            try { if (StartupService.IsEnabled() != startupBefore) await StartupService.SetEnabledAsync(startupBefore); } catch (Exception rollback) when (rollback is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException) { }
            ErrorText.Text = Strings.T("Ayarlar kaydedilemedi; mevcut tercihler korundu.");
        }
        finally { finishing = false; FinishButton.IsEnabled = true; }
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (finishing && !Completed) e.Cancel = true;
        base.OnClosing(e);
    }
    protected override void OnClosed(EventArgs e)
    {
        if (!Completed) { settings.Theme = previousTheme; Strings.SetLanguage(previousLanguage); }
        Palette.Apply(settings);
        base.OnClosed(e);
    }
}

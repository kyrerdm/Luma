using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Luma.Models;
using Luma.Services;
using Luma.Themes;
using Luma.Views;

namespace Luma;

internal static partial class Diagnostics
{
    private static async Task VerifyLocalizationAsync(string directory, Action<string, bool, string> check)
    {
        var english = Strings.Catalogs["en"];
        var catalogsComplete = Strings.Catalogs.Count == 4 && english.Count >= 250;
        foreach (var catalog in Strings.Catalogs.Values)
        {
            catalogsComplete &= catalog.Count == english.Count;
            foreach (var pair in english)
                catalogsComplete &= catalog.TryGetValue(pair.Key, out var translated) && !string.IsNullOrWhiteSpace(translated) && CompositeFormat.Parse(pair.Value).MinimumArgumentCount == CompositeFormat.Parse(translated).MinimumArgumentCount;
        }
        check("All four embedded translation catalogs cover every source key and format argument", catalogsComplete, $"{english.Count} keys per catalog; Turkish uses the source keys.");
        var labels = english.Keys.Select(key => new TextBlock { Text = key }).ToArray();
        var group = new StackPanel(); foreach (var label in labels) group.Children.Add(label);
        var roundTrip = true;
        foreach (var language in new[] { "en", "de", "es", "it", "tr", "it", "de", "tr", "es", "en", "tr" })
        {
            Strings.SetLanguage(language); Strings.Apply(group);
            var index = 0;
            foreach (var key in english.Keys) roundTrip &= labels[index++].Text == (language == "tr" ? key : Strings.Catalogs[language][key]);
        }
        check("Repeated five-language switching retains the original context of every label", roundTrip, "Identical translated words do not overwrite each control's source key.");
        var excluded = new TextBlock { Text = "General" }; Strings.Exclude(excluded); group.Children.Add(excluded);
        Strings.SetLanguage("de"); Strings.Apply(group);
        check("Excluded dynamic content is not translated even when it matches a UI word", excluded.Text == "General", "");
        Strings.SetLanguage("unsupported");
        var invalid = new AppSettings { Language = "unsupported" }; invalid.Normalize();
        check("Unknown language codes safely fall back to English", Strings.Language == "en" && invalid.Language == "en", "");
        var freshStore = new SettingsStore(Path.Combine(directory, "fresh-english-" + Guid.NewGuid().ToString("N")));
        check("New installations default to English without creating settings files", new AppSettings().Language == "en" && freshStore.Load().Language == "en" && !Directory.Exists(freshStore.DirectoryPath), "");
        var legacyStore = new SettingsStore(Path.Combine(directory, "legacy-language")); Directory.CreateDirectory(legacyStore.DirectoryPath);
        const string legacyJson = "{\"Accent\":\"Lilac\"}"; File.WriteAllText(legacyStore.FilePath, legacyJson);
        var legacyBackup = PreferenceTransfer.Export(new AppSettings { Language = "tr" }).Replace("\"Language\": \"tr\",", "");
        check("Legacy settings and backups without language keep Turkish without being rewritten", legacyStore.Load().Language == "tr" && File.ReadAllText(legacyStore.FilePath) == legacyJson && PreferenceTransfer.Parse(legacyBackup).Language == "tr", "");
        var firstWelcome = new WelcomeWindow(new AppSettings(), () => { }); firstWelcome.Show(); await Task.Delay(20);
        check("First-run onboarding starts in English with English first in the list", ((ComboBoxItem)((ComboBox)firstWelcome.FindName("LanguageCombo")).SelectedItem).Tag is "en" && ((ComboBoxItem)((ComboBox)firstWelcome.FindName("LanguageCombo")).Items[0]).Tag is "en" && ((Button)firstWelcome.FindName("FinishButton")).Content is "I'm ready", "");
        firstWelcome.Close(); Strings.SetLanguage("tr");

        var settings = new AppSettings { Animations = false, WelcomeCompleted = true, ExternalBrightness = true, Language = "tr" };
        Palette.Apply(settings);
        var saves = 0;
        var window = new SettingsWindow(settings, () => saves++) { AllowClose = true, Width = 960, Height = 640 };
        window.SetBrightness(new(true, 65, "sample", Array.Empty<int>(), "Dahili ekran · değişiklikler anında uygulanır."));
        window.UpdatePreviewAudio(new(true, 46, false, "General"));
        window.UpdatePreviewMedia(PreviewData.Media() with { Title = "General", Artist = "About", AppName = "Sound", IsPlaying = false });
        window.Show(); await Task.Delay(80);
        var selector = (ComboBox)window.FindName("LanguageCombo");
        check("Settings lists all five languages by their native names", selector.Items.Count == 5 && selector.Items.Cast<ComboBoxItem>().All(item => item.Tag is string language && Strings.Languages[language] == (string)item.Content), "");
        try
        {
            foreach (var language in new[] { "de", "es", "it", "en", "tr" })
            {
                var previousSaves = saves;
                selector.SelectedItem = selector.Items.Cast<ComboBoxItem>().Single(item => (string)item.Tag == language);
                await Task.Delay(30);
                var titles = new[] { "Genel", "Görünüm", "Ses", "Medya", "Ekran", "Hakkında" };
                var sections = new[] { "General", "Appearance", "Sound", "Media", "Display", "About" };
                var correct = settings.Language == language && Strings.Language == language && window.Language.IetfLanguageTag == language && saves == previousSaves + 1 && ((TextBlock)window.FindName("DisplayBrightnessText")).Text == Strings.Percent(65);
                var clipped = new HashSet<string>();
                for (var i = 0; i < sections.Length; i++)
                {
                    window.Navigate(sections[i]); window.UpdateLayout();
                    correct &= ((TextBlock)window.FindName("PageTitle")).Text == Strings.T(titles[i]) && ((RadioButton)window.FindName(sections[i] + "Nav")).Content is string caption && caption == Strings.T(titles[i]);
                    foreach (var text in VisibleText(window))
                    {
                        if (text.Text.Length == 0 || text.TextWrapping != TextWrapping.NoWrap || text.TextTrimming != TextTrimming.None || text.ActualWidth < 1) continue;
                        var measured = new FormattedText(text.Text, Strings.Culture, text.FlowDirection, new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(text).PixelsPerDip);
                        if (measured.WidthIncludingTrailingWhitespace > text.ActualWidth + 2) clipped.Add(text.Text);
                    }
                    foreach (var button in VisibleControls<Button>(window).Where(button => button.Content is string))
                    {
                        var captionText = (string)button.Content;
                        var measured = new FormattedText(captionText, Strings.Culture, button.FlowDirection, new Typeface(button.FontFamily, button.FontStyle, button.FontWeight, button.FontStretch), button.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(button).PixelsPerDip);
                        if (measured.WidthIncludingTrailingWhitespace + button.Padding.Left + button.Padding.Right + button.BorderThickness.Left + button.BorderThickness.Right > button.ActualWidth + 2) clipped.Add(captionText);
                    }
                }
                check("Settings navigation, captions and preference saves switch live: " + language, correct, "");
                check("Unwrapped settings labels fit at the minimum window size: " + language, clipped.Count == 0, string.Join(" / ", clipped.Take(8)));
                check("Live media metadata remains unchanged through language switching: " + language,
                    ((TextBlock)window.FindName("PreviewTrackTitle")).Text == "General" && ((TextBlock)window.FindName("PreviewArtist")).Text == "About" && ((TextBlock)window.FindName("PreviewAppName")).Text == "Sound" && AutomationProperties.GetName((DependencyObject)window.FindName("PreviewVolumeSlider")) == Strings.T("Canlı ses düzeyi"), "");
                window.Navigate("Appearance"); Render(window, Path.Combine(directory, "luma-settings-" + language + ".png"));
                var store = new SettingsStore(Path.Combine(directory, "language-" + language));
                store.Save(settings);
                check("Language survives settings restart and preference transfer: " + language, store.Load().Language == language && PreferenceTransfer.Parse(PreferenceTransfer.Export(settings)).Language == language, "");

                var flyout = new FlyoutWindow(settings);
                try
                {
                    flyout.AudioPanel.SetState(new(new[] { new AudioOutput("sample", "General", true) }, new[] { new AudioApp("sample", "About", "sample", new[] { "one", "two" }, 40, false) }, "Seçili çıkıştaki uygulamalar"));
                    flyout.ApplyLanguage();
                    var rows = (StackPanel)flyout.AudioPanel.FindName("AppRows");
                    var row = (Grid)rows.Children[0];
                    var mute = row.Children.OfType<Button>().Single();
                    check("Audio panel localizes commands and preserves app names: " + language, ((TextBlock)row.Children[0]).Text == "About" && AutomationProperties.GetName(mute) == Strings.Format("{0} sesini kapat", "About") && ((TextBlock)flyout.AudioPanel.FindName("StatusText")).Text == Strings.T("Seçili çıkıştaki uygulamalar"), "");
                    flyout.ShowLock("Caps Lock", true);
                    check("Lock indicators use the selected language: " + language, ((TextBlock)flyout.FindName("LockStateText")).Text == Strings.T("Etkin"), "");
                }
                finally { flyout.Close(); }

                var welcome = new WelcomeWindow(settings, () => { });
                welcome.Show(); await Task.Delay(20);
                var choices = (ComboBox)welcome.FindName("LanguageCombo");
                check("Onboarding offers the saved language without changing startup: " + language, choices.Items.Count == 5 && ((ComboBoxItem)choices.SelectedItem).Tag is string selected && selected == language && ((Button)welcome.FindName("FinishButton")).Content is string finish && finish == Strings.T("Hazırım"), "");
                Render(welcome, Path.Combine(directory, "luma-welcome-" + language + ".png")); welcome.Close();
                var installer = new InstallWindow(); installer.Show(); await Task.Delay(20);
                check("Installer captions follow the selected language: " + language, installer.Title == Strings.T("Luma kurulumu") && ((Button)installer.FindName("InstallNowButton")).Content is string install && install == Strings.T("Kur"), "No installation is performed by this fixture.");
                Render(installer, Path.Combine(directory, "luma-install-" + language + ".png")); installer.Close();
            }
        }
        finally { window.Close(); Strings.SetLanguage("tr"); Palette.Apply(new AppSettings()); }
        check("The uninstall confirmation supports all five saved language choices", new[] { "tr", "en", "de", "es", "it" }.All(language => InstallService.UninstallScript.Contains(language + " = '", StringComparison.Ordinal)), "The uninstaller is inspected, never executed.");
    }

    private static IEnumerable<TextBlock> VisibleText(DependencyObject root)
    {
        if (root is TextBlock { IsVisible: true } text) yield return text;
        if (root is not Visual) yield break;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in VisibleText(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private static IEnumerable<T> VisibleControls<T>(DependencyObject root) where T : FrameworkElement
    {
        if (root is T { IsVisible: true } control) yield return control;
        if (root is not Visual) yield break;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in VisibleControls<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
}

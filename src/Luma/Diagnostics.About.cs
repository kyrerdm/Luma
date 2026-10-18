using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Luma.Models;
using Luma.Services;
using Luma.Themes;
using Luma.Views;

namespace Luma;

internal static partial class Diagnostics
{
    private static async Task VerifyAboutAsync(string directory, Action<string, bool, string> check)
    {
        Strings.SetLanguage("tr");
        var prefs = new AppSettings { WelcomeCompleted = true, Animations = false };
        Palette.Apply(prefs);
        var before = PreferenceTransfer.Export(prefs);
        var saves = 0;
        var window = new SettingsWindow(prefs, () => saves++) { AllowClose = true };
        SeedSettingsPreview(window);
        window.Show(); window.Activate();
        await Task.Delay(80);
        try
        {
            window.Navigate("About");
            var combo = (ComboBox)window.FindName("LicenseCombo");
            var text = (TextBox)window.FindName("LicenseText");
            var version = (TextBlock)window.FindName("AboutVersionText");
            var signature = (TextBlock)window.FindName("AboutSignature");
            var copy = (Button)window.FindName("CopyVersionButton");
            var project = (Button)window.FindName("ProjectButton");
            check("About identifies the running version and developer without personal data", version.Text == AppVersion.Current.ToString(3) && signature.Text == "kyrerdm" && AboutInfo.CopyText == AboutInfo.VersionLabel + Environment.NewLine + AboutInfo.ProjectUrl && !AboutInfo.CopyText.Contains(Environment.UserName, StringComparison.OrdinalIgnoreCase), "");
            check("The original Luma MIT license is embedded with the correct attribution", text.Text.StartsWith("MIT License", StringComparison.Ordinal) && text.Text.Contains("Copyright (c) 2026 kyrerdm") && text.Text.Contains("THE SOFTWARE IS PROVIDED") && text.Text == Encoding.UTF8.GetString(InstallService.ReadNotice("LICENSE.txt")), "");
            var allNoticesMatch = true;
            for (var i = 0; i < AboutInfo.Licenses.Count; i++)
            {
                combo.SelectedIndex = i;
                allNoticesMatch &= text.Text == AboutInfo.ReadLicense(AboutInfo.Licenses[i].ResourceName) && text.Text.Length > 100;
            }
            check("Every selectable license displays its complete embedded text offline", allNoticesMatch && combo.Items.Count == 6, "No network or adjacent files are needed.");
            var refused = false;
            try { AboutInfo.ReadLicense("../../settings.json"); } catch (ArgumentException) { refused = true; }
            check("The license viewer accepts only known bundled license resources", refused, "");
            var peer = FrameworkElementAutomationPeer.CreatePeerForElement(text)!;
            var value = (IValueProvider)peer.GetPattern(PatternInterface.Value);
            var original = text.Text;
            var editRefused = false;
            try { value.SetValue("Changed"); } catch (InvalidOperationException) { editRefused = true; }
            check("License text is named, selectable and read-only through UI Automation", peer.GetName() == "Lisans metni" && value.IsReadOnly && editRefused && text.Text == original && text.Focusable, "");
            combo.SelectedIndex = 2;
            original = text.Text;
            Strings.SetLanguage("en");
            check("About navigation, license labels and accessibility hints switch to English", ((TextBlock)window.FindName("PageTitle")).Text == "About" && ((RadioButton)window.FindName("AboutNav")).Content is "About" && copy.Content is "Copy version" && ((ComboBoxItem)combo.SelectedItem).Content is ".NET · Third-party notices" && AutomationProperties.GetName(text) == "License text" && AutomationProperties.GetHelpText(text) == "Read-only. You can select and copy the text.", "");
            check("Language changes preserve the exact legal text and developer signature", text.Text == original && signature.Text == "kyrerdm", "");
            combo.SelectedIndex = 0;
            Render(window, Path.Combine(directory, "luma-about-english.png"));
            Strings.SetLanguage("tr");
            check("About returns to Turkish without changing the selected license", ((TextBlock)window.FindName("PageTitle")).Text == "Hakkında" && copy.Content is "Sürümü kopyala" && combo.SelectedIndex == 0 && AutomationProperties.GetHelpText(text) == "Salt okunur. Seçip kopyalayabilirsin.", "");

            var names = new[] { "General", "Appearance", "Sound", "Media", "Display", "About" };
            var shortcuts = window.InputBindings.OfType<KeyBinding>().Where(binding => binding.Modifiers == ModifierKeys.Control).OrderBy(binding => binding.Key).ToArray();
            var routesCorrectly = shortcuts.Length == names.Length;
            for (var i = 0; i < shortcuts.Length && i < names.Length; i++)
            {
                shortcuts[i].Command.Execute(null);
                routesCorrectly &= shortcuts[i].Key == Key.D1 + i && window.CurrentSection == names[i] && ((RadioButton)window.FindName(names[i] + "Nav")).IsChecked == true;
            }
            check("Ctrl+1 through Ctrl+6 route to all six settings sections without saving", routesCorrectly && saves == 0, "The actual shortcut commands are invoked; no global input is sent.");
            window.Activate(); copy.Focus();
            var forward = copy.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            var reachedProject = Keyboard.FocusedElement == project;
            var backward = project.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
            check("About actions support forward and reverse keyboard focus", forward && backward && reachedProject && Keyboard.FocusedElement == copy, "");

            window.Width = window.MinWidth; window.Height = window.MinHeight;
            window.UpdateLayout();
            var about = (StackPanel)window.FindName("AboutSection");
            bool Fits(FrameworkElement element)
            {
                var bounds = element.TransformToAncestor(about).TransformBounds(new Rect(element.RenderSize));
                return bounds.Left >= -0.5 && bounds.Right <= about.ActualWidth + 0.5 && element.ActualWidth > 0;
            }
            check("About actions and license selector fit the minimum settings size", Fits(copy) && Fits(project) && Fits(combo), $"About content width: {about.ActualWidth:0}");
            window.Width = 1040; window.Height = 740;
            window.UpdateLayout();
            ((ScrollViewer)window.FindName("PreferencesScroll")).ScrollToTop();
            await Task.Delay(25);
            Render(window, Path.Combine(directory, "luma-about.png"));
            prefs.Theme = "Light"; Palette.Apply(prefs); window.ApplyAppearance();
            await Task.Delay(25);
            check("The license reader follows the light theme rather than forcing a dark surface", ((SolidColorBrush)text.Foreground).Color == ((SolidColorBrush)Application.Current.Resources["TextBrush"]).Color && ((SolidColorBrush)Application.Current.Resources["SurfaceBrush"]).Color.R > 200, "");
            Render(window, Path.Combine(directory, "luma-about-light.png"));
            prefs.Theme = "Dark"; Palette.Apply(prefs);
            check("Reading About, changing licenses and using navigation preserve preferences", saves == 0 && PreferenceTransfer.Export(prefs) == before, "");
        }
        finally { window.Close(); Strings.SetLanguage("tr"); Palette.Apply(new AppSettings()); }
    }
}

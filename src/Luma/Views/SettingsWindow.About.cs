using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Luma.Services;

namespace Luma.Views;

public partial class SettingsWindow
{
    private void InitializeAbout()
    {
        AboutVersionText.Text = AppVersion.Current.ToString(3);
        AboutPlatformText.Text = "Windows · " + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        AboutSignature.Text = AboutInfo.Author;
        foreach (var notice in AboutInfo.Licenses)
            LicenseCombo.Items.Add(new ComboBoxItem { Content = Strings.T(notice.Title), Tag = notice.ResourceName });
        LicenseCombo.SelectedIndex = 0;
        LicenseText.ContextMenu.Opened += (_, _) => Strings.Apply(LicenseText.ContextMenu);
        for (var i = 0; i < SectionNames.Length; i++)
        {
            var section = SectionNames[i];
            var command = new SectionCommand(() =>
            {
                Navigate(section);
                ((RadioButton)FindName(section + "Nav")).Focus();
            });
            InputBindings.Add(new KeyBinding(command, new KeyGesture(Key.D1 + i, ModifierKeys.Control)));
        }
    }

    private void License_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (LicenseText is null || LicenseCombo.SelectedItem is not ComboBoxItem { Tag: string resourceName }) return;
        LicenseText.Text = AboutInfo.ReadLicense(resourceName);
        LicenseText.ScrollToHome();
        LicenseText.Select(0, 0);
    }

    private void CopyVersion_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(AboutInfo.CopyText); FeedbackText.Text = Strings.T("Sürüm bilgisi kopyalandı."); }
        catch (ExternalException) { FeedbackText.Text = Strings.T("Panoya erişilemedi. Tekrar deneyebilirsin."); }
    }

    private void AboutLink_Click(object sender, RoutedEventArgs e)
    {
        var url = ((sender as FrameworkElement)?.Tag as string) switch
        {
            "Project" => AboutInfo.ProjectUrl,
            "Release" => AboutInfo.ReleaseUrl,
            "Issues" => AboutInfo.IssuesUrl,
            _ => null
        };
        if (url is null) return;
        try { using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { FeedbackText.Text = Strings.T("Bağlantı açılamadı. github.com/kyrerdm/Luma adresini kullanabilirsin."); }
    }

    private sealed class SectionCommand(Action execute) : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}

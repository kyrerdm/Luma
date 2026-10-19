using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using Luma.Services;

namespace Luma.Views;

public partial class SettingsWindow
{
    private readonly Dictionary<string, Slider> externalSliders = new();
    private bool updatingExternal;
    private ExternalBrightness[] externalState = Array.Empty<ExternalBrightness>();
    public event Action? ExternalScanRequested;
    public event Action<string, int>? ExternalBrightnessRequested;
    public void SetExternalBusy(bool busy) { ScanMonitorsButton.IsEnabled = !busy; ExternalStatusText.Text = Strings.T(busy ? "Monitörler sorgulanıyor…" : "Destekleyen ekranlarda monitör menüsünden DDC/CI açık olmalıdır."); }
    public void SetExternal(ExternalBrightness[] displays)
    {
        externalState = displays;
        updatingExternal = true; ExternalRows.Children.Clear(); externalSliders.Clear();
        foreach (var display in displays)
        {
            var panel = new StackPanel { Margin = new(0, 6, 0, 4) };
            var name = new TextBlock { Text = display.Name, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = display.Name };
            Strings.Exclude(name); panel.Children.Add(name);
            var line = new Grid(); line.ColumnDefinitions.Add(new()); line.ColumnDefinitions.Add(new() { Width = new GridLength(42) });
            var slider = new Slider { Value = display.Value, IsEnabled = display.Available, Minimum = 0, Maximum = 100, SmallChange = 5, LargeChange = 10, ToolTip = Strings.T(display.Status) };
            var value = new TextBlock { Text = display.Available ? Strings.Percent(display.Value) : "—", FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(value, 1);
            AutomationProperties.SetName(slider, Strings.Format("{0} parlaklığı", display.Name));
            slider.ValueChanged += (_, e) => { if (!updatingExternal && slider.IsEnabled) { value.Text = Strings.Percent(e.NewValue); ExternalBrightnessRequested?.Invoke(display.Id, (int)Math.Round(e.NewValue)); } };
            line.Children.Add(slider); line.Children.Add(value); panel.Children.Add(line);
            var status = new TextBlock { Text = Strings.T(display.Status), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new(0, 2, 0, 0) }; status.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush"); panel.Children.Add(status);
            externalSliders[display.Id] = slider; ExternalRows.Children.Add(panel);
        }
        ExternalStatusText.Text = Strings.T(displays.Length == 0 ? "Bağlı fiziksel monitör bulunamadı." : "Monitör desteklemiyorsa veya DDC/CI kapalıysa kaydırıcı pasif kalır.");
        updatingExternal = false;
    }
    public void UpdateExternal(ExternalBrightness state)
    {
        externalState = Array.ConvertAll(externalState, previous => previous.Id == state.Id ? state : previous);
        if (!externalSliders.TryGetValue(state.Id, out var slider)) return;
        updatingExternal = true;
        slider.IsEnabled = state.Available;
        if (!slider.IsMouseCaptureWithin) slider.Value = state.Value;
        if (slider.Parent is Grid line && line.Children[1] is TextBlock value) value.Text = state.Available ? Strings.Percent(state.Value) : "—";
        ExternalStatusText.Text = Strings.T(state.Status); updatingExternal = false;
    }
    private void External_Changed(object sender, RoutedEventArgs e) { if (!ready) return; settings.ExternalBrightness = ExternalToggle.IsChecked == true; Save(); if (settings.ExternalBrightness) ExternalScanRequested?.Invoke(); else { ExternalRows.Children.Clear(); externalSliders.Clear(); ExternalStatusText.Text = Strings.T("Monitör kontrolü kapalı."); } }
    private void ScanMonitors_Click(object sender, RoutedEventArgs e) { if (ExternalToggle.IsChecked != true) ExternalToggle.IsChecked = true; else ExternalScanRequested?.Invoke(); }
}

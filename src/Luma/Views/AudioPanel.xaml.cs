using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Luma.Services;

namespace Luma.Views;

public partial class AudioPanel : UserControl
{
    private sealed record AppRow(Grid Root, TextBlock Name, Button Mute, Slider Slider, TextBlock Value) { public AudioApp? State; public bool Updating; }
    private readonly Dictionary<string, AppRow> rows = new();
    private string outputSignature = "";
    private bool busy;
    public event Action<string>? OutputRequested;
    public event Action<AudioApp, int>? AppVolumeRequested;
    public event Action<AudioApp, bool>? AppMuteRequested;
    public AudioPanel() { InitializeComponent(); ApplyLanguage(); }
    internal void ApplyLanguage()
    {
        if (Content is Border { Child: StackPanel panel })
            foreach (var child in panel.Children) if (child is TextBlock text) text.Text = Strings.T(text.Text);
    }
    public void SetBusy(bool value) { busy = value; OutputRows.IsEnabled = !value; }
    public void Report(bool success)
    {
        if (success) return;
        StatusText.Text = Strings.T("İşlem uygulanamadı. Aygıt veya uygulama kapanmış olabilir; yeniden deneyebilirsin.");
        foreach (var row in rows.Values) if (row.State is { } state && !row.Slider.IsMouseCaptureWithin) { row.Updating = true; row.Slider.Value = state.Volume; row.Value.Text = state.Volume.ToString(); row.Updating = false; }
    }
    public void SetState(AudioRoutingState state)
    {
        var signature = string.Join("\n", state.Outputs.Select(o => o.Id + o.Name + o.Selected));
        if (signature != outputSignature)
        {
            outputSignature = signature; OutputRows.Children.Clear();
            foreach (var output in state.Outputs)
            {
                var line = new Grid(); line.ColumnDefinitions.Add(new()); line.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
                var name = new TextBlock { Text = output.Name, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 12, 0) };
                line.Children.Add(name);
                if (output.Selected) { var check = new TextBlock { Text = "\uE73E", FontFamily = new("Segoe Fluent Icons"), FontSize = 11 }; check.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush"); Grid.SetColumn(check, 1); line.Children.Add(check); }
                var button = new Button { Content = line, Padding = new(9, 9, 9, 9), HorizontalContentAlignment = HorizontalAlignment.Stretch, Margin = new(0, 2, 0, 2), ToolTip = output.Name };
                button.SetResourceReference(BackgroundProperty, output.Selected ? "SelectionBrush" : "SurfaceBrush");
                AutomationProperties.SetName(button, output.Name + (output.Selected ? " · Seçili" : ""));
                button.Click += (_, _) => { if (!busy) OutputRequested?.Invoke(output.Id); };
                OutputRows.Children.Add(button);
            }
        }
        var available = state.Apps.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in rows.Keys.Where(id => !available.Contains(id)).ToArray()) { AppRows.Children.Remove(rows[id].Root); rows.Remove(id); }
        foreach (var app in state.Apps)
        {
            if (!rows.TryGetValue(app.Id, out var row))
            {
                var root = new Grid { Margin = new(2, 6, 2, 6) };
                root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new() { Height = new GridLength(30) });
                root.ColumnDefinitions.Add(new() { Width = new GridLength(28) }); root.ColumnDefinitions.Add(new()); root.ColumnDefinitions.Add(new() { Width = new GridLength(34) });
                var name = new TextBlock { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new(0, 0, 0, 3) }; Grid.SetColumnSpan(name, 3); root.Children.Add(name);
                var mute = new Button { Style = (Style)FindResource("QuietButton") }; Grid.SetRow(mute, 1); root.Children.Add(mute);
                var slider = new Slider { Minimum = 0, Maximum = 100, SmallChange = 2, LargeChange = 10, Margin = new(5, 0, 8, 0) }; Grid.SetRow(slider, 1); Grid.SetColumn(slider, 1); root.Children.Add(slider);
                var value = new TextBlock { FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }; Grid.SetRow(value, 1); Grid.SetColumn(value, 2); root.Children.Add(value);
                row = new(root, name, mute, slider, value); rows.Add(app.Id, row); AppRows.Children.Add(root);
                var captured = row;
                slider.ValueChanged += (_, e) => { if (!captured.Updating && captured.State is { } current) { captured.Value.Text = $"{e.NewValue:0}"; AppVolumeRequested?.Invoke(current, (int)Math.Round(e.NewValue)); } };
                mute.Click += (_, _) => { if (captured.State is { } current) AppMuteRequested?.Invoke(current, !current.Muted); };
            }
            row.State = app; row.Updating = true;
            row.Name.Text = app.Name; row.Name.ToolTip = app.SessionIds.Length > 1 ? (Strings.Language == "en" ? $"{app.SessionIds.Length} audio sessions are adjusted together." : $"{app.SessionIds.Length} ses oturumu birlikte ayarlanır.") : app.Name;
            row.Mute.Content = new TextBlock { Text = app.Muted ? "\uE74F" : "\uE767", FontFamily = new("Segoe Fluent Icons"), FontSize = 13 };
            AutomationProperties.SetName(row.Mute, app.Name + (Strings.Language == "en" ? (app.Muted ? " unmute" : " mute") : (app.Muted ? " sesini aç" : " sesini kapat")));
            AutomationProperties.SetName(row.Slider, app.Name + (Strings.Language == "en" ? " volume" : " ses düzeyi"));
            if (!row.Slider.IsMouseCaptureWithin) { row.Slider.Value = app.Volume; row.Value.Text = app.Volume.ToString(); }
            row.Updating = false;
        }
        StatusText.Text = Strings.T(state.Status);
    }
}

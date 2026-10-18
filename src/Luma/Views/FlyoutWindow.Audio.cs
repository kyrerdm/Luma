using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Luma.Services;

namespace Luma.Views;

public partial class FlyoutWindow
{
    internal AudioPanel AudioPanel => AudioControls;
    internal bool AudioPanelOpen => AudioPopup.IsOpen;
    internal string BrightnessTargetId => brightnessState.InstanceId;
    internal Func<bool>? FullscreenProbe { get; set; }
    public event Action<bool>? AudioPanelOpenChanged;
    private readonly DispatcherTimer fullscreenTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private void Output_Click(object sender, RoutedEventArgs e)
    {
        if (preview) return;
        SourceMenu.IsOpen = false;
        AudioPopup.Placement = settings.Position.StartsWith("Top", StringComparison.Ordinal) ? PlacementMode.Bottom : PlacementMode.Top;
        AudioPopup.IsOpen = !AudioPopup.IsOpen;
    }
    private void Audio_Opened(object sender, EventArgs e) { hideTimer.Stop(); AudioPanelOpenChanged?.Invoke(true); }
    private void Audio_Closed(object sender, EventArgs e) { AudioPanelOpenChanged?.Invoke(false); if (volumeMode) RestartTimer(); }
    private bool SuppressForFullscreen => !preview && settings.HideInFullscreen && (FullscreenProbe?.Invoke() ?? FullscreenDetector.IsTargetFullscreen(settings.TargetDisplay));
}

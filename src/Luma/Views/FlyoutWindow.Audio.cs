using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
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
    private string audioPopupPosition = "";
    private void Output_Click(object sender, RoutedEventArgs e)
    {
        if (preview) return;
        SourceMenu.IsOpen = false;
        AudioPopup.IsOpen = !AudioPopup.IsOpen;
    }
    private CustomPopupPlacement[] PlaceAudioPopup(Size popup, Size target, System.Windows.Point offset)
    {
        var x = target.Width - popup.Width;
        var below = new CustomPopupPlacement(new System.Windows.Point(x, target.Height + 8), PopupPrimaryAxis.Horizontal);
        var above = new CustomPopupPlacement(new System.Windows.Point(x, -popup.Height - 8), PopupPrimaryAxis.Horizontal);
        return settings.Position.StartsWith("Top", StringComparison.Ordinal) ? new[] { below, above } : new[] { above, below };
    }
    private void UpdateOutputChevron(bool animate)
    {
        var below = settings.Position.StartsWith("Top", StringComparison.Ordinal);
        var angle = below == AudioPopup.IsOpen ? 180d : 0d;
        if (animate && Motion) OutputChevronRotation.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, new DoubleAnimation(angle, TimeSpan.FromMilliseconds(140)));
        else { OutputChevronRotation.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null); OutputChevronRotation.Angle = angle; }
    }
    private void Audio_Opened(object sender, EventArgs e) { audioPopupPosition = settings.Position; hideTimer.Stop(); UpdateOutputChevron(true); AudioPanelOpenChanged?.Invoke(true); }
    private void Audio_Closed(object sender, EventArgs e) { UpdateOutputChevron(true); AudioPanelOpenChanged?.Invoke(false); if (volumeMode) RestartTimer(); }
    private bool SuppressForFullscreen => !preview && settings.HideInFullscreen && (FullscreenProbe?.Invoke() ?? FullscreenDetector.IsTargetFullscreen(settings.TargetDisplay));
}

using System.Windows;
using System.Windows.Controls;
using Luma.Services;

namespace Luma.Views;

public partial class FlyoutWindow
{
    internal void ApplyLanguage()
    {
        Strings.Apply(VolumeCard);
        Strings.Apply(BrightnessCard);
        Strings.Apply(CardStack.ContextMenu);
        foreach (var name in new[] { "PinButton", "PreviousButton", "PlayButton", "NextButton", "SourceButton", "TimelineSlider" })
            if (FindName(name) is DependencyObject control) Strings.Apply(control);
        AudioPanel.ApplyLanguage();
        PinButton.ToolTip = Strings.T(pinned ? "Otomatik gizle" : "Açık tut");
        if (volumeMode) PaintVolume();
        if (brightnessMode) PaintBrightness();
        if (media.Available) PaintMedia();
        if (LockCard.IsVisible) LockStateText.Text = Strings.T(lockEnabled ? "Etkin" : "Devre dışı");
    }
}

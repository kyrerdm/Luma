using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Luma.Controls;

// The real Value changes once. Only DisplayValue animates, so animation frames never
// write intermediate volume levels back to CoreAudio or seek a media session.
public sealed class SmoothSlider : Slider
{
    public static readonly DependencyProperty DisplayValueProperty = DependencyProperty.Register(
        nameof(DisplayValue), typeof(double), typeof(SmoothSlider), new FrameworkPropertyMetadata(0d));
    public double DisplayValue { get => (double)GetValue(DisplayValueProperty); set => SetValue(DisplayValueProperty, value); }
    public bool MotionEnabled { get; set; } = true;

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        var from = DisplayValue;
        BeginAnimation(DisplayValueProperty, null);
        SetCurrentValue(DisplayValueProperty, newValue);
        if (IsLoaded && MotionEnabled && SystemParameters.ClientAreaAnimation && !IsMouseCaptureWithin && !IsMouseOver)
            BeginAnimation(DisplayValueProperty, new DoubleAnimation(from, newValue, TimeSpan.FromMilliseconds(170))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        BeginAnimation(DisplayValueProperty, null);
        SetCurrentValue(DisplayValueProperty, Value);
        base.OnPreviewMouseDown(e);
    }
}

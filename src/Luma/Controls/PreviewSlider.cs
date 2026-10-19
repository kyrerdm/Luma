using System;
using System.Collections.Generic;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;

namespace Luma.Controls;

// Preserve the animated appearance, but expose a display rather than an action.
public sealed class PreviewSlider : SmoothSlider
{
    public PreviewSlider() { Focusable = false; IsTabStop = false; IsHitTestVisible = false; }
    protected override AutomationPeer OnCreateAutomationPeer() => new PreviewSliderAutomationPeer(this);

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        if (UIElementAutomationPeer.FromElement(this) is PreviewSliderAutomationPeer peer)
            peer.RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, oldValue, newValue);
    }
    protected override void OnPreviewKeyDown(KeyEventArgs e) => e.Handled = true;
}

internal sealed class PreviewSliderAutomationPeer(PreviewSlider owner) : FrameworkElementAutomationPeer(owner), IRangeValueProvider
{
    protected override string GetClassNameCore() => "ProgressBar";
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;
    protected override bool IsKeyboardFocusableCore() => false;
    // Do not leak interactive thumb/repeat-button peers from the shared template.
    protected override List<AutomationPeer>? GetChildrenCore() => null;
    public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.RangeValue ? this : base.GetPattern(patternInterface);
    bool IRangeValueProvider.IsReadOnly => true;
    double IRangeValueProvider.Value => owner.Value;
    double IRangeValueProvider.Minimum => owner.Minimum;
    double IRangeValueProvider.Maximum => owner.Maximum;
    double IRangeValueProvider.SmallChange => double.NaN;
    double IRangeValueProvider.LargeChange => double.NaN;
    void IRangeValueProvider.SetValue(double value) => throw new InvalidOperationException("The preview is read-only.");
}

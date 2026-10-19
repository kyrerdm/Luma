using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using Luma.Models;
using Luma.Themes;
using Luma.Views;

namespace Luma;

internal static partial class Diagnostics
{
    private static string diagnosticMouseStatus = "";
    private static async Task VerifyAudioPopupAsync(Action<string, bool, string> check)
    {
        // Native mouse down/up is essential here: raising Click bypasses WPF's
        // popup capture and cannot reproduce dismiss-then-reopen on the arrow.
        var restorePointer = GetCursorPos(out var original);
        var prefs = new AppSettings { Animations = false, Position = "TopRight", ShowMedia = false, DurationSeconds = 6 };
        Palette.Apply(prefs);
        var flyout = new FlyoutWindow(prefs);
        var popup = (Popup)flyout.FindName("AudioPopup");
        var button = (Button)flyout.FindName("OutputButton");
        var card = (FrameworkElement)flyout.FindName("VolumeCard");
        var rotation = (RotateTransform)flyout.FindName("OutputChevronRotation");
        var events = 0;
        flyout.AudioPanelOpenChanged += _ => events++;
        flyout.AudioPanel.SetState(new(new[] { new Luma.Services.AudioOutput("sample", "Sample output", true) }, Array.Empty<Luma.Services.AudioApp>(), ""));
        try
        {
            flyout.ShowVolume(new(true, 40, false, "Sample output"));
            await Task.Delay(100);
            var opened = await ClickDiagnosticElementAsync(button);
            await Task.Delay(220);
            check("A native arrow click opens the audio panel once", opened && popup.IsOpen && events == 1, $"Open={popup.IsOpen}; transitions={events}");
            var closed = await ClickDiagnosticElementAsync(button);
            await Task.Delay(220);
            check("A second native arrow click closes without reopening the audio panel", closed && !popup.IsOpen && events == 2 && rotation.Angle == 0, $"Open={popup.IsOpen}; transitions={events}");

            popup.IsOpen = false; await Task.Delay(220);
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); await Task.Delay(80);
            var inside = await ClickDiagnosticElementAsync(flyout.AudioPanel, new Point(30, 26));
            check("Clicking non-actionable audio panel content keeps the panel open", inside && popup.IsOpen, diagnosticMouseStatus);
            var outside = await ClickDiagnosticElementAsync(card, new Point(4, 4));
            await Task.Delay(220);
            check("Clicking outside the audio panel still dismisses it", outside && !popup.IsOpen, diagnosticMouseStatus);
            var reopened = await ClickDiagnosticElementAsync(button);
            await Task.Delay(220);
            check("The next arrow click reopens normally after outside dismissal", reopened && popup.IsOpen, "");
            popup.IsOpen = false; await Task.Delay(220);

            var toggles = true;
            var toggleDetails = new System.Collections.Generic.List<string>();
            foreach (var position in new[] { "TopLeft", "TopRight", "BottomLeft", "BottomRight" })
            {
                prefs.Position = position; prefs.Animations = true;
                flyout.ApplyPreferences(); flyout.ShowVolume(new(true, 40, false, "Sample output"));
                await Task.Delay(300);
                for (var i = 0; i < 4; i++)
                {
                    var clicked = await ClickDiagnosticElementAsync(button);
                    await Task.Delay(220);
                    var expectedOpen = i % 2 == 0;
                    if (!clicked || popup.IsOpen != expectedOpen) toggleDetails.Add($"{position}, click {i}: input={clicked}, open={popup.IsOpen}, expected={expectedOpen}; {diagnosticMouseStatus}");
                    toggles &= clicked && popup.IsOpen == expectedOpen;
                }
            }
            check("Repeated native arrow clicks alternate open and closed at all four corners with animation", toggles, string.Join(" / ", toggleDetails));
            popup.IsOpen = false; await Task.Delay(220);
            var rapid = true;
            for (var i = 0; i < 8; i++)
            {
                rapid &= await ClickDiagnosticElementAsync(button);
                await Task.Delay(45);
                rapid &= popup.IsOpen == (i % 2 == 0);
            }
            await Task.Delay(220);
            check("Rapid native clicks during popup fades do not leave the audio panel open", rapid && !popup.IsOpen, "");
            var peer = UIElementAutomationPeer.CreatePeerForElement(button)!;
            var invoke = (IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!;
            invoke.Invoke(); await Task.Delay(80);
            var accessibleOpen = popup.IsOpen;
            invoke.Invoke(); await Task.Delay(220);
            check("Accessibility button invocation still opens and closes the audio panel", accessibleOpen && !popup.IsOpen, "");
        }
        finally
        {
            flyout.Close();
            if (restorePointer) MoveDiagnosticPointer(original.X, original.Y);
            Palette.Apply(new AppSettings());
        }
    }

    private static Task<bool> ClickDiagnosticElementAsync(FrameworkElement element, Point? local = null)
    {
        var point = element.PointToScreen(local ?? new Point(element.ActualWidth / 2, element.ActualHeight / 2));
        var target = new CursorPoint { X = (int)Math.Round(point.X), Y = (int)Math.Round(point.Y) };
        // Refuse clicks if another application covers the fixture. No live audio
        // commands are subscribed to these diagnostic-only windows or controls.
        var source = PresentationSource.FromVisual(element) as HwndSource;
        if (source is null || GetAncestor(WindowFromPoint(target), 2) != source.Handle) { diagnosticMouseStatus = $"Target window not on top at {target.X},{target.Y}"; return Task.FromResult(false); }
        if ((GetAsyncKeyState(1) & 0x8000) != 0 || (GetAsyncKeyState(2) & 0x8000) != 0) { diagnosticMouseStatus = "A physical mouse button is held; refusing to interrupt a drag"; return Task.FromResult(false); }
        var move = DiagnosticPointerMove(target.X, target.Y);
        if (move is null) { diagnosticMouseStatus = "No virtual desktop bounds"; return Task.FromResult(false); }
        var down = new NativeInput { Type = 0, Data = new InputUnion { Mouse = new MouseInput { Flags = 0x0002 } } };
        var up = new NativeInput { Type = 0, Data = new InputUnion { Mouse = new MouseInput { Flags = 0x0004 } } };
        // A single input batch preserves move/down/up order without an await
        // gap in which physical mouse movement can redirect the click.
        var sent = SendInput(3, new[] { move.Value, down, up }, Marshal.SizeOf<NativeInput>());
        diagnosticMouseStatus = $"Mouse messages sent: {sent}";
        return Task.FromResult(sent == 3);
    }

    private static bool MoveDiagnosticPointer(int x, int y)
    {
        var input = DiagnosticPointerMove(x, y);
        return input is not null && SendInput(1, new[] { input.Value }, Marshal.SizeOf<NativeInput>()) == 1;
    }
    private static NativeInput? DiagnosticPointerMove(int x, int y)
    {
        var width = GetSystemMetrics(78); var height = GetSystemMetrics(79);
        if (width <= 1 || height <= 1) return null;
        var input = new NativeInput { Type = 0, Data = new InputUnion { Mouse = new MouseInput
        {
            X = Math.Clamp((int)((x - GetSystemMetrics(76) + 0.5) * 65536d / width), 0, 65535),
            Y = Math.Clamp((int)((y - GetSystemMetrics(77) + 0.5) * 65536d / height), 0, 65535),
            Flags = 0xC001 // MOVE | ABSOLUTE | VIRTUALDESK
        } } };
        return input;
    }

    [StructLayout(LayoutKind.Sequential)] private struct CursorPoint { public int X; public int Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out CursorPoint point);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(CursorPoint point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}

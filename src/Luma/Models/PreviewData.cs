using System;
using System.Windows;
using System.Windows.Media;

namespace Luma.Models;

public static class PreviewData
{
    public static MediaState Media() => new()
    {
        Available = true, SessionId = "luma-preview", AppName = "Önizleme", Title = "Blue hour", Artist = "Luma Sessions",
        IsPlaying = true, CanToggle = true, CanPrevious = true, CanNext = true, CanSeek = true,
        End = TimeSpan.FromSeconds(222), Position = TimeSpan.FromSeconds(72), Artwork = Artwork()
    };

    public static ImageSource Artwork()
    {
        var group = new DrawingGroup();
        using (var drawing = group.Open())
        {
            var background = new LinearGradientBrush(Color.FromRgb(32, 57, 84), Color.FromRgb(175, 136, 140), 60);
            drawing.DrawRectangle(background, null, new Rect(0, 0, 320, 320));
            var glow = new RadialGradientBrush(Color.FromArgb(220, 235, 201, 165), Color.FromArgb(0, 235, 201, 165));
            drawing.DrawEllipse(glow, null, new Point(200, 120), 115, 115);
            drawing.DrawEllipse(new SolidColorBrush(Color.FromRgb(230, 201, 166)), null, new Point(202, 118), 38, 38);
            drawing.DrawGeometry(new SolidColorBrush(Color.FromRgb(39, 67, 86)), null, Geometry.Parse("M0,180 C80,120 100,220 180,180 C230,155 270,175 320,130 L320,320 L0,320 Z"));
            drawing.DrawGeometry(new SolidColorBrush(Color.FromRgb(24, 43, 64)), null, Geometry.Parse("M0,230 C80,180 150,260 230,205 L320,230 L320,320 L0,320 Z"));
            for (var i = 0; i < 6; i++) drawing.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(35, 222, 211, 198)), 1), new Point(0, 260 + i * 8), new Point(320, 250 + i * 9));
        }
        var image = new DrawingImage(group); image.Freeze(); return image;
    }
}

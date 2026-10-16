using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Luma.Themes;

internal static class BrandIcon
{
    public static Icon CreateTrayIcon()
    {
        var source = (ImageSource)Application.Current.FindResource("BrandAppIcon");
        var sizes = new[] { 16, 20, 24, 32, 40, 48, 64 };
        var images = new byte[sizes.Length][];
        for (var i = 0; i < sizes.Length; i++)
        {
            var bitmap = Render(source, sizes[i]);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var png = new MemoryStream(); encoder.Save(png); images[i] = png.ToArray();
        }
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            var offset = 6 + 16 * sizes.Length;
            for (var i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)sizes[i]); writer.Write((byte)sizes[i]); writer.Write((byte)0); writer.Write((byte)0);
                writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(images[i].Length); writer.Write(offset);
                offset += images[i].Length;
            }
            foreach (var bytes in images) writer.Write(bytes);
        }
        stream.Position = 0;
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }

    internal static BitmapSource Render(ImageSource source, int size)
    {
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen()) drawing.DrawImage(source, new Rect(0, 0, size, size));
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
}

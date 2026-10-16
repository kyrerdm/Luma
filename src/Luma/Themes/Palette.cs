using System.Windows;
using System.Windows.Media;

namespace Luma.Themes;

public static class Palette
{
    public static void Apply(string accent)
    {
        var color = accent switch { "Lilac" => "#B398EE", "Mint" => "#88C9A4", _ => "#81B7DB" };
        Application.Current.Resources["AccentBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
}

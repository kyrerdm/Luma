using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Luma.Themes;

public sealed record AccentOption(string Id, string Name, string Hex)
{
    public Color Color => (Color)ColorConverter.ConvertFromString(Hex);
}

public static class Palette
{
    public static IReadOnlyList<AccentOption> Options { get; } = Array.AsReadOnly(new[]
    {
        new AccentOption("Ice", "Buz mavisi", "#81B7DB"),
        new AccentOption("Lilac", "Lila", "#B398EE"),
        new AccentOption("Mint", "Adaçayı", "#88C9A4"),
        new AccentOption("Rose", "Gül kurusu", "#EE9FBB"),
        new AccentOption("Red", "Yakut", "#EE8E92"),
        new AccentOption("Coral", "Mercan", "#EF9D8C"),
        new AccentOption("Peach", "Şeftali", "#EFB489"),
        new AccentOption("Amber", "Kehribar", "#E7BD73"),
        new AccentOption("Lemon", "Limon", "#D8D77B"),
        new AccentOption("Teal", "Turkuaz", "#77CBC5"),
        new AccentOption("Ocean", "Okyanus", "#7D9FEB"),
        new AccentOption("Silver", "Gümüş", "#C6CBD6")
    });

    public static bool IsKnown(string? id) => Options.Any(option => option.Id == id);
    public static AccentOption Resolve(string? id) => Options.FirstOrDefault(option => option.Id == id) ?? Options[0];

    public static void Apply(string accent)
    {
        var brush = new SolidColorBrush(Resolve(accent).Color);
        brush.Freeze();
        Application.Current.Resources["AccentBrush"] = brush;
    }
}

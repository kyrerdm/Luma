using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace Luma.Services;

internal static class Strings
{
    public const string DefaultLanguage = "en";
    public static IReadOnlyDictionary<string, string> Languages { get; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["en"] = "English", ["tr"] = "Türkçe", ["de"] = "Deutsch", ["es"] = "Español", ["it"] = "Italiano"
    });
    internal static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Catalogs { get; } = LoadCatalogs();
    private static readonly Dictionary<string, string> Originals = BuildOriginals();
    private sealed record Translation(string Original, string Rendered);
    private static readonly ConditionalWeakTable<DependencyObject, Dictionary<DependencyProperty, Translation>> Translations = new();
    private static readonly ConditionalWeakTable<DependencyObject, object> Excluded = new();
    public static string Language { get; private set; } = DefaultLanguage;
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Language);
    public static event Action? Changed;

    public static bool IsSupported(string? language) => language is not null && Languages.ContainsKey(language);
    public static string T(string text)
    {
        var original = Catalogs["en"].ContainsKey(text) ? text : Originals.GetValueOrDefault(text, text);
        return Language == "tr" ? original : Catalogs[Language].GetValueOrDefault(original, original);
    }
    public static string Format(string text, params object[] arguments) => string.Format(Culture, T(text), arguments);
    public static string Percent(double value) => Language == "tr" ? "%" + value.ToString("0", Culture) : value.ToString("0", Culture) + " %";
    public static void Exclude(DependencyObject node) => Excluded.GetValue(node, _ => new object());
    public static void SetLanguage(string language)
    {
        var next = IsSupported(language) ? language : DefaultLanguage;
        if (next == Language) return;
        Language = next;
        foreach (var option in Luma.Themes.Palette.Options) option.RefreshLanguage();
        Changed?.Invoke();
    }
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadCatalogs()
    {
        var catalogs = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var language in new[] { "en", "de", "es", "it" })
        {
            using var stream = typeof(Strings).Assembly.GetManifestResourceStream("Luma.Localization." + language + ".json") ?? throw new InvalidOperationException("Missing language resource: " + language);
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? throw new InvalidOperationException("Invalid language resource: " + language);
            catalogs.Add(language, new ReadOnlyDictionary<string, string>(values));
        }
        return new ReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>(catalogs);
    }
    private static Dictionary<string, string> BuildOriginals()
    {
        var originals = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var catalog in Catalogs.Values)
            foreach (var pair in catalog) originals.TryAdd(pair.Value, pair.Key);
        return originals;
    }
    private static void Translate(DependencyObject node, DependencyProperty property)
    {
        if (node.GetValue(property) is not string { Length: > 0 } current) return;
        var saved = Translations.GetOrCreateValue(node);
        var original = saved.TryGetValue(property, out var previous) && previous.Rendered == current
            ? previous.Original : Catalogs["en"].ContainsKey(current) ? current : Originals.GetValueOrDefault(current, current);
        if (!Catalogs["en"].ContainsKey(original)) { saved.Remove(property); return; }
        // Keep each control's source key. Reverse lookup alone loses context
        // when two translations happen to use the same word in another language.
        var rendered = Language == "tr" ? original : Catalogs[Language].GetValueOrDefault(original, original);
        saved[property] = new(original, rendered);
        node.SetCurrentValue(property, rendered);
    }
    public static void Apply(DependencyObject root)
    {
        if (root is FrameworkElement element) element.SetCurrentValue(FrameworkElement.LanguageProperty, XmlLanguage.GetLanguage(Language));
        var visited = new HashSet<DependencyObject>();
        void Visit(DependencyObject node)
        {
            if (!visited.Add(node) || Excluded.TryGetValue(node, out _)) return;
            if (node is Window) Translate(node, Window.TitleProperty);
            if (node is TextBlock) Translate(node, TextBlock.TextProperty);
            if (node is ContentControl) Translate(node, ContentControl.ContentProperty);
            if (node is HeaderedItemsControl) Translate(node, HeaderedItemsControl.HeaderProperty);
            if (node is FrameworkElement) Translate(node, FrameworkElement.ToolTipProperty);
            Translate(node, AutomationProperties.NameProperty);
            Translate(node, AutomationProperties.HelpTextProperty);
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) Visit(child);
            if (node is Visual) for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Visit(VisualTreeHelper.GetChild(node, i));
        }
        Visit(root);
    }
}

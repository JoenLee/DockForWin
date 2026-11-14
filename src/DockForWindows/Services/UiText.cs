using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace DockForWindows.Services;

// Only built-in literals pass through here. User content, file paths and system-provided names stay untouched.
public static class UiText
{
    private static readonly IReadOnlyDictionary<string,string> English=LoadEnglish();
    public static bool IsEnglish { get; private set; }
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(IsEnglish?"en-US":"zh-CN");
    public static void SetLanguage(string? language)
    {
        IsEnglish=language=="en-US";
        CultureInfo.DefaultThreadCurrentCulture=Culture;
        CultureInfo.DefaultThreadCurrentUICulture=Culture;
        CultureInfo.CurrentCulture=Culture;
        CultureInfo.CurrentUICulture=Culture;
    }
    public static string Get(string text)=>IsEnglish&&English.TryGetValue(text,out var translated)?translated:text;
    public static string Format(FormattableString text)=>string.Format(Culture,Get(text.Format),text.GetArguments());
    private static IReadOnlyDictionary<string,string> LoadEnglish()
    {
        using Stream stream=typeof(UiText).Assembly.GetManifestResourceStream("DockForWindows.Resources.Strings.en-US.json")
            ??throw new InvalidOperationException("English UI resources are missing.");
        return JsonSerializer.Deserialize<Dictionary<string,string>>(stream)??throw new InvalidOperationException("English UI resources are invalid.");
    }
}

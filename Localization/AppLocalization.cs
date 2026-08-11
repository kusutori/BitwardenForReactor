using System;
using System.Collections.Generic;
using Microsoft.UI.Reactor.Localization;

namespace BitwardenForReactor.Localization;

public static class AppLocales
{
    public const string SimplifiedChinese = "zh-CN";
    public const string English = "en-US";

    public static string FromLanguage(Services.AppLanguage language) => language switch
    {
        Services.AppLanguage.English => English,
        _ => SimplifiedChinese
    };
}

public sealed class AppResourceProvider : IStringResourceProvider
{
    public static AppResourceProvider Instance { get; } = new();

    private readonly ReswResourceProvider _resw = new(AppLocales.SimplifiedChinese);

    private AppResourceProvider()
    {
    }

    public string? GetString(string locale, string ns, string key)
    {
        if (locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return key;
        }

        return _resw.GetString(locale, ns, key);
    }
}

public static class AppText
{
    private static IntlAccessor? _accessor;

    public static void Use(IntlAccessor accessor) => _accessor = accessor;

    public static string T(string source) =>
        _accessor?.Message(new MessageKey("App", source)) ?? source;

    public static string T(string source, params (string Name, object? Value)[] arguments)
    {
        if (_accessor is null)
        {
            return source;
        }

        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (name, value) in arguments)
        {
            values[name] = value ?? string.Empty;
        }

        return _accessor.Message(new MessageKey("App", source), values);
    }
}

using System.Windows;

namespace WandRuInstaller.Core;

/// <summary>Локализованные строки для ViewModel. Читает из merged-словарей приложения; вне WPF — возвращает ключ.</summary>
public static class L
{
    public static string Get(string key)
    {
        var app = Application.Current;
        if (app is not null && app.TryFindResource(key) is string s) return s;
        return key;
    }
}

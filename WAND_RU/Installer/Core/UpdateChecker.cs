using System.Net.Http;
using System.Text.Json;

namespace WandRuInstaller.Core;

/// <summary>Проверка новой версии на GitHub Releases. Тихая: офлайн/ошибка/нет обновления -> null.</summary>
public static class UpdateChecker
{
    public const string LatestApi = "https://api.github.com/repos/LockBB1/Wand-RU-Patcher/releases/latest";
    public const string ReleasesUrl = "https://github.com/LockBB1/Wand-RU-Patcher/releases/latest";

    /// <summary>tag новее current? Понимает "v0.12.0"/"0.12.0"; суффиксы (+build, -rc) отрезаются.</summary>
    public static bool IsNewer(string tag, string current) =>
        TryParse(tag, out var t) && TryParse(current, out var c) && t > c;

    static bool TryParse(string s, out Version v)
    {
        s = s.Trim().TrimStart('v', 'V').Split('+', '-')[0];
        if (!s.Contains('.')) s += ".0";
        return Version.TryParse(s, out v!);
    }

    /// <summary>Номер новой версии (без 'v') или null. handler - для тестов (мок HTTP).</summary>
    public static async Task<string?> CheckAsync(string currentVersion, HttpMessageHandler? handler = null)
    {
        try
        {
            using var http = handler is null ? new HttpClient() : new HttpClient(handler);
            http.Timeout = TimeSpan.FromSeconds(5);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("WandRuInstaller"); // GitHub API требует UA
            var json = await http.GetStringAsync(LatestApi).ConfigureAwait(false);
            var tag = JsonDocument.Parse(json).RootElement.GetProperty("tag_name").GetString();
            return tag is not null && IsNewer(tag, currentVersion) ? tag.TrimStart('v', 'V') : null;
        }
        catch { return null; }
    }
}

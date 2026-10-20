using System.IO;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using WandRuInstaller.Models;

namespace WandRuInstaller.Core.Runtime;

internal static class RuntimeScripts
{
    static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    static readonly Regex MapPath = new(@"^/(?:ru/)?maps/([^/]+)(?:/|$)", RegexOptions.CultureInvariant);
    static readonly Regex AssistantPath = new(@"^/(?:[a-z]{2}(?:-[a-zA-Z]{2})?/)?assistant/embed/?$", RegexOptions.CultureInvariant);

    internal static bool IsMap(string url) => FrameAddress(url) is { } uri && uri.Host is "wand.com" or "www.wand.com" && MapPath.IsMatch(uri.AbsolutePath);

    static Uri? FrameAddress(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 ? uri : null;

    public static string? FrameScript(string url, AppSettings settings)
    {
        if (!settings.TranslateMaps || FrameAddress(url) is not { } uri) return null;
        string seed;
        if (IsMap(url))
        {
            using var maps = JsonDocument.Parse(MapFrameHook.MapsJson);
            var dictionary = new Dictionary<string, string>();
            foreach (var slug in new[] { "_common", MapPath.Match(uri.AbsolutePath).Groups[1].Value })
                if (maps.RootElement.TryGetProperty(slug, out var entries))
                    foreach (var entry in entries.EnumerateObject()) dictionary[entry.Name] = entry.Value.GetString()!;
            seed = $"window.__WANDRU_MTON={(settings.TranslateMapsOnline ? "true" : "false")};window.__WANDRU_SEED={JsonSerializer.Serialize(dictionary, JsonOptions)};";
        }
        else if (uri.Host == "mist.wand.com" && AssistantPath.IsMatch(uri.AbsolutePath))
        {
            using var assistant = JsonDocument.Parse(MapFrameHook.AssistJson);
            seed = $"window.__WANDRU_UIONLY=true;window.__WANDRU_SEED={assistant.RootElement.GetProperty("strings").GetRawText()};" +
                $"window.__WANDRU_SEED_TPL={assistant.RootElement.GetProperty("templates").GetRawText()};";
        }
        else return null;
        return $$"""
            (function () {
              var url = new URL(window.location.href);
              if (window === window.top || url.origin !== {{JsonSerializer.Serialize(uri.GetLeftPart(UriPartial.Authority))}} ||
                  url.pathname !== {{JsonSerializer.Serialize(uri.AbsolutePath)}}) return;
              {{seed}}
              {{MapFrameHook.Translator}}
              return window.__wandruTr === 1;
            })();
            """;
    }

    public static string? EmbedUrl(string url)
    {
        if (FrameAddress(url) is not { } uri) return null;
        string before, after;
        if (IsMap(url) && uri.AbsolutePath.StartsWith("/ru/maps/", StringComparison.Ordinal))
            (before, after) = ("/ru/maps/", "/maps/");
        else if (uri.Host == "mist.wand.com" && uri.AbsolutePath is "/ru/assistant/embed" or "/ru/assistant/embed/")
            (before, after) = ("/ru/assistant/embed", "/en/assistant/embed");
        else return null;
        var start = url.IndexOf('/', url.IndexOf("://", StringComparison.Ordinal) + 3);
        return url[..start] + after + url[(start + before.Length)..];
    }

    public static string MainScript(string appDir, AppSettings settings, (string Path, string Identity)? activation = null)
    {
        var pages = new[] { "index.html", "overlay.html" }
            .Select(name => new Uri(Path.GetFullPath(Path.Combine(appDir, "resources", "app.asar", name))).AbsoluteUri).ToArray();
        var overrides = RuOverrides.LoadEmbedded();
        var configuration = new { translations = overrides.Translations, language = overrides.LanguageNative,
            flag = JsLocalePatch.RussianFlagDataUri, translateCheats = settings.TranslateCheats,
            activationFile = activation?.Path, activationIdentity = activation?.Identity };
        var prefix = $$"""
            (function () {
              var allowed = {{JsonSerializer.Serialize(pages, JsonOptions)}};
              var current = new URL(window.location.href);
              current.search = ''; current.hash = '';
              if (current.protocol !== 'file:' || !allowed.some(function (url) { return url.toLowerCase() === current.href.toLowerCase(); })) return;
              window.__WANDRU_RUNTIME_CONFIG = {{JsonSerializer.Serialize(configuration, JsonOptions)}};
            """;
        var online = settings.TranslateMaps && settings.TranslateMapsOnline
            ? "\n(function(){\n" + Regex.Replace(LoadText("cheat-online.js"), @"^export\s+", "", RegexOptions.Multiline) +
                "\n" + LoadText("runtime-map-online.js") + "\n})();" : "";
        return prefix + "\n" + (settings.TranslateCheats ? CheatHook.LoadEmbedded() : "") + online + "\n" + LoadText("runtime-ui.js") + "\n})();";
    }

    static string LoadText(string suffix)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

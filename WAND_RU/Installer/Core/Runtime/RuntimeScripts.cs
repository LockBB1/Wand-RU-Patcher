using System.IO;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using WandRuInstaller.Models;

namespace WandRuInstaller.Core.Runtime;

internal static class RuntimeScripts
{
    static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

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
        return prefix + "\n" + (settings.TranslateCheats ? CheatHook.LoadEmbedded() : "") + "\n" + LoadText("runtime-ui.js") + "\n})();";
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

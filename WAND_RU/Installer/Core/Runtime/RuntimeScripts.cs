using System.IO;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using WandRuInstaller.Models;

namespace WandRuInstaller.Core.Runtime;

internal static class RuntimeScripts
{
    static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string MainScript(string appDir, AppSettings settings)
    {
        var pages = new[] { "index.html", "overlay.html" }
            .Select(name => new Uri(Path.GetFullPath(Path.Combine(appDir, "resources", "app.asar", name))).AbsoluteUri).ToArray();
        var configuration = new { translations = RuOverrides.LoadEmbedded().Translations, translateCheats = settings.TranslateCheats };
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

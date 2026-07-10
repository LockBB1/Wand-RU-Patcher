using System.IO;
using System.Reflection;
using System.Text.Json;

namespace WandRuInstaller.Models;

public sealed class RuOverrides
{
    public string LanguageNative { get; init; } = "Русский";
    public Dictionary<string, string> Translations { get; init; } = new();

    public static RuOverrides LoadEmbedded()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith("ru-overrides.json", StringComparison.OrdinalIgnoreCase));
        using var s = asm.GetManifestResourceStream(name)!;
        return Parse(s);
    }

    public static RuOverrides Parse(Stream json)
    {
        using var doc = JsonDocument.Parse(json);
        return FromRoot(doc.RootElement);
    }

    public static RuOverrides Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return FromRoot(doc.RootElement);
    }

    static RuOverrides FromRoot(JsonElement root)
    {
        var native = root.TryGetProperty("language", out var lang) && lang.TryGetProperty("native", out var n)
            ? n.GetString() ?? "Русский"
            : "Русский";
        var tr = new Dictionary<string, string>();
        if (root.TryGetProperty("translations", out var t))
            foreach (var p in t.EnumerateObject())
                tr[p.Name] = p.Value.GetString() ?? "";
        return new RuOverrides { LanguageNative = native, Translations = tr };
    }
}

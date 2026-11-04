using System.Text.Json;

namespace WandRuInstaller.Core;

internal static class MapFrameHook
{
    static string? _translator, _mapsJson, _assistJson;
    internal static string Translator => _translator ??= LoadEmbedded("map-translator.js");
    // Пер-карта офлайн-словари {slug:{en:ru}} из ресурсов maps.<slug>.json -> JS-объект (__MAPS__).
    internal static string MapsJson => _mapsJson ??= BuildMapsJson();
    // UI фрейма AI-помощника {strings,templates} -> JS-объект (__ASSIST__). Тот же Path D, что карты.
    internal static string AssistJson => _assistJson ??= BuildAssistJson();

    // Кириллица литеральная (не \uXXXX) - втрое компактнее в index.js, как оригинальные локали.
    static readonly JsonSerializerOptions MapsOpts = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // ceiling: все карты бакуются в index.js целиком (RDR2 ~350КБ ок). При десятках карт (видение 190)
    // это раздует index.js - тогда грузить словарь по slug иначе (напр. отдельный файл в asar). Пока YAGNI.
    static string BuildMapsJson()
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var maps = new Dictionary<string, JsonElement>();
        foreach (var n in asm.GetManifestResourceNames()
            .Where(n => n.StartsWith("maps.", StringComparison.OrdinalIgnoreCase)
                     && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            var slug = n.Substring("maps.".Length, n.Length - "maps.".Length - ".json".Length);
            using var s = asm.GetManifestResourceStream(n)!;
            using var r = new System.IO.StreamReader(s);
            maps[slug] = JsonSerializer.Deserialize<JsonElement>(r.ReadToEnd());
        }
        return JsonSerializer.Serialize(maps, MapsOpts);   // валидный JS-литерал в index.js
    }

    // Словарь UI помощника из ресурса assistant-ui.json. `_note` (происхождение строк) в JS не тащим -
    // это документация для нас, а не данные хука.
    static string BuildAssistJson()
    {
        var doc = JsonSerializer.Deserialize<JsonElement>(LoadEmbedded("assistant-ui.json"));
        var outp = new Dictionary<string, JsonElement>();
        foreach (var p in doc.EnumerateObject())
            if (p.Name != "_note") outp[p.Name] = p.Value;
        return JsonSerializer.Serialize(outp, MapsOpts);   // литеральная кириллица, как MapsJson
    }

    static string LoadEmbedded(string suffix)
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        using var s = asm.GetManifestResourceStream(name)!;
        using var r = new System.IO.StreamReader(s);
        return r.ReadToEnd().Trim();
    }

}

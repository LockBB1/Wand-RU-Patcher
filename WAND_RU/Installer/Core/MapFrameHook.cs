using System.Text.Json;
using System.Text.RegularExpressions;

namespace WandRuInstaller.Core;

/// <summary>
/// Path D (перевод карт): патч main-процесса index.js. Карта Wand - cross-origin &lt;iframe&gt;
/// (wand.com), SOP не даёт renderer-хуку до неё дотянуться. Из main-процесса же
/// webFrameMain.executeJavaScript впрыскивает скрипт прямо в контекст фрейма (обход SOP).
///
/// Вешаем did-frame-navigate на главное окно; при навигации подфрейма на wand.com/maps впрыск
/// переводчика (renderer/map-translator.mjs) в фрейм. Переводчик: словарь -> мгновенный офлайн,
/// промахи -> в main через console.log (WANDRU_MTREQ), main переводит через o.net Google gtx и
/// зовёт window.__wandruApply обратно. Канал в лог инсталлера - o.net POST на MapDiagServer :39271
/// (НЕ fs: require/fs в этой ncc-точке не в scope + OneDrive режет запись; loopback без CORS).
///
/// JS вынесен в читаемые renderer/map-translator.mjs + renderer/map-mainhook.mjs (встроены как
/// ресурсы). Якорь структурный с захватом минифицированных имён (win/electron) - устойчив к ренейму
/// (Wand 12.36-12.38 сверено). Пере-патч: strip-then-reinject (парные маркеры) ставит актуальный хук.
/// integrity-fuse не нужен: index.js внутри asar, AsarIntegrity пере-синкает хэш после repack.
/// </summary>
public static class MapFrameHook
{
    public const string Marker = "__WANDRU_MAPHOOK__";

    // <win>=new <electron>.BrowserWindow(<opts>.windowOptions) - создание главного окна Wand.
    static readonly Regex MainWindow = new(
        @"(\w+)=new (\w+)\.BrowserWindow\((\w+)\.windowOptions\)", RegexOptions.Compiled);

    // JS-хуки вынесены в читаемые renderer/*.mjs (встроены как ресурсы map-translator.js /
    // map-mainhook.js). map-translator = переводчик текст-узлов (в map-фрейм), map-mainhook =
    // main-процесс (did-frame-navigate инъектор + o.net Google MT + relay). Ленивая загрузка + кэш.
    static string? _translator, _mainhook, _mapsJson;
    static string Translator => _translator ??= LoadEmbedded("map-translator.js");
    static string MainHook => _mainhook ??= LoadEmbedded("map-mainhook.js");
    // Пер-карта офлайн-словари {slug:{en:ru}} из ресурсов maps.<slug>.json -> JS-объект (__MAPS__).
    static string MapsJson => _mapsJson ??= BuildMapsJson();

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

    static string LoadEmbedded(string suffix)
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        using var s = asm.GetManifestResourceStream(name)!;
        using var r = new System.IO.StreamReader(s);
        return r.ReadToEnd().Trim();
    }

    // Прошлый блок хука (парные маркеры) - для strip-then-reinject: пере-патч ставит АКТУАЛЬНЫЙ хук,
    // а не сохраняет старый (иначе обновление дампера/переводчика не встанет поверх). Singleline на всякий.
    static readonly Regex ExistingBlock = new(
        @";/\*__WANDRU_MAPHOOK__\*/.*?/\*__WANDRU_MAPHOOK_END__\*/",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // Легаси-блок (0.16.3-0.16.8, до END-маркера): заканчивался FATAL-фоллбэком. Снимаем ПОСЛЕ
    // ExistingBlock (иначе съест хвост нового блока до его END). Иначе накопление двойных хуков.
    static readonly Regex LegacyBlock = new(
        @";/\*__WANDRU_MAPHOOK__\*/.*?FATAL ""\+e\)\}catch\(_\)\{\}\}",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public static bool IsPatched(string js) => js.Contains(Marker);

    /// <summary>Есть якорь главного окна - патчим (пере-инжектим для актуальности, даже если хук уже стоит).</summary>
    public static bool NeedsPatch(string js) => MainWindow.IsMatch(js);

    /// <summary>Снять прошлый хук-блок без реинжекта (перевод карт выключён -> убираем хук из index.js).</summary>
    public static string Strip(string js)
    {
        var clean = ExistingBlock.Replace(js, "");   // END-маркированные блоки (0.16.9+)
        return LegacyBlock.Replace(clean, "");        // легаси-блоки без END (0.16.3-0.16.8)
    }

    /// <summary>Снимает прошлый хук-блок и вставляет актуальный после создания главного окна. Идемпотентно.
    /// mapOnline = онлайн-добор карт (Google/MyMemory); diag = диагностика в инсталлер (:39271).</summary>
    public static string Patch(string js, bool mapOnline = true, bool diag = false)
    {
        if (!MainWindow.IsMatch(js)) return js;
        var clean = Strip(js);
        var dumpLit = JsonSerializer.Serialize(Translator);   // переводчик как JS-строковый литерал
        return MainWindow.Replace(clean, m =>
        {
            var win = m.Groups[1].Value;   // главное окно (минифиц. имя)
            var el = m.Groups[2].Value;    // алиас require("electron")
            var inject = MainHook
                .Replace("__WIN__", win)
                .Replace("__EL__", el)
                .Replace("__DUMP__", dumpLit)
                .Replace("__MTON__", mapOnline ? "true" : "false")
                .Replace("__DIAG__", diag ? "true" : "false")
                .Replace("__MAPS__", MapsJson);   // последним: контент словаря не должен ре-подставляться
            return m.Value + inject;
        }, 1);
    }
}

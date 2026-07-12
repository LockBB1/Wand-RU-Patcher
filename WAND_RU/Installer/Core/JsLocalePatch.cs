using System.Text.RegularExpressions;

namespace WandRuInstaller.Core;

/// <summary>
/// Регистрирует локаль ru-RU в JS-бандлах Wand. Якоря структурные (не по жёсткому списку языков),
/// идемпотентны. Если якорь не найден - JS возвращается без изменений.
/// </summary>
public static class JsLocalePatch
{
    public const string RussianFlagDataUri =
        "data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCA5MDAgNjAwIj48cGF0aCBmaWxsPSIjZmZmIiBkPSJNMCAwaDkwMHY2MDBIMHoiLz48cGF0aCBmaWxsPSIjMDAzOWE2IiBkPSJNMCAyMDBoOTAwdjQwMEgweiIvPjxwYXRoIGZpbGw9IiNkNTJiMWUiIGQ9Ik0wIDQwMGg5MDB2MjAwSDB6Ii8+PC9zdmc+";

    // Список локалей: массив BCP-47, начинающийся с "en-US" и содержащий хотя бы ещё одну локаль
    // (+ , не *): иначе ловит не-списки - ["en-US"], ["en-US",f] (Map), delete m["en-US"].
    static readonly Regex LocaleList = new("(\\[\"en-US\"(?:,\"[a-z]{2}-[A-Z]{2}\")+)", RegexOptions.Compiled);

    // Признак списка ЯЗЫКОВ (а не e-mail доменов): есть short-pair с не-ASCII значением (native-имя:
    // "português"/"한국어"/"Türkçe"). E-mail typo-map (["vcom","com"]...) - весь ASCII, не подходит.
    static readonly Regex LangListGuard = new("\\[\"[a-z]{2}\",\"[^\"]*[^\\x00-\\x7F][^\"]*\"\\]", RegexOptions.Compiled);

    // Метаданные языков: последняя запись xx:{name,native,locale} перед закрывающими }} (не {).
    static readonly Regex LangMetaTail = new(
        "([a-z]{2}:\\{name:\"[^\"]+\",native:\"[^\"]+\",locale:\"[a-z]{2}-[A-Z]{2}\"\\})(\\}(?!\\{))",
        RegexOptions.Compiled);

    // Пары [код,подпись], список закрывается );  (напр. i18n init).
    static readonly Regex ShortPairTail = new("(\\[\"[a-z]{2}\",\"[^\"]+\"\\])(\\]\\);)", RegexOptions.Compiled);

    // Пары [BCP-47, <var|строка>], закрывается ]) - для флагов.
    static readonly Regex FlagPairTail = new(
        "(\\[\"[a-z]{2}-[A-Z]{2}\",(?:\"[^\"]*\"|[A-Za-z_$][\\w$]*)\\])(\\]\\))",
        RegexOptions.Compiled);

    // Сигнатура порчи от жадного якоря: за ["en-US","ru-RU" сразу ] или запятая+не-кавычка (напр.
    // ["en-US","ru-RU",f] или delete m["en-US","ru-RU"]). Валидный список - только ["en-US","ru-RU","xx"...
    static readonly Regex LocaleCorruption = new("\\[\"en-US\",\"ru-RU\"(?:\\]|,(?!\"))", RegexOptions.Compiled);

    /// <summary>Патч попал не в список локалей (регресс якоря на новой версии Wand)?</summary>
    public static bool HasCorruption(string js) => LocaleCorruption.IsMatch(js);

    // --- Внешние embed-URL wand.com/mist.wand.com: локаль UI течёт в path-сегмент ---
    // Wand строит карту как `${base}${lang!=="en"?"/"+lang:""}/maps/...` и ассистента как
    // `${base}/${lang}/assistant/embed`. С патчем lang="ru" -> /ru/maps, /ru/assistant/embed, но
    // сайты не имеют /ru роутов -> 404 (BLOCKED) -> карта и ассистент не грузятся вовсе.
    // Нормализуем сегмент к рабочему дефолту: карта - без префикса (англ.-сайт, как у EN-юзера),
    // ассистент - /en. Оба - точечные минифицированные шейпы, стабильны на Wand 12.36-12.38.
    static readonly Regex MapLocalePrefix = new(
        "([a-z])\\.language&&\"en\"!==\\1\\.language\\?`/\\$\\{\\1\\.language\\}`:\"\"",
        RegexOptions.Compiled);
    static readonly Regex AssistantLocaleSeg = new(
        "/\\$\\{[a-z]+\\}/assistant/embed", RegexOptions.Compiled);

    /// <summary>Убирает ru-префикс из внешних embed-URL (иначе 404 -> карта/ассистент не грузятся). Идемпотентно.</summary>
    public static string NeutralizeEmbedLocale(string js)
    {
        var text = MapLocalePrefix.Replace(js, "\"\"");
        return AssistantLocaleSeg.Replace(text, "/en/assistant/embed");
    }

    public static bool NeedsPatch(string js) =>
        !js.Contains("\"ru-RU\"") &&
        (LocaleList.IsMatch(js) || LangMetaTail.IsMatch(js) || ShortPairTail.IsMatch(js));

    public static string Patch(string js, string nativeName, string flagDataUri)
    {
        var text = js;

        if (!text.Contains("\"ru-RU\"") && LocaleList.IsMatch(text))
            text = LocaleList.Replace(text, m => m.Value.Insert("[\"en-US\"".Length, ",\"ru-RU\""), 1);

        if (!text.Contains("ru:{name:\"Russian\"") && LangMetaTail.IsMatch(text))
            text = LangMetaTail.Replace(text,
                m => $"{m.Groups[1].Value},ru:{{name:\"Russian\",native:\"{nativeName}\",locale:\"ru-RU\"}}{m.Groups[2].Value}", 1);

        if (!Regex.IsMatch(text, "\\[\"ru\",") && LangListGuard.IsMatch(text) && ShortPairTail.IsMatch(text))
            text = ShortPairTail.Replace(text,
                m => $"{m.Groups[1].Value},[\"ru\",\"{nativeName}\"]{m.Groups[2].Value}", 1);

        if (!Regex.IsMatch(text, "\\[\"ru-RU\",") && FlagPairTail.IsMatch(text))
            text = FlagPairTail.Replace(text,
                m => $"{m.Groups[1].Value},[\"ru-RU\",\"{flagDataUri}\"]{m.Groups[2].Value}", 1);

        return text;
    }
}

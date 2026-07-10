using System.Text.RegularExpressions;

namespace WandRuInstaller.Core;

/// <summary>
/// Регистрирует локаль ru-RU в JS-бандлах Wand. Якоря структурные (не по жёсткому списку языков),
/// идемпотентны. Если якорь не найден — JS возвращается без изменений.
/// </summary>
public static class JsLocalePatch
{
    public const string RussianFlagDataUri =
        "data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCA5MDAgNjAwIj48cGF0aCBmaWxsPSIjZmZmIiBkPSJNMCAwaDkwMHY2MDBIMHoiLz48cGF0aCBmaWxsPSIjMDAzOWE2IiBkPSJNMCAyMDBoOTAwdjQwMEgweiIvPjxwYXRoIGZpbGw9IiNkNTJbMWUiIGQ9Ik0wIDQwMGg5MDB2MjAwSDB6Ii8+PC9zdmc+";

    // Список локалей: массив BCP-47, начинающийся с "en-US"; вставить "ru-RU" сразу после.
    static readonly Regex LocaleList = new("(\\[\"en-US\"(?:,\"[a-z]{2}-[A-Z]{2}\")*)", RegexOptions.Compiled);

    // Метаданные языков: последняя запись xx:{name,native,locale} перед закрывающими }} (не {).
    static readonly Regex LangMetaTail = new(
        "([a-z]{2}:\\{name:\"[^\"]+\",native:\"[^\"]+\",locale:\"[a-z]{2}-[A-Z]{2}\"\\})(\\}(?!\\{))",
        RegexOptions.Compiled);

    // Пары [код,подпись], список закрывается );  (напр. i18n init).
    static readonly Regex ShortPairTail = new("(\\[\"[a-z]{2}\",\"[^\"]+\"\\])(\\]\\);)", RegexOptions.Compiled);

    // Пары [BCP-47, <var|строка>], закрывается ]) — для флагов.
    static readonly Regex FlagPairTail = new(
        "(\\[\"[a-z]{2}-[A-Z]{2}\",(?:\"[^\"]*\"|[A-Za-z_$][\\w$]*)\\])(\\]\\))",
        RegexOptions.Compiled);

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

        if (!Regex.IsMatch(text, "\\[\"ru\",") && ShortPairTail.IsMatch(text))
            text = ShortPairTail.Replace(text,
                m => $"{m.Groups[1].Value},[\"ru\",\"{nativeName}\"]{m.Groups[2].Value}", 1);

        if (!Regex.IsMatch(text, "\\[\"ru-RU\",") && FlagPairTail.IsMatch(text))
            text = FlagPairTail.Replace(text,
                m => $"{m.Groups[1].Value},[\"ru-RU\",\"{flagDataUri}\"]{m.Groups[2].Value}", 1);

        return text;
    }
}

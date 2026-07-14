namespace WandRuInstaller.Models;

/// <summary>
/// Что РЕАЛЬНО легло в патч. Раньше «Готово» печаталось безусловно: якорь читов/карт мог не найтись
/// на новой версии Wand - юзер уходил довольным с недопатченным Wand. Промах якоря карт - осознанный
/// best-effort (не фейл), поэтому честный отчёт, а не исключение.
/// Cheats/Maps: null = компонент не запрашивали.
/// </summary>
public sealed record PatchReport(bool Locale, bool Flag, bool LangName, bool? Cheats, bool? Maps)
{
    /// <summary>Всё запрошенное легло полностью?</summary>
    public bool AllOk => Locale && Flag && LangName && Cheats is not false && Maps is not false;
}

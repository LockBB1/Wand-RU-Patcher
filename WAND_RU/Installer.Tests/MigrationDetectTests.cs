using System.IO;
using WandRuInstaller.Core;
using Xunit;

namespace WandRuInstaller.Tests;

public class MigrationDetectTests
{
    // Синтетические версии: младшая / старшая / отсутствующая. НЕ привязаны к реальным номерам Wand
    // (dirs создаются в temp, а не из установленного Wand) - при обновлении Wand править не нужно.
    const string Old = "1.0.0", New = "2.0.0", Absent = "9.9.9";

    static string MakeRoot(params (string ver, bool patched)[] apps)
    {
        var root = Path.Combine(Path.GetTempPath(), "wru-mig-" + Path.GetRandomFileName());
        foreach (var (ver, patched) in apps)
        {
            var res = Path.Combine(root, "app-" + ver, "resources");
            Directory.CreateDirectory(res);
            if (patched)
                File.WriteAllText(Path.Combine(res, "wand-ru-patch.json"), """{"Name":"Wand RU"}""");
        }
        return root;
    }

    [Fact]
    public void Detect_flags_patched_old_version_when_newest_is_clean()
    {
        var root = MakeRoot((Old, true), (New, false));
        try
        {
            var install = WandLocator.Detect(new[] { root })!;
            Assert.False(install.IsPatched);
            Assert.EndsWith($"app-{New}", install.SelectedAppDir);
            Assert.EndsWith($"app-{Old}", install.PatchedOtherAppDir);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Detect_no_migration_hint_when_newest_is_patched_or_nothing_patched()
    {
        var patchedNew = MakeRoot((Old, true), (New, true));
        var cleanAll = MakeRoot((Old, false), (New, false));
        try
        {
            Assert.Null(WandLocator.Detect(new[] { patchedNew })!.PatchedOtherAppDir);
            Assert.Null(WandLocator.Detect(new[] { cleanAll })!.PatchedOtherAppDir);
        }
        finally
        {
            Directory.Delete(patchedNew, true);
            Directory.Delete(cleanAll, true);
        }
    }

    [Fact]
    public void Detect_honors_pinned_version_over_latest()
    {
        var root = MakeRoot((Old, true), (New, false));
        try
        {
            var pinned = WandLocator.Detect(new[] { root }, Old)!;
            Assert.EndsWith($"app-{Old}", pinned.SelectedAppDir);   // закреплённая, не последняя
            Assert.True(pinned.IsPatched);                          // манифест закреплённой версии
            Assert.EndsWith($"app-{New}", WandLocator.Detect(new[] { root }, null)!.SelectedAppDir);    // без пина - последняя
            Assert.EndsWith($"app-{New}", WandLocator.Detect(new[] { root }, Absent)!.SelectedAppDir);  // несуществующая -> фолбэк
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void VerifyTree_throws_on_unsupported_layout_and_passes_on_patched()
    {
        var tree = Path.Combine(Path.GetTempPath(), "wru-vt-" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(tree, "static", "strings"));
        try
        {
            // Якоря не нашлись: нет ru-RU.json и нет регистрации в JS.
            File.WriteAllText(Path.Combine(tree, "bundle.js"), "var locales=[\"en-US\",\"de-DE\"];");
            Assert.Throws<NotSupportedException>(() => RuPatcher.VerifyTree(tree));

            // Патч прошёл: локаль на месте + ru-RU в JS. 3-я локаль после ru-RU - иначе HasCorruption
            // (guard 0.15.3) примет `["en-US","ru-RU"]` за порчу (список локалей без следующей за ru-RU).
            File.WriteAllText(Path.Combine(tree, "static", "strings", "ru-RU.json"), "{}");
            File.WriteAllText(Path.Combine(tree, "bundle.js"), "var locales=[\"en-US\",\"ru-RU\",\"de-DE\"];");
            RuPatcher.VerifyTree(tree); // не бросает

            // Читы/карты просили, но их якоря не легли: это best-effort - НЕ фейл, а честный отчёт.
            // Флаг и native-имя тоже не легли (в бандле только список локалей).
            var report = RuPatcher.VerifyTree(tree, wantCheats: true, wantMaps: true);
            Assert.True(report.Locale);
            Assert.False(report.Cheats);
            Assert.False(report.Maps);
            Assert.False(report.Flag);
            Assert.False(report.LangName);
            Assert.False(report.AllOk);   // «Готово» больше не безусловно

            // Легли и флаг, и native-имя (якоря JsLocalePatch) -> отчёт это видит.
            File.WriteAllText(Path.Combine(tree, "flags.js"),
                "var f=[[\"en-US\",a],[\"ru-RU\",\"" + JsLocalePatch.RussianFlagDataUri + "\"]]);");
            File.WriteAllText(Path.Combine(tree, "meta.js"),
                "var m={en:{name:\"English\",native:\"English\",locale:\"en-US\"},ru:{name:\"Russian\",native:\"Русский\",locale:\"ru-RU\"}};");
            var full = RuPatcher.VerifyTree(tree);
            Assert.True(full.Flag);
            Assert.True(full.LangName);
        }
        finally { Directory.Delete(tree, true); }
    }
}

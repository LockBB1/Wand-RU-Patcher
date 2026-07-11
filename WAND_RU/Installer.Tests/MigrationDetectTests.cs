using System.IO;
using WandRuInstaller.Core;
using Xunit;

namespace WandRuInstaller.Tests;

public class MigrationDetectTests
{
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
        var root = MakeRoot(("12.37.0", true), ("12.38.0", false));
        try
        {
            var install = WandLocator.Detect(new[] { root })!;
            Assert.False(install.IsPatched);
            Assert.EndsWith("app-12.38.0", install.SelectedAppDir);
            Assert.EndsWith("app-12.37.0", install.PatchedOtherAppDir);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Detect_no_migration_hint_when_newest_is_patched_or_nothing_patched()
    {
        var patchedNew = MakeRoot(("12.37.0", true), ("12.38.0", true));
        var cleanAll = MakeRoot(("12.37.0", false), ("12.38.0", false));
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
    public void VerifyTree_throws_on_unsupported_layout_and_passes_on_patched()
    {
        var tree = Path.Combine(Path.GetTempPath(), "wru-vt-" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(tree, "static", "strings"));
        try
        {
            // Якоря не нашлись: нет ru-RU.json и нет регистрации в JS.
            File.WriteAllText(Path.Combine(tree, "bundle.js"), "var locales=[\"en-US\",\"de-DE\"];");
            Assert.Throws<NotSupportedException>(() => RuPatcher.VerifyTree(tree));

            // Патч прошёл: локаль на месте + ru-RU в JS.
            File.WriteAllText(Path.Combine(tree, "static", "strings", "ru-RU.json"), "{}");
            File.WriteAllText(Path.Combine(tree, "bundle.js"), "var locales=[\"en-US\",\"ru-RU\"];");
            RuPatcher.VerifyTree(tree); // не бросает
        }
        finally { Directory.Delete(tree, true); }
    }
}

using System.IO;
using System.Linq;
using AsarSharp.AsarFileSystem;
using WandRuInstaller.Core;
using WandRuInstaller.Models;
using Xunit;

namespace WandRuInstaller.Tests;

/// <summary>Патч+откат на pristine temp-копии repo Wand. Wand НЕ запускается.</summary>
public class PatchRoundTripTests
{
    [Fact]
    public void Apply_registers_ru_locale_in_asar_tree()
    {
        var appDir = TestPaths.PristineAppCopy();
        var man = new RuPatcher(appDir, RuOverrides.LoadEmbedded()).Apply();

        Assert.Equal("12.37.0", man.AppVersion);
        Assert.True(File.Exists(Path.Combine(appDir, "resources", "wand-ru-patch.json")));

        // ru-RU.json должен быть в ДЕРЕВЕ нового app.asar (так его видит Electron), не только в unpacked.
        var newAsar = Path.Combine(appDir, "resources", "app.asar");
        var listed = Disk.ReadFilesystemSync(newAsar).ListFiles();
        Assert.Contains(listed, p => p.EndsWith("ru-RU.json"));

        // и содержимое ru-RU корректно (в unpacked-копии, куда пишет PatchTree)
        var ruUnpacked = Path.Combine(appDir, "resources", "app.asar.unpacked", "static", "strings", "ru-RU.json");
        Assert.True(File.Exists(ruUnpacked));
        Assert.Contains("Главная", File.ReadAllText(ruUnpacked));
    }

    [Fact]
    public void Apply_injects_cheat_hook_when_enabled()
    {
        var appDir = TestPaths.PristineAppCopy();
        new RuPatcher(appDir, RuOverrides.LoadEmbedded(), translateCheats: true).Apply();

        var unpacked = Path.Combine(appDir, "resources", "app.asar.unpacked");
        Assert.True(File.Exists(Path.Combine(unpacked, "cheat-hook.js")));
        Assert.Contains("cheat-hook.js", File.ReadAllText(Path.Combine(unpacked, "index.html")));

        // cheat-hook.js должен попасть в дерево нового app.asar (так его грузит Electron).
        var listed = Disk.ReadFilesystemSync(Path.Combine(appDir, "resources", "app.asar")).ListFiles();
        Assert.Contains(listed, p => p.EndsWith("cheat-hook.js"));
    }

    [Fact]
    public void Apply_skips_cheat_hook_when_disabled()
    {
        var appDir = TestPaths.PristineAppCopy();
        new RuPatcher(appDir, RuOverrides.LoadEmbedded(), translateCheats: false).Apply();

        var unpacked = Path.Combine(appDir, "resources", "app.asar.unpacked");
        Assert.False(File.Exists(Path.Combine(unpacked, "cheat-hook.js")));
        Assert.DoesNotContain("cheat-hook.js", File.ReadAllText(Path.Combine(unpacked, "index.html")));
    }

    [Fact]
    public void CheatHook_inject_is_idempotent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wru-hook-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "index.html"), "<html><head></head><body></body></html>");
            CheatHook.Inject(dir, "/*hook*/");
            CheatHook.Inject(dir, "/*hook*/");
            var html = File.ReadAllText(Path.Combine(dir, "index.html"));
            var count = html.Split("cheat-hook.js").Length - 1;
            Assert.Equal(1, count); // подключён ровно один раз
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Restore_reverts_asar_and_removes_manifest()
    {
        var appDir = TestPaths.PristineAppCopy();
        var asar = Path.Combine(appDir, "resources", "app.asar");
        var before = File.ReadAllBytes(asar);

        new RuPatcher(appDir, RuOverrides.LoadEmbedded()).Apply();
        Assert.NotEqual(before, File.ReadAllBytes(asar));

        RuUnpatcher.Restore(appDir);
        Assert.Equal(before, File.ReadAllBytes(asar));
        Assert.False(File.Exists(Path.Combine(appDir, "resources", "wand-ru-patch.json")));
    }
}

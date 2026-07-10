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
    // Копия app-* в temp с восстановлением app.asar и app.asar.unpacked из их .backup (чистый baseline).
    static string PristineAppCopy()
    {
        var src = TestPaths.LatestAppDir();
        var dst = Path.Combine(Path.GetTempPath(), "wand-ru-test-" + Guid.NewGuid().ToString("N"), "app-12.37.0");
        CopyDir(src, dst, skipTop: "wand-ru-backup");
        var res = Path.Combine(dst, "resources");

        var asarBak = Path.Combine(res, "app.asar.backup");
        if (File.Exists(asarBak)) File.Copy(asarBak, Path.Combine(res, "app.asar"), true);

        var unpackedBak = Path.Combine(res, "app.asar.unpacked.backup");
        var unpacked = Path.Combine(res, "app.asar.unpacked");
        if (Directory.Exists(unpackedBak))
        {
            if (Directory.Exists(unpacked)) Directory.Delete(unpacked, true);
            CopyDir(unpackedBak, unpacked);
        }
        foreach (var f in new[] { "wand-ru-patch.json" })
            if (File.Exists(Path.Combine(res, f))) File.Delete(Path.Combine(res, f));
        return dst;
    }

    static void CopyDir(string s, string d, string? skipTop = null)
    {
        Directory.CreateDirectory(d);
        foreach (var f in Directory.GetFiles(s)) File.Copy(f, Path.Combine(d, Path.GetFileName(f)), true);
        foreach (var sub in Directory.GetDirectories(s))
        {
            var name = Path.GetFileName(sub);
            if (skipTop is not null && name == skipTop) continue;
            CopyDir(sub, Path.Combine(d, name));
        }
    }

    [Fact]
    public void Apply_registers_ru_locale_in_asar_tree()
    {
        var appDir = PristineAppCopy();
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
    public void Restore_reverts_asar_and_removes_manifest()
    {
        var appDir = PristineAppCopy();
        var asar = Path.Combine(appDir, "resources", "app.asar");
        var before = File.ReadAllBytes(asar);

        new RuPatcher(appDir, RuOverrides.LoadEmbedded()).Apply();
        Assert.NotEqual(before, File.ReadAllBytes(asar));

        RuUnpatcher.Restore(appDir);
        Assert.Equal(before, File.ReadAllBytes(asar));
        Assert.False(File.Exists(Path.Combine(appDir, "resources", "wand-ru-patch.json")));
    }
}

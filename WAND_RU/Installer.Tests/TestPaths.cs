using System.IO;

namespace WandRuInstaller.Tests;

/// <summary>Находит repo-копию Wand (для верификации без запуска Wand).</summary>
static class TestPaths
{
    public static string WandRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Wand");
            if (Directory.Exists(Path.Combine(candidate, "app-12.37.0"))) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo Wand/app-12.37.0 не найден вверх по дереву от " + AppContext.BaseDirectory);
    }

    public static string LatestAppDir() => Path.Combine(WandRoot(), "app-12.37.0");

    /// <summary>Копия app-* в temp с восстановлением app.asar и app.asar.unpacked из .backup. Возвращает путь app-dir.</summary>
    public static string PristineAppCopy()
    {
        var src = LatestAppDir();
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
        var man = Path.Combine(res, "wand-ru-patch.json");
        if (File.Exists(man)) File.Delete(man);
        return dst;
    }

    public static void CopyDir(string s, string d, string? skipTop = null)
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
}

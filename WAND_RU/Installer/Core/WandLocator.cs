using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using WandRuInstaller.Models;

namespace WandRuInstaller.Core;

public static class WandLocator
{
    static readonly Regex AppRe = new(@"^app-(\d+)\.(\d+)\.(\d+)$", RegexOptions.Compiled);

    public static string[] FindAppDirs(string root)
    {
        if (!Directory.Exists(root)) return Array.Empty<string>();
        return new DirectoryInfo(root).GetDirectories("app-*")
            .Select(d => (dir: d.FullName, m: AppRe.Match(d.Name)))
            .Where(x => x.m.Success)
            .OrderByDescending(x => new Version(
                int.Parse(x.m.Groups[1].Value),
                int.Parse(x.m.Groups[2].Value),
                int.Parse(x.m.Groups[3].Value)))
            .Select(x => x.dir)
            .ToArray();
    }

    public static string LatestAppDir(string root)
    {
        var dirs = FindAppDirs(root);
        if (dirs.Length == 0) throw new InvalidOperationException($"Нет app-* в {root}");
        return dirs[0];
    }

    public static IEnumerable<string> DefaultRoots()
    {
        var lad = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(lad, "Wand");
        yield return Path.Combine(lad, "Programs", "Wand");
    }

    /// <summary>Версия app-дира ("12.38.0") из имени "app-12.38.0".</summary>
    public static string VersionOf(string appDir) => new DirectoryInfo(appDir).Name.Replace("app-", "");

    public static PatchManifest? Manifest(string appDir) => ReadManifest(appDir);

    public static WandInstall? Detect(IEnumerable<string> candidateRoots, string? pinnedVersion = null)
    {
        foreach (var root in candidateRoots)
        {
            var dirs = FindAppDirs(root);
            if (dirs.Length == 0) continue;
            // Закреплённая версия имеет приоритет над последней (играть на старой, пока новую не поддержали).
            var sel = (pinnedVersion is not null
                ? dirs.FirstOrDefault(d => VersionOf(d) == pinnedVersion) : null) ?? dirs[0];
            var man = ReadManifest(sel);
            // Wand обновился: новая версия без патча, а в старой он есть -> предложим перенос.
            var patchedOther = man is null
                ? dirs.Skip(1).FirstOrDefault(d => ReadManifest(d) is not null)
                : null;
            return new WandInstall
            {
                RootDir = root,
                AppDirs = dirs,
                SelectedAppDir = sel,
                IsPatched = man is not null,
                Manifest = man,
                PatchedOtherAppDir = patchedOther,
            };
        }
        return null;
    }

    static PatchManifest? ReadManifest(string appDir)
    {
        var path = Path.Combine(appDir, "resources", "wand-ru-patch.json");
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<PatchManifest>(File.ReadAllText(path))
                : null;
        }
        catch { return null; } // битый manifest = не пропатчено
    }
}

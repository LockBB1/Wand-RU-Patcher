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
}

using System.IO;
using System.Linq;
using System.Text.Json;

namespace WandRuInstaller.Core;

/// <summary>
/// Кэш онлайн-переводов читов (пишет renderer-хук через fs). Установщик его меряет и чистит.
/// Файл — %AppData%\WandRuInstaller\cheat-cache.json (тот же каталог, что settings.json).
/// </summary>
public static class CheatCache
{
    public static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WandRuInstaller", "cheat-cache.json");

    public static long SizeBytes() => SizeBytes(Path);
    public static int EntryCount() => EntryCount(Path);
    public static void Clear() => Clear(Path);

    public static string HumanSize()
    {
        var b = SizeBytes();
        return b >= 1048576 ? $"{b / 1048576.0:0.0} МБ"
             : b >= 1024 ? $"{b / 1024.0:0.0} КБ"
             : $"{b} Б";
    }

    internal static long SizeBytes(string path)
    {
        try { var f = new FileInfo(path); return f.Exists ? f.Length : 0; }
        catch { return 0; }
    }

    internal static int EntryCount(string path)
    {
        try
        {
            if (!File.Exists(path)) return 0;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.ValueKind == JsonValueKind.Object
                ? doc.RootElement.EnumerateObject().Count()
                : 0;
        }
        catch { return 0; }
    }

    internal static void Clear(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* не удалось — не критично */ }
    }
}

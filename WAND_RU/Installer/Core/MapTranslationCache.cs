using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WandRuInstaller.Core;

internal static class MapTranslationCache
{
    const int MaxEntries = 5000;
    const long MaxBytes = 100 * 1024 * 1024;
    const long MaxMapBytes = 16 * 1024 * 1024;
    internal static string Path => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WandRuInstaller", "map-cache.json");

    static T Locked<T>(string path, T fallback, Func<T> action)
    {
        try
        {
            var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(System.IO.Path.GetFullPath(path).ToUpperInvariant())));
            using var mutex = new Mutex(false, "WandRuMapCache-" + name);
            var acquired = false;
            try
            {
                try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(1)); }
                catch (AbandonedMutexException) { acquired = true; }
                return acquired ? action() : fallback;
            }
            finally { if (acquired) mutex.ReleaseMutex(); }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException) { return fallback; }
    }

    static string MapPath(string path, string slug) => System.IO.Path.Combine(path + ".d", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(slug))) + ".json");

    static IEnumerable<string> MapFiles(string path)
    {
        var directory = path + ".d";
        if (!Directory.Exists(directory)) return Array.Empty<string>();
        return Directory.EnumerateFiles(directory, "*.json").Where(file =>
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(file);
            return name.Length == 64 && name.All(Uri.IsHexDigit);
        }).ToArray();
    }

    static Dictionary<string, Dictionary<string, string>> ReadLegacy(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length > MaxMapBytes) return new();
        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(path)) ?? new();
    }

    static Dictionary<string, string> ReadMap(string path, string slug)
    {
        var mapPath = MapPath(path, slug);
        Dictionary<string, string>? entries;
        if (File.Exists(mapPath))
        {
            if (new FileInfo(mapPath).Length > MaxMapBytes) return new();
            entries = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(mapPath));
        }
        else
        {
            var legacy = ReadLegacy(path);
            legacy.TryGetValue(slug, out entries);
        }
        if (entries is null) return new();
        return entries.Where(e => e.Key is { Length: > 1 and <= 1500 } && e.Value is { Length: > 0 and <= 4000 })
            .Take(MaxEntries).ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
    }

    internal static Dictionary<string, string> Load(string path, string slug) => Locked(path, new Dictionary<string, string>(), () => ReadMap(path, slug));

    internal static void Save(string path, string slug, IReadOnlyDictionary<string, string> translations)
    {
        if (translations.Count == 0) return;
        Locked(path, false, () =>
        {
            Dictionary<string, string> entries;
            try { entries = ReadMap(path, slug); } catch (JsonException) { entries = new(); }
            foreach (var (key, value) in translations)
                if (key is { Length: > 1 and <= 1500 } && value is { Length: > 0 and <= 4000 } && (entries.ContainsKey(key) || entries.Count < MaxEntries)) entries[key] = value;
            var json = JsonSerializer.SerializeToUtf8Bytes(entries, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            var mapPath = MapPath(path, slug);
            var oldSize = File.Exists(mapPath) ? new FileInfo(mapPath).Length : 0;
            if (json.Length > MaxMapBytes || SizeBytes(path) - oldSize + json.Length > MaxBytes) return false;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(mapPath)!);
            var temporary = mapPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temporary, json); File.Move(temporary, mapPath, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return true;
        });
    }

    internal static void Clear(string path) => Locked(path, false, () =>
    {
        if (File.Exists(path)) File.Delete(path);
        foreach (var file in MapFiles(path)) File.Delete(file);
        return true;
    });
    static long SizeBytes(string path) => CheatCache.SizeBytes(path) + MapFiles(path).Sum(file => new FileInfo(file).Length);
    internal static long SizeBytes() => Locked(Path, 0L, () => SizeBytes(Path));
    internal static int EntryCount() => EntryCount(Path);
    internal static int EntryCount(string path) => Locked(path, 0, () =>
    {
        Dictionary<string, Dictionary<string, string>> legacy;
        try { legacy = ReadLegacy(path); } catch (JsonException) { legacy = new(); }
        var count = legacy.Where(pair => pair.Value is not null && !File.Exists(MapPath(path, pair.Key))).Sum(pair => pair.Value.Count);
        foreach (var file in MapFiles(path))
        {
            if (new FileInfo(file).Length > MaxMapBytes) continue;
            try { count += JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))?.Count ?? 0; }
            catch (JsonException) { }
        }
        return count;
    });
}

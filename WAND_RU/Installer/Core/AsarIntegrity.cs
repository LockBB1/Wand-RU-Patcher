using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WandRuInstaller.Core;

/// <summary>
/// Синхронизация встроенной в Wand.exe проверки целостности app.asar (Electron fuse
/// EnableEmbeddedAsarIntegrityValidation). В ресурсах exe лежит blob
/// [{"file":"resources\\app.asar","alg":"SHA256","value":"&lt;sha256 заголовка asar&gt;"}].
/// После пересборки app.asar заголовок меняется; без обновления value Electron молча не стартует -
/// падение в main-процессе ДО окна и логов. Пишем актуальный хэш заголовка in-place (64 hex, та же длина).
/// Старая версия Wand без blob -> no-op (патч работает как раньше).
/// </summary>
public static class AsarIntegrity
{
    // Якорь пути в blob: обратные слэши в JSON экранированы, в байтах это два символа '\'.
    static readonly byte[] FileAnchor = Encoding.ASCII.GetBytes(@"resources\\app.asar");
    static readonly byte[] ValueKey = Encoding.ASCII.GetBytes("\"value\":\"");
    const int HashHexLen = 64; // sha256 в hex

    /// <summary>SHA256 (hex, lower) строки-заголовка asar - ровно то, что Electron кладёт в value.</summary>
    public static string ComputeHeaderHash(string asarPath)
    {
        using var fs = File.OpenRead(asarPath);
        Span<byte> size = stackalloc byte[16];
        fs.ReadExactly(size);
        int headerLen = BitConverter.ToInt32(size[12..]);
        var header = new byte[headerLen];
        fs.ReadExactly(header);
        return Convert.ToHexString(SHA256.HashData(header)).ToLowerInvariant();
    }

    /// <summary>
    /// Прописывает хэш текущего app.asar во все exe в appDir, где есть integrity-blob.
    /// Возвращает число обновлённых exe (0 = blob нигде не найден, старая версия Wand).
    /// </summary>
    public static int SyncAppDir(string appDir, string asarPath, Action<string>? log = null)
    {
        log ??= _ => { };
        var hashBytes = Encoding.ASCII.GetBytes(ComputeHeaderHash(asarPath));
        int patched = 0;
        foreach (var exe in Directory.EnumerateFiles(appDir, "*.exe", SearchOption.TopDirectoryOnly))
            if (WriteHash(exe, hashBytes)) { patched++; log($"Целостность: обновлён {Path.GetFileName(exe)}"); }
        return patched;
    }

    /// <summary>
    /// Read-back: у каждого exe с blob хэш совпадает с текущим app.asar. Иначе Wand молча не стартует -
    /// кидаем понятную ошибку сразу после патча, а не оставляем юзеру чёрный экран.
    /// </summary>
    public static void VerifyExesMatch(string appDir, string asarPath)
    {
        var expected = ComputeHeaderHash(asarPath);
        foreach (var exe in Directory.EnumerateFiles(appDir, "*.exe", SearchOption.TopDirectoryOnly))
        {
            var got = ReadHash(exe);
            if (got is not null && got != expected)
                throw new InvalidOperationException(
                    $"Не удалось обновить проверку целостности в {Path.GetFileName(exe)} - Wand не запустится. " +
                    "Откатите русификатор (Восстановить) и создайте issue с экспортом лога.");
        }
    }

    /// <summary>Текущее значение integrity-хэша из exe (null, если blob нет). Для проверок/тестов.</summary>
    public static string? ReadHash(string exePath)
    {
        using var fs = File.OpenRead(exePath);
        long hashPos = LocateHash(fs);
        if (hashPos < 0) return null;
        var buf = new byte[HashHexLen];
        fs.Seek(hashPos, SeekOrigin.Begin);
        fs.ReadExactly(buf);
        return Encoding.ASCII.GetString(buf);
    }

    static bool WriteHash(string exePath, byte[] hashBytes)
    {
        using var fs = new FileStream(exePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        long hashPos = LocateHash(fs);
        if (hashPos < 0) return false;
        fs.Seek(hashPos, SeekOrigin.Begin);
        fs.Write(hashBytes, 0, hashBytes.Length);
        fs.Flush();
        return true;
    }

    // Смещение первого байта 64-hex значения (anchor -> "value":" -> хэш). -1, если blob нет.
    static long LocateHash(FileStream fs)
    {
        long anchor = FindPattern(fs, FileAnchor, 0, 0);
        if (anchor < 0) return -1;
        long valueKey = FindPattern(fs, ValueKey, anchor, anchor + 200);
        return valueKey < 0 ? -1 : valueKey + ValueKey.Length;
    }

    // Потоковый поиск паттерна в [start, end) с перекрытием между чанками. end<=0 = до конца файла.
    static long FindPattern(FileStream fs, byte[] pat, long start, long end)
    {
        long limit = end > 0 ? Math.Min(end, fs.Length) : fs.Length;
        fs.Seek(start, SeekOrigin.Begin);
        const int chunk = 1 << 20;
        var window = new byte[chunk + pat.Length - 1];
        long basePos = start;
        int keep = 0; // хвост предыдущего чанка (перекрытие)
        while (basePos + keep < limit)
        {
            int toRead = (int)Math.Min(chunk, limit - (basePos + keep));
            int n = fs.Read(window, keep, toRead);
            if (n <= 0) break;
            int avail = keep + n;
            int idx = IndexOf(window, avail, pat);
            if (idx >= 0) return basePos + idx;
            keep = Math.Min(pat.Length - 1, avail);
            Array.Copy(window, avail - keep, window, 0, keep);
            basePos += avail - keep;
        }
        return -1;
    }

    static int IndexOf(byte[] hay, int len, byte[] needle)
    {
        for (int i = 0; i + needle.Length <= len; i++)
        {
            int j = 0;
            while (j < needle.Length && hay[i + j] == needle[j]) j++;
            if (j == needle.Length) return i;
        }
        return -1;
    }
}

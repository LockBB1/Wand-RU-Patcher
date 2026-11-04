using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WandRuInstaller.Core.Runtime;

internal static class RuntimeEnhancer
{
    static readonly byte[] FuseMarker = Encoding.ASCII.GetBytes("dL7pKGdnNz796PbbjQWNKmHXBZaB9tsX");
    internal static readonly string CleanState = new('0', 64);
    // Returns null for a normal Wand installation; a detected Enhancer must validate completely.
    internal static bool? Validate(string appDir)
    {
        var root = Directory.GetParent(appDir)!.FullName;
        var resources = Path.Combine(appDir, "resources");
        var backup = Path.Combine(resources, "app.asar.backup");
        var unpackedBackup = Path.Combine(resources, "app.asar.unpacked.backup");
        var executable = Path.Combine(appDir, "Wand.exe");
        var rootExecutable = Path.Combine(root, "Wand.exe");
        var stub = rootExecutable + ".stub";
        var fuseBackup = executable + ".fusebak";
        if (File.Exists(stub)) throw new InvalidOperationException("Режим Supervised не поддерживается. Выберите Static в Wand-Enhancer.");
        var incomplete = File.Exists(Path.Combine(resources, ".incomplete-patch"));
        if (!File.Exists(backup) && !Directory.Exists(unpackedBackup) && !File.Exists(stub) && !File.Exists(fuseBackup) && !incomplete) return null;
        if (!File.Exists(backup) || !Directory.Exists(unpackedBackup) || incomplete)
            throw new InvalidOperationException("Состояние Wand-Enhancer не подтверждено: отсутствует резервная копия или установка не завершена.");
        var originalHash = ReadArchiveHash(backup);
        var currentHash = ReadArchiveHash(Path.Combine(resources, "app.asar"));
        string? hash;
        if (!Authenticode.IsTrusted(rootExecutable) || !ValidateStaticExecutable(executable, fuseBackup, originalHash, out hash))
            throw new InvalidOperationException("Не удалось проверить оригинальный файл Wand для Wand-Enhancer.");

        if (hash is null || (hash != originalHash && hash != currentHash))
            throw new InvalidOperationException("Резервная копия Wand-Enhancer не соответствует оригинальному Wand.");
        return false;
    }

    static string ReadArchiveHash(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> prefix = stackalloc byte[16];
        stream.ReadExactly(prefix);
        var length = BitConverter.ToInt32(prefix[12..]);
        if (length < 2 || length > 16 * 1024 * 1024 || length > stream.Length - prefix.Length)
            throw new InvalidDataException("Повреждён заголовок app.asar.");
        var header = new byte[length];
        stream.ReadExactly(header);
        return Convert.ToHexString(SHA256.HashData(header)).ToLowerInvariant();
    }

    static bool ValidateStaticExecutable(string executable, string backup, string originalHash, out string? embeddedHash)
    {
        embeddedHash = null;
        if (!File.Exists(backup) || new FileInfo(backup).Length != 1) return false;
        var previous = File.ReadAllBytes(backup)[0];
        if (previous is not ((byte)'0' or (byte)'1')) return false;
        long offset;
        using (var stream = File.OpenRead(executable))
        {
            var marker = AsarIntegrity.FindPattern(stream, FuseMarker, 0, 0);
            if (marker < 0 || AsarIntegrity.FindPattern(stream, FuseMarker, marker + 1, 0) >= 0) return false;
            offset = marker + FuseMarker.Length;
            stream.Position = offset;
            if (stream.ReadByte() != 1 || stream.ReadByte() < 5) return false;
            offset += 6;
            stream.Position = offset;
            if (stream.ReadByte() != 'r') return false;
        }
        var temporary = Path.Combine(Path.GetTempPath(), "wrp-original-" + Guid.NewGuid().ToString("N") + ".exe");
        try
        {
            File.Copy(executable, temporary);
            embeddedHash = AsarIntegrity.ReadHash(temporary);
            using (var stream = new FileStream(temporary, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                stream.Position = offset;
                if (stream.ReadByte() != 'r') return false;
                stream.Position = offset;
                stream.WriteByte(previous);
            }
            if (!AsarIntegrity.WriteHash(temporary, Encoding.ASCII.GetBytes(originalHash))) return false;
            return Authenticode.IsTrusted(temporary);
        }
        finally { File.Delete(temporary); }
    }

    internal static string Snapshot(string appDir)
    {
        var root = Directory.GetParent(appDir)!.FullName;
        var resources = Path.Combine(appDir, "resources");
        var executable = Path.Combine(appDir, "Wand.exe");
        var launcher = Path.Combine(root, "Wand.exe");
        var auxiliaryDir = Path.Combine(resources, "app.asar.unpacked", "static", "unpacked", "auxiliary");
        var auxiliary = Directory.Exists(auxiliaryDir)
            ? Directory.EnumerateFiles(auxiliaryDir, "*AuxiliaryService.exe").SingleOrDefault() : null;
        if (auxiliary is null) throw new InvalidOperationException("Не найдена служба Wand-Enhancer для проверки.");
        var files = new[] { executable, launcher, Path.Combine(resources, "app.asar"),
            Path.Combine(resources, "app.asar.backup"), auxiliary, executable + ".fusebak" };
        using var combined = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in files)
        {
            RuntimeShortcuts.EnsureNoLinks(path);
            using var stream = File.OpenRead(path);
            combined.AppendData(Encoding.UTF8.GetBytes(Path.GetFileName(path)));
            combined.AppendData(SHA256.HashData(stream));
        }
        return Convert.ToHexString(combined.GetHashAndReset());
    }

    internal static void Pin(string stateDirectory, string snapshot, bool allowChange)
    {
        RuntimeShortcuts.EnsureNoLinks(stateDirectory);
        Directory.CreateDirectory(stateDirectory);
        var path = Path.Combine(stateDirectory, "enhancer.sha256");
        RuntimeShortcuts.EnsureNoLinks(path);
        if (File.Exists(path))
        {
            if (new FileInfo(path).Length != 64) throw new InvalidDataException("Повреждены данные проверки Wand-Enhancer.");
            var existing = File.ReadAllText(path, Encoding.ASCII);
            if (!allowChange && existing != CleanState && !string.Equals(existing, snapshot, StringComparison.Ordinal))
                throw new InvalidOperationException("Файлы Wand-Enhancer изменились. Повторите установку перевода WRP.");
            if (existing == snapshot) return;
        }
        else if (!allowChange) throw new InvalidOperationException("Данные проверки Wand-Enhancer отсутствуют. Повторите установку перевода WRP.");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            RuntimeShortcuts.WriteDurable(temporary, Encoding.ASCII.GetBytes(snapshot));
            RuntimeShortcuts.Replace(temporary, path);
        }
        finally { File.Delete(temporary); }
    }
}

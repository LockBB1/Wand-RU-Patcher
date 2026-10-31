using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WandRuInstaller.Core.Runtime;

internal static class RuntimeExeRepair
{
    internal sealed record Intent(string AppDir, string Id, string Before, string After, string OldHash, string NewHash);
    internal static string JournalPath(string appDir, string storage)
    {
        var name = Path.GetFileName(Path.GetFullPath(appDir));
        if (!Regex.IsMatch(name, @"^app-\d+\.\d+\.\d+$")) throw new InvalidDataException("Не распознана версия Wand.");
        return Path.Combine(storage, "exe-repair-" + name + ".json");
    }

    internal static void TryRepair(string appDir, string storage)
    {
        appDir = Path.GetFullPath(appDir);
        storage = Path.GetFullPath(storage);
        var root = Directory.GetParent(appDir)?.FullName ?? throw new InvalidDataException("Не найдена папка Wand.");
        if (storage.Equals(root, StringComparison.OrdinalIgnoreCase) || storage.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Данные восстановления должны находиться вне Wand.");
        RepairOne(appDir, storage);
    }

    static void RepairOne(string appDir, string storage)
    {
        var root = Directory.GetParent(appDir)!.FullName;
        var exe = Path.Combine(appDir, "Wand.exe");
        var resources = Path.Combine(appDir, "resources");
        var archive = Path.Combine(resources, "app.asar");
        var journal = JournalPath(appDir, storage);
        foreach (var path in new[] { exe, archive, storage, journal }) RuntimeShortcuts.EnsureNoLinks(path);
        if (File.Exists(journal) && File.Exists(exe) && FinalizeCompleted(appDir, storage, journal)) return;
        if (!File.Exists(journal) && Authenticode.IsTrusted(exe)) return;
        if (RuntimeEnhancer.Validate(appDir) is not null)
        {
            if (File.Exists(journal)) throw new InvalidOperationException("Незавершённое восстановление Wand. Состояние Enhancer изменилось.");
            return;
        }
        if (appDir.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Автоматическое восстановление недоступно для расширенного пути Wand. Исходные файлы не изменены.");
        if (WandProcess.AnyRunning() || AnyUpdaterRunning()) throw new InvalidOperationException("Закройте Wand и дождитесь завершения обновления.");
        if (HasMarkers(appDir) || RuPatcher.IsAsarPatched(archive)) throw new InvalidOperationException("Обнаружен незавершённый патч Wand. Восстановление остановлено.");
        if (!Authenticode.IsTrusted(Path.Combine(root, "Wand.exe"))) throw new InvalidOperationException("Подпись загрузчика Wand не подтверждена.");

        Directory.CreateDirectory(storage);
        using var directories = HandleFile.LockDirectories(root, appDir, resources, storage);
        using var operation = AcquireOperation(storage);
        var patchPath = Path.Combine(resources, ".wru-lock");
        RuntimeShortcuts.EnsureNoLinks(patchPath);
        using var patchLock = RuPatcher.AcquireLock(resources);
        HandleFile.VerifyStream(patchLock, patchPath);
        using var launcher = HandleFile.Open(Path.Combine(root, "Wand.exe"));
        using var source = HandleFile.OpenMutable(exe);
        using var asar = HandleFile.Open(archive);
        var candidatePath = Path.Combine(storage, "exe-candidate-" + Guid.NewGuid().ToString("N") + ".tmp");
        using var candidate = HandleFile.CreateEmpty(candidatePath);
        try
        {
            directories.Validate();
            if (!launcher.Trusted() || HasMarkers(appDir) || WandProcess.AnyRunning() || AnyUpdaterRunning())
                throw new InvalidOperationException("Состояние Wand изменилось. Восстановление остановлено.");
            if (source.LinkCount() != 1) throw new InvalidOperationException("Обнаружены дополнительные ссылки на EXE Wand. Восстановление остановлено.");
            var newHash = asar.ArchiveHash();
            if (!File.Exists(journal) || Resume(source, candidate, journal, appDir, storage, newHash))
                Start(source, candidate, journal, appDir, storage, newHash);
        }
        finally { candidate.Delete(); }
    }

    static bool FinalizeCompleted(string appDir, string storage, string journal)
    {
        using var directories = HandleFile.LockDirectories(appDir, Path.Combine(appDir, "resources"), storage);
        using var operation = AcquireOperation(storage);
        using var log = HandleFile.Open(journal);
        var intent = JsonSerializer.Deserialize<Intent>(log.ReadBounded(4096)) ?? throw new InvalidDataException("Повреждён журнал восстановления Wand.");
        if (!string.Equals(Path.GetFullPath(intent.AppDir), appDir, StringComparison.OrdinalIgnoreCase) || !Guid.TryParseExact(intent.Id, "N", out _) ||
            !ValidHash(intent.Before) || !ValidHash(intent.After) || !ValidHash(intent.OldHash) || !ValidHash(intent.NewHash))
            throw new InvalidDataException("Данные восстановления Wand повреждены.");
        using var source = HandleFile.Open(Path.Combine(appDir, "Wand.exe"));
        using var backup = HandleFile.Open(Path.Combine(storage, "exe-original-" + intent.Id + ".backup"));
        directories.Validate();
        if (backup.Hash() != intent.Before) throw new InvalidDataException("Резервная копия Wand повреждена.");
        var current = source.Hash();
        if (current == intent.Before && HasMarkers(appDir)) { log.Delete(); return true; }
        if (current != intent.After || !source.Trusted()) return false;
        // The completed EXE is signed; a later Enhancer patch may have changed the archive.
        log.Delete();
        return true;
    }

    static void Start(HandleFile source, HandleFile candidate, string journal, string appDir, string storage, string newHash)
    {
        if (source.Trusted()) return;
        var oldHash = source.EmbeddedHash();
        if (oldHash is null || !oldHash.All(Uri.IsHexDigit)) throw new InvalidDataException("Формат проверки целостности Wand не распознан.");
        var before = source.Hash();
        candidate.CopyFrom(source);
        if (candidate.Hash() != before || !candidate.WriteEmbeddedHash(newHash) || !candidate.Trusted())
            throw new InvalidOperationException("Подпись Wand не восстановлена. Исходный файл не изменён.");
        var intent = new Intent(appDir, Guid.NewGuid().ToString("N"), before, candidate.Hash(), oldHash, newHash);
        var backupPath = Path.Combine(storage, "exe-original-" + intent.Id + ".backup");
        using var backup = HandleFile.CreateBackup(backupPath);
        var recorded = false;
        try
        {
            backup.CopyBackupFrom(source);
            if (backup.Hash() != before) throw new IOException("Не удалось проверить резервную копию Wand.");
            using var log = HandleFile.CreateEmpty(Path.Combine(storage, "exe-intent-" + intent.Id + ".tmp"));
            try
            {
                log.WriteAll(JsonSerializer.SerializeToUtf8Bytes(intent));
                try { log.Rename(journal); }
                finally { recorded = string.Equals(log.Path, journal, StringComparison.OrdinalIgnoreCase); }
                log.Flush();
                Apply(source, log, intent);
            }
            finally { if (!recorded) log.Delete(); }
        }
        finally { if (!recorded) backup.Delete(); }
    }

    static bool Resume(HandleFile source, HandleFile candidate, string journal, string appDir, string storage, string newHash)
    {
        using var log = HandleFile.Open(journal);
        var intent = JsonSerializer.Deserialize<Intent>(log.ReadBounded(4096)) ?? throw new InvalidDataException("Повреждён журнал восстановления Wand.");
        if (!string.Equals(Path.GetFullPath(intent.AppDir), appDir, StringComparison.OrdinalIgnoreCase) || !Guid.TryParseExact(intent.Id, "N", out _) ||
            !ValidHash(intent.Before) || !ValidHash(intent.After) || !ValidHash(intent.OldHash) || !ValidHash(intent.NewHash))
            throw new InvalidDataException("Данные восстановления Wand повреждены.");
        using var backup = HandleFile.Open(Path.Combine(storage, "exe-original-" + intent.Id + ".backup"));
        if (backup.Hash() != intent.Before) throw new InvalidDataException("Резервная копия Wand повреждена.");
        if (source.Hash() == intent.After && source.Trusted())
        {
            log.Delete();
            return false;
        }
        candidate.CopyFrom(source);
        if (!candidate.WriteEmbeddedHash(intent.OldHash, true) || candidate.Hash() != intent.Before)
            throw new InvalidDataException("EXE Wand содержит посторонние изменения. Восстановление остановлено.");
        if (intent.NewHash != newHash)
        {
            if (Changed(intent.AppDir)) throw new InvalidOperationException("Состояние Wand изменилось. Восстановление остановлено.");
            if (source.LinkCount() != 1 || !source.WriteEmbeddedHash(intent.OldHash, true) || source.Hash() != intent.Before || source.LinkCount() != 1)
                throw new IOException("Восстановление Wand не завершено. Резервная копия и журнал сохранены.");
            if (Changed(intent.AppDir)) throw new InvalidOperationException("Состояние Wand изменилось. Журнал и резервная копия сохранены.");
            log.Delete();
            return true;
        }
        if (!candidate.WriteEmbeddedHash(intent.NewHash) || candidate.Hash() != intent.After || !candidate.Trusted())
            throw new InvalidDataException("Подпись подготовленной копии Wand не подтверждена.");
        Apply(source, log, intent);
        return false;
    }

    static void Apply(HandleFile source, HandleFile log, Intent intent)
    {
        if (Changed(intent.AppDir))
        {
            if (source.Hash() == intent.Before) log.Delete();
            throw new InvalidOperationException("Состояние Wand изменилось. Восстановление остановлено.");
        }
        if (source.LinkCount() != 1) throw new InvalidOperationException("Обнаружены дополнительные ссылки на EXE Wand.");
        try
        {
            if (!source.WriteEmbeddedHash(intent.NewHash, true) || source.LinkCount() != 1 || source.Hash() != intent.After || !source.Trusted() || Changed(intent.AppDir))
                throw new IOException("Не удалось подтвердить восстановление Wand.");
        }
        catch
        {
            if (!source.WriteEmbeddedHash(intent.OldHash, true) || source.Hash() != intent.Before)
                throw new IOException("Восстановление Wand не завершено. Резервная копия и журнал сохранены.");
            throw;
        }
        log.Delete();
    }

    static bool ValidHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    internal static FileStream AcquireOperation(string storage)
    {
        var path = Path.Combine(storage, "exe-repair.lock");
        RuntimeShortcuts.EnsureNoLinks(path);
        var operation = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try { HandleFile.VerifyStream(operation, path); return operation; }
        catch { operation.Dispose(); throw; }
    }
    internal static IDisposable AcquireUpdaterPin(string root)
    {
        var path = Path.Combine(root, "Update.exe");
        var launcherPath = Path.Combine(root, File.Exists(Path.Combine(root, "Wand.exe.stub")) ? "Wand.exe.stub" : "Wand.exe");
        RuntimeShortcuts.EnsureNoLinks(path);
        RuntimeShortcuts.EnsureNoLinks(launcherPath);
        var launcher = new FileStream(launcherPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            HandleFile.VerifyStream(launcher, launcherPath);
            byte[] hash;
            using (var read = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                HandleFile.VerifyStream(read, path);
                if (!Authenticode.IsTrusted(launcherPath, launcher.SafeFileHandle) || !Authenticode.IsTrusted(path, read.SafeFileHandle))
                    throw new InvalidDataException("Подпись службы обновления Wand не подтверждена.");
#pragma warning disable SYSLIB0057 // X509CertificateLoader does not extract signers from PE files.
                using var launcherCert = X509Certificate.CreateFromSignedFile(launcherPath);
                using var updaterCert = X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
                if (!SHA256.HashData(launcherCert.GetRawCertData()).SequenceEqual(SHA256.HashData(updaterCert.GetRawCertData())))
                    throw new InvalidDataException("Сертификат службы обновления не совпадает с сертификатом Wand.");
                read.Position = 0;
                hash = SHA256.HashData(read);
            }
            var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            try
            {
                HandleFile.VerifyStream(file, path);
                if (!SHA256.HashData(file).SequenceEqual(hash) || !Authenticode.IsTrusted(path, file.SafeFileHandle))
                    throw new InvalidDataException("Служба обновления Wand изменилась во время проверки.");
                return new UpdaterPin(file, launcher);
            }
            catch { file.Dispose(); throw; }
        }
        catch { launcher.Dispose(); throw; }
    }
    sealed class UpdaterPin(FileStream updater, FileStream launcher) : IDisposable
    {
        public void Dispose() { updater.Dispose(); launcher.Dispose(); }
    }
    internal static bool AnyUpdaterRunning()
    {
        foreach (var name in new[] { "Update", "Squirrel" })
        {
            var processes = Process.GetProcessesByName(name);
            try { if (processes.Length != 0) return true; }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        return false;
    }
    static bool Changed(string appDir) => HasMarkers(appDir) || WandProcess.AnyRunning() || AnyUpdaterRunning();
    static bool HasMarkers(string appDir)
    {
        var resources = Path.Combine(appDir, "resources");
        var root = Directory.GetParent(appDir)!.FullName;
        return File.Exists(Path.Combine(resources, "wand-ru-patch.json")) || File.Exists(Path.Combine(resources, ".incomplete-patch")) ||
            File.Exists(Path.Combine(resources, "app.asar.backup")) || Directory.Exists(Path.Combine(resources, "app.asar.unpacked.backup")) ||
            File.Exists(Path.Combine(appDir, "Wand.exe.fusebak")) || File.Exists(Path.Combine(root, "Wand.exe.stub"));
    }
}

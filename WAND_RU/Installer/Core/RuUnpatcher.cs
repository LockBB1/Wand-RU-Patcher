using System.IO;
using System.Text.Json;
using AsarSharp.AsarFileSystem;
using WandRuInstaller.Models;

namespace WandRuInstaller.Core;

/// <summary>Откат русификации: восстановление app.asar (+unpacked) из бэкапа по manifest.</summary>
public static class RuUnpatcher
{
    public static void Restore(string appDir, Action<string>? log = null)
    {
        log ??= _ => { };
        var resources = Path.Combine(appDir, "resources");
        using var _lock = RuPatcher.AcquireLock(resources);   // не откатывать, пока другой процесс патчит
        var manifestPath = Path.Combine(resources, "wand-ru-patch.json");
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException("Патч не установлен (нет manifest).");
        var man = JsonSerializer.Deserialize<PatchManifest>(File.ReadAllText(manifestPath))
                  ?? throw new InvalidOperationException("Битый manifest.");
        // Патч ставился поверх уже русифицированного Wand с утерянным бэкапом (юзер согласился) -
        // оригинала нет. Честная ошибка вместо восстановления мусора.
        if (string.IsNullOrEmpty(man.BackupRoot))
            throw new InvalidOperationException(
                "Откат невозможен: бэкап оригинального app.asar утерян (патч ставился без него). " +
                "Чистый Wand вернёт только переустановка Wand.");
        var backupAsar = Path.Combine(man.BackupRoot, "app.asar");
        if (!File.Exists(backupAsar))
            throw new InvalidOperationException($"Нет бэкапа: {backupAsar}");

        // Бэкап мог быть обрезан/побит (антивирус/сбой копирования) - проверяем заголовок ДО того, как
        // затрём рабочий app.asar. Иначе копия битого бэкапа поверх живого asar = кирпич необратимо.
        // Заголовок asar = JSON-объект дерева файлов; пустой (нулевой headerLen) или не-JSON = бэкап негоден.
        // Потолок: порчу ТЕЛА (заголовок цел, файлы биты) дёшево не поймать - нужна полная распаковка.
        string header;
        try { header = AsarIntegrity.ReadHeaderJson(backupAsar); }
        catch (Exception e) { throw CorruptBackup(backupAsar, e.Message, e); }
        if (string.IsNullOrWhiteSpace(header) || header[0] != '{')
            throw CorruptBackup(backupAsar, "пустой или нечитаемый заголовок", null);

        log("Восстановление app.asar…");
        var asar = Path.Combine(resources, "app.asar");
        File.Copy(backupAsar, asar, true);
        Disk.UncacheFilesystem(asar);

        // Вернуть встроенный в Wand.exe хэш целостности к оригинальному заголовку app.asar.
        // Read-back обязателен: запись не прошла -> «Откат завершён» + Wand молча не стартует.
        AsarIntegrity.SyncAndVerify(appDir, asar, log);

        var backupUnpacked = Path.Combine(man.BackupRoot, "app.asar.unpacked");
        var unpacked = Path.Combine(resources, "app.asar.unpacked");
        if (Directory.Exists(backupUnpacked))
        {
            if (Directory.Exists(unpacked)) Directory.Delete(unpacked, true);
            CopyDir(backupUnpacked, unpacked);
        }
        File.Delete(manifestPath);
        log("Откат завершён.");
    }

    static InvalidOperationException CorruptBackup(string path, string why, Exception? inner) =>
        new($"Бэкап повреждён ({path}): {why}. Откат отменён, app.asar не тронут. " +
            "Чистый Wand вернёт переустановка Wand.", inner);

    static void CopyDir(string s, string d)
    {
        Directory.CreateDirectory(d);
        foreach (var f in Directory.GetFiles(s)) File.Copy(f, Path.Combine(d, Path.GetFileName(f)), true);
        foreach (var sub in Directory.GetDirectories(s)) CopyDir(sub, Path.Combine(d, Path.GetFileName(sub)));
    }
}

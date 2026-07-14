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

        // Бэкап мог быть обрезан (антивирус/сбой копирования) - читаем заголовок ДО того, как затрём
        // рабочий app.asar. Иначе копия обрезанного бэкапа поверх живого asar = кирпич необратимо.
        try { _ = AsarIntegrity.ReadHeaderJson(backupAsar); }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                $"Бэкап повреждён ({backupAsar}): {e.Message}. Откат отменён, app.asar не тронут. " +
                "Чистый Wand вернёт переустановка Wand.", e);
        }

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

    static void CopyDir(string s, string d)
    {
        Directory.CreateDirectory(d);
        foreach (var f in Directory.GetFiles(s)) File.Copy(f, Path.Combine(d, Path.GetFileName(f)), true);
        foreach (var sub in Directory.GetDirectories(s)) CopyDir(sub, Path.Combine(d, Path.GetFileName(sub)));
    }
}

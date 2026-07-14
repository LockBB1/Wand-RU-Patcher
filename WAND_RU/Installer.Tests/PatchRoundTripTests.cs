using System.IO;
using System.Linq;
using AsarSharp.AsarFileSystem;
using WandRuInstaller.Core;
using WandRuInstaller.Models;
using Xunit;

namespace WandRuInstaller.Tests;

/// <summary>Патч+откат на pristine temp-копии repo Wand. Wand НЕ запускается.</summary>
public class PatchRoundTripTests
{
    [Fact]
    public void Apply_registers_ru_locale_in_asar_tree()
    {
        var appDir = TestPaths.PristineAppCopy();
        var man = new RuPatcher(appDir, RuOverrides.LoadEmbedded()).Apply();

        Assert.Equal("12.37.0", man.AppVersion);
        Assert.True(File.Exists(Path.Combine(appDir, "resources", "wand-ru-patch.json")));

        // ru-RU.json должен быть в ДЕРЕВЕ нового app.asar (так его видит Electron), не только в unpacked.
        var newAsar = Path.Combine(appDir, "resources", "app.asar");
        var listed = Disk.ReadFilesystemSync(newAsar).ListFiles();
        Assert.Contains(listed, p => p.EndsWith("ru-RU.json"));

        // и содержимое ru-RU корректно (в unpacked-копии, куда пишет PatchTree)
        var ruUnpacked = Path.Combine(appDir, "resources", "app.asar.unpacked", "static", "strings", "ru-RU.json");
        Assert.True(File.Exists(ruUnpacked));
        Assert.Contains("Главная", File.ReadAllText(ruUnpacked));
    }

    // CRIT-1/2: манифест (с BackupRoot) пишется ДО подмены asar, сборка идёт в .wru-build и метётся,
    // подмена атомарна (File.Replace). Инвариант отката: после Apply манифест указывает на ЧИТАЕМЫЙ
    // бэкап (иначе прерванный между подменой и синком exe патч оставил бы недостижимый оригинал),
    // а временный .wru-build не остаётся в resources.
    [Fact]
    public void Apply_manifest_points_to_readable_backup_and_leaves_no_build_temp()
    {
        var appDir = TestPaths.PristineAppCopy();
        var man = new RuPatcher(appDir, RuOverrides.LoadEmbedded()).Apply();
        var res = Path.Combine(appDir, "resources");

        Assert.False(string.IsNullOrEmpty(man.BackupRoot));
        var backupAsar = Path.Combine(man.BackupRoot, "app.asar");
        Assert.True(File.Exists(backupAsar));
        Assert.NotEmpty(AsarIntegrity.ReadHeaderJson(backupAsar));      // бэкап цел - откат возможен
        Assert.False(Directory.Exists(Path.Combine(res, ".wru-build"))); // временная сборка убрана
    }

    [Fact]
    public void Apply_injects_cheat_hook_when_enabled()
    {
        var appDir = TestPaths.PristineAppCopy();
        new RuPatcher(appDir, RuOverrides.LoadEmbedded(), translateCheats: true).Apply();

        var unpacked = Path.Combine(appDir, "resources", "app.asar.unpacked");
        Assert.True(File.Exists(Path.Combine(unpacked, "cheat-hook.js")));
        Assert.Contains("cheat-hook.js", File.ReadAllText(Path.Combine(unpacked, "index.html")));

        // cheat-hook.js должен попасть в дерево нового app.asar (так его грузит Electron).
        var listed = Disk.ReadFilesystemSync(Path.Combine(appDir, "resources", "app.asar")).ListFiles();
        Assert.Contains(listed, p => p.EndsWith("cheat-hook.js"));
    }

    [Fact]
    public void Apply_skips_cheat_hook_when_disabled()
    {
        var appDir = TestPaths.PristineAppCopy();
        new RuPatcher(appDir, RuOverrides.LoadEmbedded(), translateCheats: false).Apply();

        var unpacked = Path.Combine(appDir, "resources", "app.asar.unpacked");
        Assert.False(File.Exists(Path.Combine(unpacked, "cheat-hook.js")));
        Assert.DoesNotContain("cheat-hook.js", File.ReadAllText(Path.Combine(unpacked, "index.html")));
    }

    [Fact]
    public void CheatHook_inject_is_idempotent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wru-hook-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "index.html"), "<html><head></head><body></body></html>");
            CheatHook.Inject(dir, "/*hook*/");
            CheatHook.Inject(dir, "/*hook*/");
            var html = File.ReadAllText(Path.Combine(dir, "index.html"));
            var count = html.Split("cheat-hook.js").Length - 1;
            Assert.Equal(1, count); // подключён ровно один раз
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Restore_reverts_asar_and_removes_manifest()
    {
        var appDir = TestPaths.PristineAppCopy();
        var asar = Path.Combine(appDir, "resources", "app.asar");
        var before = File.ReadAllBytes(asar);

        new RuPatcher(appDir, RuOverrides.LoadEmbedded()).Apply();
        Assert.NotEqual(before, File.ReadAllBytes(asar));

        RuUnpatcher.Restore(appDir);
        Assert.Equal(before, File.ReadAllBytes(asar));
        Assert.False(File.Exists(Path.Combine(appDir, "resources", "wand-ru-patch.json")));
    }

    // Регресс (фальшивый бэкап): бэкап снесли (антивирус/клинер), а app.asar - уже НАШ патч.
    // Копия патча как «оригинал» убивает откат навсегда и молча. Теперь: детект + отказ, а с явного
    // согласия юзера - патч без бэкапа (BackupRoot пуст), откат честно недоступен.
    [Fact]
    public void Apply_refuses_to_back_up_patched_asar_when_backup_lost()
    {
        var appDir = PatchedAppWithBackupLost();

        Assert.True(RuPatcher.BackupLost(appDir));   // UI спросит юзера до патча
        var ex = Assert.Throws<InvalidOperationException>(
            () => new RuPatcher(appDir, RuOverrides.LoadEmbedded()).Apply());
        Assert.Contains("бэкап", ex.Message, System.StringComparison.OrdinalIgnoreCase);

        // Ничего не сохранили: фальшивого бэкапа нет, manifest не переписан.
        Assert.False(Directory.Exists(Path.Combine(appDir, "resources", "wand-ru-backup")));
        Assert.False(File.Exists(Path.Combine(appDir, "resources", "wand-ru-patch.json")));
    }

    // CRIT-4: бэкап обрезан (антивирус/сбой) - откат обязан отказаться ДО перезаписи живого asar,
    // иначе копия огрызка поверх рабочего Wand = кирпич необратимо.
    [Fact]
    public void Restore_refuses_corrupt_backup_and_keeps_live_asar()
    {
        var appDir = TestPaths.PristineAppCopy();
        new RuPatcher(appDir, RuOverrides.LoadEmbedded()).Apply();
        var res = Path.Combine(appDir, "resources");
        var asar = Path.Combine(res, "app.asar");
        var patched = File.ReadAllBytes(asar);

        var man = System.Text.Json.JsonSerializer.Deserialize<PatchManifest>(
            File.ReadAllText(Path.Combine(res, "wand-ru-patch.json")))!;
        File.WriteAllBytes(Path.Combine(man.BackupRoot, "app.asar"), new byte[8]); // обрезали бэкап

        var ex = Assert.Throws<InvalidOperationException>(() => RuUnpatcher.Restore(appDir));
        Assert.Contains("повреждён", ex.Message);
        Assert.Equal(patched, File.ReadAllBytes(asar));                                // asar не тронут
        Assert.True(File.Exists(Path.Combine(res, "wand-ru-patch.json")));             // откат не состоялся
    }

    [Fact]
    public void Apply_without_backup_when_user_confirms_marks_restore_unavailable()
    {
        var appDir = PatchedAppWithBackupLost();

        var man = new RuPatcher(appDir, RuOverrides.LoadEmbedded(), allowMissingBackup: true).Apply();

        Assert.Equal("", man.BackupRoot);   // патч поверх патча, но за оригинал его НЕ выдаём
        var ex = Assert.Throws<InvalidOperationException>(() => RuUnpatcher.Restore(appDir));
        Assert.Contains("Откат невозможен", ex.Message);
    }

    [Fact]
    public void BackupLost_is_false_on_pristine_and_on_healthy_patch()
    {
        var appDir = TestPaths.PristineAppCopy();
        Assert.False(RuPatcher.BackupLost(appDir));   // чистый Wand: бэкапить есть что

        new RuPatcher(appDir, RuOverrides.LoadEmbedded()).Apply();
        Assert.False(RuPatcher.BackupLost(appDir));   // патч с живым бэкапом: откат на месте
    }

    // Регресс: «Готово» печаталось безусловно - якорь читов/карт мог не найтись (CheatHook молча
    // `continue`, map-хук молча «пропуск»), и юзер уходил с недопатченным Wand. Теперь Apply отдаёт
    // отчёт: что запросили и что реально легло.
    [Fact]
    public void Apply_reports_what_actually_landed()
    {
        var appDir = TestPaths.PristineAppCopy();
        var patcher = new RuPatcher(appDir, RuOverrides.LoadEmbedded(), translateCheats: true, translateMaps: true);

        patcher.Apply();

        var r = patcher.Report!;
        Assert.True(r.Locale);      // ru-RU.json + регистрация в JS
        Assert.True(r.LangName);    // якорь native-имени языка
        Assert.True(r.Cheats);      // cheat-hook.js подключён в index.html
        Assert.True(r.Maps);        // map-хук в index.js
        // r.Flag намеренно не утверждаем: флаг-пары живут в отдельном бандле, и на части версий Wand
        // (напр. 12.37 = фикстура) в нём нет якорей локали -> флаг не ложится. Отчёт это и показывает.
    }

    [Fact]
    public void Apply_report_marks_disabled_components_as_not_requested()
    {
        var appDir = TestPaths.PristineAppCopy();
        var patcher = new RuPatcher(appDir, RuOverrides.LoadEmbedded(), translateCheats: false, translateMaps: false);

        patcher.Apply();

        var r = patcher.Report!;
        Assert.True(r.Locale);
        Assert.Null(r.Cheats);   // не просили - в отчёт не попадает
        Assert.Null(r.Maps);
    }

    /// <summary>Пропатченный Wand, у которого снесли и бэкап, и manifest (антивирус/клинер/юзер).</summary>
    static string PatchedAppWithBackupLost()
    {
        var appDir = TestPaths.PristineAppCopy();
        var res = Path.Combine(appDir, "resources");
        new RuPatcher(appDir, RuOverrides.LoadEmbedded()).Apply();
        Directory.Delete(Path.Combine(res, "wand-ru-backup"), true);
        File.Delete(Path.Combine(res, "wand-ru-patch.json"));
        return appDir;
    }

    // Регресс: патченый Wand молча не стартует, если хэш заголовка app.asar в Wand.exe не обновлён
    // (Electron fuse integrity). После Apply exe должен указывать на хэш ПАТЧЕНОГО заголовка, после
    // Restore - вернуться к оригинальному.
    [Fact]
    public void Apply_and_restore_keep_exe_integrity_hash_in_sync()
    {
        var appDir = TestPaths.PristineAppCopy();
        var asar = Path.Combine(appDir, "resources", "app.asar");
        var exe = Path.Combine(appDir, "Wand.exe");

        Assert.Equal(AsarIntegrity.ComputeHeaderHash(asar), AsarIntegrity.ReadHash(exe)); // pristine согласован

        new RuPatcher(appDir, RuOverrides.LoadEmbedded()).Apply();
        Assert.Equal(AsarIntegrity.ComputeHeaderHash(asar), AsarIntegrity.ReadHash(exe)); // патч -> новый хэш

        RuUnpatcher.Restore(appDir);
        Assert.Equal(AsarIntegrity.ComputeHeaderHash(asar), AsarIntegrity.ReadHash(exe)); // откат -> оригинал
    }
}

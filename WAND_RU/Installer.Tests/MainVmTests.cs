using System.IO;
using WandRuInstaller.Models;
using WandRuInstaller.ViewModels;
using Xunit;

namespace WandRuInstaller.Tests;

public class MainVmTests
{
    static string RootOf(string appDir) => Path.GetDirectoryName(appDir)!;

    // Экран «Готово» показывает, что реально легло. Вне WPF L.Get отдаёт сам ключ - проверяем структуру:
    // выключенный компонент в отчёт не попадает, промах якоря виден.
    [Fact]
    public void FormatReport_lists_only_requested_components_and_shows_misses()
    {
        var full = MainVm.FormatReport(new PatchReport(Locale: true, Flag: true, LangName: true, Cheats: true, Maps: true));
        Assert.Equal("S_Rep_Locale ✓ · S_Rep_Cheats ✓ · S_Rep_Maps ✓", full);

        // карты просили, но якорь не нашёлся (best-effort, не фейл); читы выключены - пункта нет.
        var partial = MainVm.FormatReport(new PatchReport(Locale: true, Flag: true, LangName: true, Cheats: null, Maps: false));
        Assert.Equal("S_Rep_Locale ✓ · S_Rep_Maps - S_Rep_NoAnchor", partial);

        // флаг не лёг (на части версий Wand его бандл без якорей) - отдельный пункт, а не тишина.
        var noFlag = MainVm.FormatReport(new PatchReport(Locale: true, Flag: false, LangName: true, Cheats: true, Maps: null));
        Assert.Equal("S_Rep_Locale ✓ · S_Rep_Flag - S_Rep_NoAnchor · S_Rep_Cheats ✓", noFlag);
    }

    [Fact]
    public void Detect_ready_then_patch_done_then_patched()
    {
        var appDir = TestPaths.PristineAppCopy();
        var root = RootOf(appDir);
        var vm = new MainVm();

        vm.DetectFrom(new[] { root });
        Assert.Equal(InstallerState.Ready, vm.State);

        vm.PatchAsync().GetAwaiter().GetResult();
        Assert.Equal(InstallerState.Done, vm.State);

        // повторный detect на том же корне видит пропатченное состояние
        vm.DetectFrom(new[] { root });
        Assert.Equal(InstallerState.Patched, vm.State);
    }

    // Регресс от манифест-до-подмены (0.17.8): если File.Replace упал ПОСЛЕ записи манифеста, на диске
    // остаётся манифест + оригинальный asar. «Пропатчено» = ложь. IsPatched теперь сверяется с заголовком
    // asar, а не с одним фактом манифеста -> UI показывает Ready, а не мнимо-русифицированный Wand.
    [Fact]
    public void Detect_ignores_stale_manifest_when_asar_not_actually_patched()
    {
        var appDir = TestPaths.PristineAppCopy();
        var res = Path.Combine(appDir, "resources");
        File.WriteAllText(Path.Combine(res, "wand-ru-patch.json"),
            "{\"Name\":\"Wand RU\",\"BackupRoot\":\"\"}");   // манифест есть, asar - оригинальный

        var vm = new MainVm();
        vm.DetectFrom(new[] { RootOf(appDir) });
        Assert.Equal(InstallerState.Ready, vm.State);       // не Patched: asar реально не наш
    }

    [Fact]
    public void Detect_notfound_when_no_wand()
    {
        var empty = Path.Combine(Path.GetTempPath(), "empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        var vm = new MainVm();
        vm.DetectFrom(new[] { empty });
        Assert.Equal(InstallerState.NotFound, vm.State);
    }

    [Fact]
    public void Patch_then_restore_returns_to_ready()
    {
        var appDir = TestPaths.PristineAppCopy();
        var root = RootOf(appDir);
        var vm = new MainVm();
        vm.DetectFrom(new[] { root });

        vm.PatchAsync().GetAwaiter().GetResult();
        vm.DetectFrom(new[] { root });
        Assert.Equal(InstallerState.Patched, vm.State);

        vm.RestoreAsync().GetAwaiter().GetResult();
        Assert.Equal(InstallerState.Ready, vm.State);
    }

    // CRIT-3: из состояния Error откат должен оставаться доступным (патч мог упасть ПОСЛЕ подмены asar -
    // Wand кирпич, откат - единственный путь назад). Раньше RestoreCommand разрешал только Patched.
    [Fact]
    public void Restore_stays_available_from_error_state()
    {
        var appDir = TestPaths.PristineAppCopy();
        var vm = new MainVm();
        vm.DetectFrom(new[] { RootOf(appDir) });
        Assert.Equal(InstallerState.Ready, vm.State);

        // Откат без установленного патча -> исключение -> Error (реальный путь в это состояние).
        vm.RestoreAsync().GetAwaiter().GetResult();
        Assert.Equal(InstallerState.Error, vm.State);

        Assert.True(vm.RestoreCommand.CanExecute(null));
    }

    [Fact]
    public void Log_auto_clears_at_cap()
    {
        var vm = new MainVm();
        for (var i = 0; i <= 5000; i++) vm.AppendLog("line " + i);
        Assert.True(vm.Log.Count < 5000);        // очистилось при пороге, не копится
        Assert.Equal("line 5000", vm.Log[^1]);   // последняя строка на месте после очистки
    }
}

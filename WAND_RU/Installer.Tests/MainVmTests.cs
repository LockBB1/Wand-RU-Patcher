using System.IO;
using WandRuInstaller.ViewModels;
using Xunit;

namespace WandRuInstaller.Tests;

public class MainVmTests
{
    static string RootOf(string appDir) => Path.GetDirectoryName(appDir)!;

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
}

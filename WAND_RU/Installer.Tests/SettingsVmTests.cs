using WandRuInstaller.Models;
using WandRuInstaller.ViewModels;
using Xunit;

namespace WandRuInstaller.Tests;

public class SettingsVmTests
{
    static WandInstall MakeInstall() => new()
    {
        RootDir = @"C:\Wand",
        AppDirs = new[] { @"C:\Wand\app-12.36.0", @"C:\Wand\app-12.37.0" },
        SelectedAppDir = @"C:\Wand\app-12.37.0",
    };

    [Fact]
    public void AppVersions_and_default_selection()
    {
        var vm = new SettingsVm(MakeInstall());
        Assert.Equal(new[] { "12.36.0", "12.37.0" }, vm.AppVersions);
        Assert.Equal("12.37.0", vm.SelectedAppVersion);
    }

    [Fact]
    public void ChangingVersion_updates_install_selected_dir()
    {
        var install = MakeInstall();
        var vm = new SettingsVm(install);
        vm.SelectedAppVersion = "12.36.0";
        Assert.EndsWith("app-12.36.0", install.SelectedAppDir);
    }
}

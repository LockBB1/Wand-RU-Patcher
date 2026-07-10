using System.IO;
using WandEnhancer.ReactiveUICore;
using WandRuInstaller.Models;

namespace WandRuInstaller.ViewModels;

public sealed class SettingsVm : ObservableObject
{
    readonly WandInstall _install;
    string _selectedAppVersion;
    bool _restartWandAfter;
    bool _showLog;

    public IReadOnlyList<string> AppVersions { get; }

    public string SelectedAppVersion
    {
        get => _selectedAppVersion;
        set { if (SetProperty(ref _selectedAppVersion, value)) ApplySelection(); }
    }

    public bool RestartWandAfter { get => _restartWandAfter; set => SetProperty(ref _restartWandAfter, value); }
    public bool ShowLog { get => _showLog; set => SetProperty(ref _showLog, value); }

    public SettingsVm(WandInstall install)
    {
        _install = install;
        AppVersions = install.AppDirs.Select(VersionOf).ToList();
        _selectedAppVersion = install.SelectedAppDir is not null
            ? VersionOf(install.SelectedAppDir)
            : AppVersions.FirstOrDefault() ?? "";
    }

    static string VersionOf(string appDir) => new DirectoryInfo(appDir).Name.Replace("app-", "");

    void ApplySelection()
    {
        var match = _install.AppDirs.FirstOrDefault(d => VersionOf(d) == _selectedAppVersion);
        if (match is not null) _install.SelectedAppDir = match;
    }
}

using System.IO;
using WandEnhancer.ReactiveUICore;
using WandRuInstaller.Core;
using WandRuInstaller.Models;

namespace WandRuInstaller.ViewModels;

public sealed class SettingsVm : ObservableObject
{
    readonly WandInstall _install;
    readonly AppSettings _appSettings;
    string _selectedAppVersion;
    bool _restartWandAfter;
    bool _showLog;
    bool _translateCheats;

    public IReadOnlyList<string> AppVersions { get; }

    public string SelectedAppVersion
    {
        get => _selectedAppVersion;
        set { if (SetProperty(ref _selectedAppVersion, value)) ApplySelection(); }
    }

    public bool RestartWandAfter
    {
        get => _restartWandAfter;
        set { if (SetProperty(ref _restartWandAfter, value)) { _appSettings.RestartWandAfter = value; _appSettings.Save(); } }
    }

    public bool ShowLog
    {
        get => _showLog;
        set { if (SetProperty(ref _showLog, value)) { _appSettings.ShowLog = value; _appSettings.Save(); } }
    }

    public bool TranslateCheats
    {
        get => _translateCheats;
        set { if (SetProperty(ref _translateCheats, value)) { _appSettings.TranslateCheats = value; _appSettings.Save(); } }
    }

    public SettingsVm(WandInstall install)
    {
        _install = install;
        _appSettings = AppSettings.Load();
        _restartWandAfter = _appSettings.RestartWandAfter;
        _showLog = _appSettings.ShowLog;
        _translateCheats = _appSettings.TranslateCheats;
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

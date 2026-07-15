using System.IO;
using System.Windows.Input;
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
    bool _translateCheatsOnline;
    bool _translateMaps;
    bool _translateMapsOnline;
    string _onlineProvider = "auto";
    string _cacheInfo = "";

    public IReadOnlyList<string> AppVersions { get; }

    /// <summary>MainVm подписывается: смена версии -> обновить шапку/состояние (без пере-Detect).</summary>
    public Action<string>? OnAppDirSelected { get; set; }

    /// <summary>MainVm подписывается: смена лога -> поднять/погасить приёмник диагностики карт.</summary>
    public Action<bool>? OnShowLogChanged { get; set; }

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
        set { if (SetProperty(ref _showLog, value)) { _appSettings.ShowLog = value; _appSettings.Save(); OnShowLogChanged?.Invoke(value); } }
    }

    public bool TranslateCheats
    {
        get => _translateCheats;
        set { if (SetProperty(ref _translateCheats, value)) { _appSettings.TranslateCheats = value; _appSettings.Save(); } }
    }

    public bool TranslateCheatsOnline
    {
        get => _translateCheatsOnline;
        set { if (SetProperty(ref _translateCheatsOnline, value)) { _appSettings.TranslateCheatsOnline = value; _appSettings.Save(); } }
    }

    public bool TranslateMaps
    {
        get => _translateMaps;
        set { if (SetProperty(ref _translateMaps, value)) { _appSettings.TranslateMaps = value; _appSettings.Save(); } }
    }

    public bool TranslateMapsOnline
    {
        get => _translateMapsOnline;
        set { if (SetProperty(ref _translateMapsOnline, value)) { _appSettings.TranslateMapsOnline = value; _appSettings.Save(); } }
    }

    public string OnlineProvider
    {
        get => _onlineProvider;
        set { if (SetProperty(ref _onlineProvider, value)) { _appSettings.OnlineProvider = value; _appSettings.Save(); } }
    }

    public string CacheInfo
    {
        get => _cacheInfo;
        private set => SetProperty(ref _cacheInfo, value);
    }

    public ICommand ClearCacheCommand { get; }

    void RefreshCacheInfo() => CacheInfo = $"{CheatCache.HumanSize()} · {CheatCache.EntryCount()}";

    public SettingsVm(WandInstall install)
    {
        _install = install;
        _appSettings = AppSettings.Load();
        _restartWandAfter = _appSettings.RestartWandAfter;
        _showLog = _appSettings.ShowLog;
        _translateCheats = _appSettings.TranslateCheats;
        _translateCheatsOnline = _appSettings.TranslateCheatsOnline;
        _translateMaps = _appSettings.TranslateMaps;
        _translateMapsOnline = _appSettings.TranslateMapsOnline;
        _onlineProvider = string.IsNullOrEmpty(_appSettings.OnlineProvider) ? "auto" : _appSettings.OnlineProvider;
        ClearCacheCommand = new RelayCommand(_ => { CheatCache.Clear(); RefreshCacheInfo(); });
        RefreshCacheInfo();
        AppVersions = install.AppDirs.Select(VersionOf).ToList();
        _selectedAppVersion = install.SelectedAppDir is not null
            ? VersionOf(install.SelectedAppDir)
            : AppVersions.FirstOrDefault() ?? "";
    }

    static string VersionOf(string appDir) => new DirectoryInfo(appDir).Name.Replace("app-", "");

    void ApplySelection()
    {
        var match = _install.AppDirs.FirstOrDefault(d => VersionOf(d) == _selectedAppVersion);
        if (match is null) return;
        _install.SelectedAppDir = match;
        // Закрепляем выбор: переживает рестарт, Detect берёт её вместо последней (играть на старой версии).
        _appSettings.PinnedAppVersion = _selectedAppVersion == VersionOf(_install.AppDirs[0]) ? null : _selectedAppVersion;
        _appSettings.Save();
        OnAppDirSelected?.Invoke(match);
    }
}

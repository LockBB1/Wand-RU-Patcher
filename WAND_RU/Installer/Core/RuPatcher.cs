using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AsarSharp;
using AsarSharp.AsarFileSystem;
using WandRuInstaller.Models;

namespace WandRuInstaller.Core;

/// <summary>
/// Нативный порт install-ru.ps1 (рецепт из _ref/WandEnhancer): backup → extract packed asar в
/// app.asar.unpacked (merge) → patch → repack → manifest. Идемпотентно.
/// </summary>
public sealed class RuPatcher
{
    static readonly UTF8Encoding Utf8NoBom = new(false);
    // Только оригинально-unpacked дерево остаётся вне asar; всё остальное паковать обратно.
    static readonly Regex UnpackDirs = new(@"^static\\unpacked.*$", RegexOptions.Compiled);

    readonly string _appDir, _resources, _asar, _unpacked, _manifestPath;
    readonly RuOverrides _ov;
    readonly bool _translateCheats;
    readonly Action<string> _log;

    public RuPatcher(string appDir, RuOverrides overrides, bool translateCheats = true, Action<string>? log = null)
    {
        _appDir = appDir;
        _ov = overrides;
        _translateCheats = translateCheats;
        _log = log ?? (_ => { });
        _resources = Path.Combine(appDir, "resources");
        _asar = Path.Combine(_resources, "app.asar");
        _unpacked = Path.Combine(_resources, "app.asar.unpacked");
        _manifestPath = Path.Combine(_resources, "wand-ru-patch.json");
    }

    public PatchManifest Apply()
    {
        if (!File.Exists(_asar)) throw new FileNotFoundException($"Нет app.asar: {_asar}");

        var backupRoot = EnsureBackup();

        _log("Распаковка app.asar…");
        AsarExtractor.ExtractAll(_asar, _unpacked);

        _log("Патч локали и JS…");
        PatchTree(_unpacked);

        _log("Сборка app.asar…");
        var newAsar = Path.Combine(Path.GetTempPath(), "app-ru-" + Guid.NewGuid().ToString("N") + ".asar");
        new AsarCreator(_unpacked, newAsar, new CreateOptions { Unpack = UnpackDirs })
            .CreatePackageWithOptions();
        File.Copy(newAsar, _asar, overwrite: true);
        File.Delete(newAsar);
        // ExtractAll закэшировал старый filesystem по этому пути; после перезаписи - сбросить.
        Disk.UncacheFilesystem(_asar);

        var man = new PatchManifest
        {
            Name = "Wand RU",
            PatchVersion = "0.1.0",
            Build = 1,
            AppVersion = new DirectoryInfo(_appDir).Name.Replace("app-", ""),
            InstalledAt = DateTimeOffset.Now.ToString("o"),
            BackupRoot = backupRoot,
        };
        File.WriteAllText(_manifestPath,
            JsonSerializer.Serialize(man, new JsonSerializerOptions { WriteIndented = true }), Utf8NoBom);
        _log("Готово.");
        return man;
    }

    string EnsureBackup()
    {
        if (File.Exists(_manifestPath))
        {
            var prev = JsonSerializer.Deserialize<PatchManifest>(File.ReadAllText(_manifestPath));
            if (prev is not null && !string.IsNullOrEmpty(prev.BackupRoot) && Directory.Exists(prev.BackupRoot))
                return prev.BackupRoot;
        }
        var root = Path.Combine(_resources, "wand-ru-backup", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(root);
        File.Copy(_asar, Path.Combine(root, "app.asar"));
        if (Directory.Exists(_unpacked)) CopyDir(_unpacked, Path.Combine(root, "app.asar.unpacked"));
        return root;
    }

    void PatchTree(string treeRoot)
    {
        var stringsDir = Path.Combine(treeRoot, "static", "strings");
        var enUs = Path.Combine(stringsDir, "en-US.json");
        if (File.Exists(enUs))
        {
            var ruJson = LocaleBuilder.BuildRuJson(File.ReadAllText(enUs), _ov);
            File.WriteAllText(Path.Combine(stringsDir, "ru-RU.json"), ruJson, Utf8NoBom);
        }
        foreach (var js in Directory.EnumerateFiles(treeRoot, "*.js", SearchOption.AllDirectories))
        {
            var src = File.ReadAllText(js);
            if (!JsLocalePatch.NeedsPatch(src)) continue;
            var patched = JsLocalePatch.Patch(src, _ov.LanguageNative, JsLocalePatch.RussianFlagDataUri);
            if (patched != src) File.WriteAllText(js, patched, Utf8NoBom);
        }
        if (_translateCheats)
        {
            _log("Инъекция перевода читов…");
            CheatHook.Inject(treeRoot);
        }
    }

    static void CopyDir(string s, string d)
    {
        Directory.CreateDirectory(d);
        foreach (var f in Directory.GetFiles(s)) File.Copy(f, Path.Combine(d, Path.GetFileName(f)), true);
        foreach (var sub in Directory.GetDirectories(s)) CopyDir(sub, Path.Combine(d, Path.GetFileName(sub)));
    }
}

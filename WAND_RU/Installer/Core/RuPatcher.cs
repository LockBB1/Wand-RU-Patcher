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
        VerifyTree(_unpacked); // до repack: оригинальный app.asar ещё не тронут

        _log("Сборка app.asar…");
        var newAsar = Path.Combine(Path.GetTempPath(), "app-ru-" + Guid.NewGuid().ToString("N") + ".asar");
        new AsarCreator(_unpacked, newAsar, new CreateOptions { Unpack = UnpackDirs })
            .CreatePackageWithOptions();
        File.Copy(newAsar, _asar, overwrite: true);
        File.Delete(newAsar);
        // ExtractAll закэшировал старый filesystem по этому пути; после перезаписи - сбросить.
        Disk.UncacheFilesystem(_asar);

        // Wand.exe хранит SHA256 заголовка app.asar (Electron fuse integrity). Заголовок изменился -
        // без обновления хэша Electron молча не стартует. Прописываем актуальный хэш в exe.
        if (AsarIntegrity.SyncAppDir(_appDir, _asar, _log) == 0)
            _log("Целостность: встроенная проверка не обнаружена (старая версия Wand) - пропуск.");
        else
            AsarIntegrity.VerifyExesMatch(_appDir, _asar); // read-back: хэш реально записан (иначе тихий не-старт)

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

    /// <summary>
    /// Честный фейл-детект: если якоря патча не нашлись в новой версии Wand, кидаем понятную
    /// ошибку ВМЕСТО тихого «успеха» без русского. Зовётся до repack - app.asar остаётся цел.
    /// </summary>
    internal static void VerifyTree(string treeRoot)
    {
        var ruJson = Path.Combine(treeRoot, "static", "strings", "ru-RU.json");
        var jsFiles = Directory.EnumerateFiles(treeRoot, "*.js", SearchOption.AllDirectories).ToList();
        var ok = File.Exists(ruJson) && jsFiles.Any(f => File.ReadAllText(f).Contains("\"ru-RU\""));
        if (!ok)
            throw new NotSupportedException(
                "Эта версия Wand пока не поддерживается: не найдены точки для вставки русской локали. " +
                "app.asar не изменён - Wand работает как раньше. Проверьте обновление WRP или создайте issue с экспортом лога.");

        // Guard: жадный якорь мог попасть не в список локалей (регресс на новой версии Wand).
        // Ловим ДО repack - app.asar ещё оригинальный, Wand не сломан.
        var corrupt = jsFiles.FirstOrDefault(f => JsLocalePatch.HasCorruption(File.ReadAllText(f)));
        if (corrupt is not null)
            throw new NotSupportedException(
                $"Патч локали дал сбой на этой версии Wand (якорь попал не в список локалей: {Path.GetFileName(corrupt)}). " +
                "app.asar не изменён. Обновите WRP или создайте issue с экспортом лога.");
    }

    static void CopyDir(string s, string d)
    {
        Directory.CreateDirectory(d);
        foreach (var f in Directory.GetFiles(s)) File.Copy(f, Path.Combine(d, Path.GetFileName(f)), true);
        foreach (var sub in Directory.GetDirectories(s)) CopyDir(sub, Path.Combine(d, Path.GetFileName(sub)));
    }
}

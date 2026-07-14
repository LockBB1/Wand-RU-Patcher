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

    public const string ManifestName = "wand-ru-patch.json";
    // Наш след в заголовке asar: ru-RU.json появляется в static/strings только от патча (Wand везёт 14 локалей без RU).
    const string RuLocaleEntry = "\"ru-RU.json\"";

    readonly string _appDir, _resources, _asar, _unpacked, _manifestPath;
    readonly RuOverrides _ov;
    readonly bool _translateCheats, _translateMaps, _translateMapsOnline, _mapDiag, _allowMissingBackup;
    readonly Action<string> _log;

    public RuPatcher(string appDir, RuOverrides overrides, bool translateCheats = true,
        bool translateMaps = true, bool translateMapsOnline = true, bool mapDiag = false,
        bool allowMissingBackup = false, Action<string>? log = null)
    {
        _appDir = appDir;
        _ov = overrides;
        _translateCheats = translateCheats;
        _translateMaps = translateMaps;
        _translateMapsOnline = translateMapsOnline;
        _mapDiag = mapDiag;
        _allowMissingBackup = allowMissingBackup;
        _log = log ?? (_ => { });
        _resources = Path.Combine(appDir, "resources");
        _asar = Path.Combine(_resources, "app.asar");
        _unpacked = Path.Combine(_resources, "app.asar.unpacked");
        _manifestPath = Path.Combine(_resources, ManifestName);
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

    /// <summary>Уже наш app.asar? Смотрим заголовок (ru-RU.json кладём только мы) - без распаковки.</summary>
    internal static bool IsAsarPatched(string asarPath) =>
        File.Exists(asarPath) &&
        AsarIntegrity.ReadHeaderJson(asarPath).Contains(RuLocaleEntry, StringComparison.Ordinal);

    /// <summary>
    /// Бэкап утерян (антивирус/клинер/юзер), а app.asar - уже наш патч. Копировать его как «оригинал»
    /// нельзя: откат навсегда вернёт патч вместо чистого Wand. UI зовёт ДО патча - спросить юзера.
    /// </summary>
    public static bool BackupLost(string appDir)
    {
        var resources = Path.Combine(appDir, "resources");
        return !HasUsableBackup(Path.Combine(resources, ManifestName))
               && IsAsarPatched(Path.Combine(resources, "app.asar"));
    }

    static bool HasUsableBackup(string manifestPath) =>
        ReadManifest(manifestPath) is { BackupRoot: var root }
        && !string.IsNullOrEmpty(root) && Directory.Exists(root);

    static PatchManifest? ReadManifest(string manifestPath)
    {
        if (!File.Exists(manifestPath)) return null;
        try { return JsonSerializer.Deserialize<PatchManifest>(File.ReadAllText(manifestPath)); }
        catch (JsonException) { return null; } // битый manifest = бэкапа считай нет
    }

    /// <summary>Путь бэкапа: существующий, свежий или "" (бэкап утерян, откат недоступен - по согласию юзера).</summary>
    string EnsureBackup()
    {
        if (HasUsableBackup(_manifestPath)) return ReadManifest(_manifestPath)!.BackupRoot;

        if (IsAsarPatched(_asar))
        {
            // Оригинала нет: текущий asar - наш патч. Молча бэкапить его = убить откат навсегда.
            if (!_allowMissingBackup)
                throw new InvalidOperationException(
                    "Бэкап оригинального app.asar утерян, а Wand уже русифицирован - оригинал взять неоткуда. " +
                    "Переустановите Wand, затем русифицируйте заново.");
            _log("ВНИМАНИЕ: бэкап утерян, а app.asar уже русифицирован - оригинал НЕ сохраняем " +
                 "(копия патча - не оригинал). ОТКАТ БУДЕТ НЕДОСТУПЕН: чистый Wand вернёт только его переустановка.");
            return "";
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
            // Локаль-патч - только по своим якорям; embed-fix - на всех JS (билдеры URL в др. бандлах).
            var patched = JsLocalePatch.NeedsPatch(src)
                ? JsLocalePatch.Patch(src, _ov.LanguageNative, JsLocalePatch.RussianFlagDataUri)
                : src;
            patched = JsLocalePatch.NeutralizeEmbedLocale(patched);
            if (patched != src) File.WriteAllText(js, patched, Utf8NoBom);
        }
        if (_translateCheats)
        {
            _log("Инъекция перевода читов…");
            CheatHook.Inject(treeRoot);
        }

        // Path D (перевод карт, Шаг 1 PoC): main-процесс index.js - инъектор в map-фрейм.
        // Best-effort: якорь не нашёлся на новой версии Wand -> карты не хукаются, но патч цел.
        var indexJs = Path.Combine(treeRoot, "index.js");
        if (File.Exists(indexJs))
        {
            var main = File.ReadAllText(indexJs);
            if (!_translateMaps)
            {
                // Перевод карт выключен: убрать прошлый хук, если был (иначе останется от прошлой установки).
                var stripped = MapFrameHook.Strip(main);
                if (stripped != main) { File.WriteAllText(indexJs, stripped, Utf8NoBom); _log("Карты: перевод выключен - map-хук убран."); }
                else _log("Карты: перевод выключен - пропуск.");
            }
            else if (MapFrameHook.NeedsPatch(main))
            {
                var wasPatched = MapFrameHook.IsPatched(main);
                var patched = MapFrameHook.Patch(main, _translateMapsOnline, _mapDiag); // strip прошлого + актуальный хук
                if (patched != main)
                {
                    File.WriteAllText(indexJs, patched, Utf8NoBom);
                    _log(wasPatched ? "Карты: map-хук обновлён в index.js." : "Карты: map-хук встроен в index.js (якорь найден).");
                }
                else if (wasPatched)
                    _log("Карты: map-хук уже актуален в index.js.");
            }
            else
                _log("Карты: якорь главного окна в index.js не найден (новая версия Wand?) - пропуск map-хука.");
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

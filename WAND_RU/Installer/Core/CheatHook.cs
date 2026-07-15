using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace WandRuInstaller.Core;

/// <summary>
/// Вставляет renderer-скрипт перевода имён читов (cheat-hook.js) в распакованное дерево asar.
/// Кладёт файл в корень дерева и добавляет &lt;script&gt; в &lt;head&gt; index.html/overlay.html
/// (не-defer - ставит хук до загрузки app-бандлов и до fetch читов). Идемпотентно.
/// </summary>
public static class CheatHook
{
    public const string FileName = "cheat-hook.js";
    const string ScriptTag = "<script src=\"cheat-hook.js\"></script>";
    static readonly UTF8Encoding Utf8NoBom = new(false);
    static readonly Regex HeadOpen = new("<head[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Текст cheat-hook.js из встроенного ресурса (сгенерирован build-hook.mjs).</summary>
    public static string LoadEmbedded()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith(FileName, StringComparison.OrdinalIgnoreCase));
        using var s = asm.GetManifestResourceStream(name)!;
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    /// <summary>Кладёт cheat-hook.js в корень дерева и подключает его в index.html/overlay.html.</summary>
    public static void Inject(string treeRoot, string? script = null)
    {
        script ??= LoadEmbedded();
        File.WriteAllText(Path.Combine(treeRoot, FileName), script, Utf8NoBom);
        foreach (var html in new[] { "index.html", "overlay.html" })
        {
            var path = Path.Combine(treeRoot, html);
            if (!File.Exists(path)) continue;
            var src = File.ReadAllText(path);
            if (src.Contains(FileName)) continue; // уже подключён
            var m = HeadOpen.Match(src);
            if (!m.Success) continue; // нет <head> - пропускаем
            var patched = src.Insert(m.Index + m.Length, ScriptTag);
            File.WriteAllText(path, patched, Utf8NoBom);
        }
    }

    /// <summary>
    /// Убирает подключение cheat-hook.js из index.html/overlay.html и удаляет сам файл хука.
    /// Нужно, когда перевод читов выключен: без strip хук от прошлой установки остаётся работать,
    /// а отчёт честно говорит «читы выключено» - ложный рапорт. Возвращает true, если что-то убрал.
    /// </summary>
    public static bool Strip(string treeRoot)
    {
        var changed = false;
        foreach (var html in new[] { "index.html", "overlay.html" })
        {
            var path = Path.Combine(treeRoot, html);
            if (!File.Exists(path)) continue;
            var src = File.ReadAllText(path);
            if (!src.Contains(ScriptTag)) continue;
            File.WriteAllText(path, src.Replace(ScriptTag, ""), Utf8NoBom);
            changed = true;
        }
        var hookFile = Path.Combine(treeRoot, FileName);
        if (File.Exists(hookFile)) { File.Delete(hookFile); changed = true; }
        return changed;
    }
}

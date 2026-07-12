using System.Text.Json;
using System.Text.RegularExpressions;

namespace WandRuInstaller.Core;

/// <summary>
/// Path D (перевод карт): патч main-процесса index.js. Карта Wand - cross-origin &lt;iframe&gt;
/// (wand.com), SOP не даёт renderer-хуку до неё дотянуться. Из main-процесса же
/// webFrameMain.executeJavaScript впрыскивает скрипт прямо в контекст фрейма (обход SOP).
///
/// Вешаем did-frame-navigate на главное окно; при навигации подфрейма на wand.com/maps впрыск.
/// ШАГ 1 PoC = скрипт-дампер: ловит попап POI (крупнейший добавленный узел ~600мс после клика),
/// шлёт outerHTML в main через console.log, main пишет в ~/wand-ru-map-dump.log. Так узнаём
/// точные DOM-селекторы title/description до написания переводчика (Шаг 2).
///
/// Дампер за env-флагом WANDRU_MAP_DUMP - у релизных юзеров код инертен (нет дампа/файла).
/// Якорь структурный с захватом минифицированных имён (win/electron) - устойчив к ренейму
/// (Wand 12.36-12.38 сверено). integrity-fuse не нужен отдельно: index.js внутри asar,
/// AsarIntegrity пере-синкает хэш после repack.
/// </summary>
public static class MapFrameHook
{
    public const string Marker = "__WANDRU_MAPHOOK__";

    // <win>=new <electron>.BrowserWindow(<opts>.windowOptions) - создание главного окна Wand.
    static readonly Regex MainWindow = new(
        @"(\w+)=new (\w+)\.BrowserWindow\((\w+)\.windowOptions\)", RegexOptions.Compiled);

    // Впрыскивается В map-фрейм. Ванильный ES5, без внешних зависимостей и fetch (CSP wand.com).
    const string DumpScript = """
(function(){if(window.__wandruDumper)return;window.__wandruDumper=1;var recent=[];new MutationObserver(function(ms){ms.forEach(function(m){for(var i=0;i<m.addedNodes.length;i++){var n=m.addedNodes[i];if(n.nodeType===1&&n.textContent&&n.textContent.trim().length>40)recent.push(n);}});}).observe(document.documentElement,{childList:true,subtree:true});document.addEventListener("click",function(){recent=[];setTimeout(function(){var b=recent.sort(function(a,c){return c.textContent.length-a.textContent.length;})[0];if(b){try{console.log("WANDRU_DUMP::"+btoa(unescape(encodeURIComponent(b.outerHTML.slice(0,6000)))));}catch(e){}}},600);},true);})();
""";

    public static bool IsPatched(string js) => js.Contains(Marker);

    public static bool NeedsPatch(string js) => !IsPatched(js) && MainWindow.IsMatch(js);

    /// <summary>Вставляет did-frame-navigate инъектор + console-relay после создания главного окна. Идемпотентно.</summary>
    public static string Patch(string js)
    {
        if (IsPatched(js) || !MainWindow.IsMatch(js)) return js;
        var dumpLit = JsonSerializer.Serialize(DumpScript); // валидный JS-строковый литерал
        return MainWindow.Replace(js, m =>
        {
            var win = m.Groups[1].Value;   // главное окно (минифиц. имя)
            var el = m.Groups[2].Value;    // алиас require("electron")
            var inject =
                $";/*{Marker}*/try{{if(process.env.WANDRU_MAP_DUMP){{" +
                $"{win}.webContents.on(\"did-frame-navigate\",function(ev,u,c,t,mn,pi,ri){{" +
                $"if(!mn&&/wand\\.com\\/maps\\//.test(u)){{try{{{el}.webFrameMain.fromId(pi,ri).executeJavaScript({dumpLit})}}catch(_){{}}}}}});" +
                $"{win}.webContents.on(\"console-message\",function(ev,l,ms){{" +
                "var s=typeof ms==\"string\"?ms:(ev&&ev.message);" +
                "if(typeof s==\"string\"&&s.indexOf(\"WANDRU_DUMP::\")===0){try{" +
                "require(\"fs\").appendFileSync(require(\"path\").join(require(\"os\").homedir(),\"wand-ru-map-dump.log\")," +
                "Buffer.from(s.slice(13),\"base64\").toString(\"utf8\")+\"\\n\\n\")}catch(_){}}" +
                "});}}catch(_){}";
            return m.Value + inject;
        }, 1);
    }
}

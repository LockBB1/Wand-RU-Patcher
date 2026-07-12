using System.Text.Json;
using System.Text.RegularExpressions;

namespace WandRuInstaller.Core;

/// <summary>
/// Path D (перевод карт): патч main-процесса index.js. Карта Wand - cross-origin &lt;iframe&gt;
/// (wand.com), SOP не даёт renderer-хуку до неё дотянуться. Из main-процесса же
/// webFrameMain.executeJavaScript впрыскивает скрипт прямо в контекст фрейма (обход SOP).
///
/// Вешаем did-frame-navigate на главное окно; при навигации подфрейма на wand.com/maps впрыск
/// скрипта-дампера (ловит попап POI, шлёт outerHTML в main через console.log). main пишет всё в
/// ~/wand-ru-map-dump.log. Так узнаём точные DOM-селекторы title/description до переводчика (Шаг 2).
///
/// ДИАГНОСТИКА (Шаг 1): staged-строки на каждой границе (STAGE1 хук встал -> NAV навигации
/// фреймов -> STAGE2 матч карты -> STAGE3 инъект резолв/ошибка -> STAGE4 дамп). Канал - НЕ fs
/// (require/fs в этой ncc-точке не в scope; запись в профиль режет OneDrive), а o.net POST на
/// локальный приёмник инсталлера (MapDiagServer :39271) -> строки в лог инсталлера с Copy/Export.
/// o.net (electron main HTTP) не требует require/fs, loopback без CORS. Дампер шлёт "ARMED" сразу -
/// подтверждает пайп инъект->relay ещё до клика.
///
/// Якорь структурный с захватом минифицированных имён (win/electron) - устойчив к ренейму
/// (Wand 12.36-12.38 сверено). integrity-fuse не нужен: index.js внутри asar, AsarIntegrity
/// пере-синкает хэш после repack.
/// </summary>
public static class MapFrameHook
{
    public const string Marker = "__WANDRU_MAPHOOK__";

    // <win>=new <electron>.BrowserWindow(<opts>.windowOptions) - создание главного окна Wand.
    static readonly Regex MainWindow = new(
        @"(\w+)=new (\w+)\.BrowserWindow\((\w+)\.windowOptions\)", RegexOptions.Compiled);

    // Впрыскивается В map-фрейм. ES5, без fetch (CSP wand.com). Шлёт ARMED сразу + попап по клику.
    // (raw-литерал: бэкслеши литеральные -> "\n" остаётся JS-эскейпом новой строки, как надо)
    const string DumpScript = """
(function(){if(window.__wandruDumper)return;try{console.log("WANDRU_DUMP::"+btoa(unescape(encodeURIComponent("ARMED@"+location.href))))}catch(e){}var recent=[];new MutationObserver(function(ms){ms.forEach(function(m){for(var i=0;i<m.addedNodes.length;i++){var n=m.addedNodes[i];if(n.nodeType===1&&n.textContent&&n.textContent.trim().length>40)recent.push(n);}});}).observe(document,{childList:true,subtree:true});document.addEventListener("click",function(){recent=[];setTimeout(function(){var b=recent.sort(function(a,c){return c.textContent.length-a.textContent.length;})[0];if(b){try{console.log("WANDRU_DUMP::"+btoa(unescape(encodeURIComponent(b.outerHTML.slice(0,6000)))));}catch(e){}}},600);},true);window.__wandruDumper=1;})();
""";

    // Инъекция в main-процесс. Плейсхолдеры __WIN__/__EL__/__DUMP__ подставляются в Patch.
    // Канал: __EL__.net (electron main HTTP) POST на 127.0.0.1:39271 -> лог инсталлера. Без fs/require.
    // raw-литерал: JS-бэкслеши (\n, \., \/) сохраняются как есть -> валидный JS.
    const string InjectTemplate = """
;/*__WANDRU_MAPHOOK__*/try{function _p(l){try{var r=__EL__.net.request({method:"POST",url:"http://127.0.0.1:39271/"});r.on("error",function(){});r.write(typeof l=="string"?l:String(l));r.end()}catch(_){}}_p("STAGE1 main hook installed");__WIN__.webContents.on("did-frame-navigate",function(ev,u,c,t,mn,pi,ri){_p("NAV "+(mn?"main":"sub")+" "+u);if(!mn&&/wand\.com\/maps\//.test(u)){_p("STAGE2 map matched: "+u);try{__EL__.webFrameMain.fromId(pi,ri).executeJavaScript(__DUMP__).then(function(){_p("STAGE3 inject resolved")}).catch(function(e){_p("STAGE3 inject ERR "+e)})}catch(e){_p("STAGE2 throw "+e)}}});__WIN__.webContents.on("console-message",function(ev,l,ms){var s=typeof ms=="string"?ms:(ev&&ev.message);if(typeof s=="string"&&s.indexOf("WANDRU_DUMP::")===0){var txt;try{txt=Buffer.from(s.slice(13),"base64").toString("utf8")}catch(e){txt="(decode fail)"}_p("STAGE4 dump:\n"+txt)}});_p("STAGE1b listeners attached");}catch(e){try{__EL__.dialog.showErrorBox("WANDRU","FATAL "+e)}catch(_){}}
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
            var inject = InjectTemplate
                .Replace("__WIN__", win)
                .Replace("__EL__", el)
                .Replace("__DUMP__", dumpLit);
            return m.Value + inject;
        }, 1);
    }
}

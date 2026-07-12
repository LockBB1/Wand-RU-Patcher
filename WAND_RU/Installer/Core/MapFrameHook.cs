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
(function(){if(window.__wandruDumper)return;function send(t){try{console.log("WANDRU_DUMP::"+btoa(unescape(encodeURIComponent(t))))}catch(e){}}send("ARMED@"+location.href);document.addEventListener("click",function(){setTimeout(function(){var cands=document.querySelectorAll('[class*="ov-y_auto"],[role="dialog"],[data-state],[data-part]');var out=[],seen=[];for(var i=0;i<cands.length;i++){var e=cands[i];if(seen.indexOf(e)>=0)continue;seen.push(e);var txt=(e.textContent||"").trim().replace(/\s+/g," ");if(txt.length<12)continue;out.push("<"+e.tagName.toLowerCase()+" role="+(e.getAttribute("role")||"-")+" state="+(e.getAttribute("data-state")||"-")+" part="+(e.getAttribute("data-part")||"-")+" hidden="+e.hasAttribute("hidden")+" vis="+(e.offsetParent!==null)+" cls="+(typeof e.className=="string"?e.className.slice(0,55):"")+"> "+txt.slice(0,90));if(out.length>=14)break}send("CANDIDATES ("+out.length+"):\n"+out.join("\n\n"));},700);},true);window.__wandruDumper=1;})();
""";

    // Инъекция в main-процесс. Плейсхолдеры __WIN__/__EL__/__DUMP__ подставляются в Patch.
    // Канал: __EL__.net (electron main HTTP) POST на 127.0.0.1:39271 -> лог инсталлера. Без fs/require.
    // raw-литерал: JS-бэкслеши (\n, \., \/) сохраняются как есть -> валидный JS.
    const string InjectTemplate = """
;/*__WANDRU_MAPHOOK__*/try{function _p(l){try{var r=__EL__.net.request({method:"POST",url:"http://127.0.0.1:39271/"});r.on("error",function(){});r.write(typeof l=="string"?l:String(l));r.end()}catch(_){}}_p("STAGE1 main hook installed");__WIN__.webContents.on("did-frame-navigate",function(ev,u,c,t,mn,pi,ri){_p("NAV "+(mn?"main":"sub")+" "+u);if(!mn&&/wand\.com\/maps\//.test(u)){_p("STAGE2 map matched: "+u);try{__EL__.webFrameMain.fromId(pi,ri).executeJavaScript(__DUMP__).then(function(){_p("STAGE3 inject resolved")}).catch(function(e){_p("STAGE3 inject ERR "+e)})}catch(e){_p("STAGE2 throw "+e)}}});__WIN__.webContents.on("console-message",function(ev,l,ms){var s=typeof ms=="string"?ms:(ev&&ev.message);if(typeof s=="string"&&s.indexOf("WANDRU_DUMP::")===0){var txt;try{txt=Buffer.from(s.slice(13),"base64").toString("utf8")}catch(e){txt="(decode fail)"}_p("STAGE4 dump:\n"+txt)}});_p("STAGE1b listeners attached");}catch(e){try{__EL__.dialog.showErrorBox("WANDRU","FATAL "+e)}catch(_){}}/*__WANDRU_MAPHOOK_END__*/
""";

    // Прошлый блок хука (парные маркеры) - для strip-then-reinject: пере-патч ставит АКТУАЛЬНЫЙ хук,
    // а не сохраняет старый (иначе обновление дампера/переводчика не встанет поверх). Singleline на всякий.
    static readonly Regex ExistingBlock = new(
        @";/\*__WANDRU_MAPHOOK__\*/.*?/\*__WANDRU_MAPHOOK_END__\*/",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public static bool IsPatched(string js) => js.Contains(Marker);

    /// <summary>Есть якорь главного окна - патчим (пере-инжектим для актуальности, даже если хук уже стоит).</summary>
    public static bool NeedsPatch(string js) => MainWindow.IsMatch(js);

    /// <summary>Снимает прошлый хук-блок и вставляет актуальный после создания главного окна. Идемпотентно.</summary>
    public static string Patch(string js)
    {
        if (!MainWindow.IsMatch(js)) return js;
        var clean = ExistingBlock.Replace(js, "");           // снять прошлую версию хука (обновляемость)
        var dumpLit = JsonSerializer.Serialize(DumpScript);  // валидный JS-строковый литерал
        return MainWindow.Replace(clean, m =>
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

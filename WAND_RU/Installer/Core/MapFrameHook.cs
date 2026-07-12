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

    // Впрыскивается В map-фрейм. ШАГ 2b: переводчик текст-узлов (MutationObserver+TreeWalker).
    // Словарь D = мгновенный офлайн (структурные/категории); промахи -> очередь -> WANDRU_MTREQ в main
    // (main переводит через o.net Google, зовёт window.__wandruApply обратно). Пропуск: video.js,
    // script/style, кириллица, не-латиница. ES5, без fetch (CSP). raw-литерал. Кириллица -> JSON \u.
    const string DumpScript = """
(function(){if(window.__wandruTr)return;window.__wandruTr=1;var D={"Reward":"Награда","Description":"Описание","Details":"Детали","Region":"Регион","Requirements":"Требования","Required":"Требуется","Reward:":"Награда:","Features":"Особенности","Stamina":"Выносливость","Recommended Weapon":"Рекомендуемое оружие","Starting Location":"Начальная локация","Collectibles":"Коллекционные","Bounty Poster":"Плакат о розыске","Cigarette Card":"Сигаретная карточка","Dinosaur Bones":"Кости динозавра","Dreamcatcher":"Ловец снов","Hunting Request":"Заказ на охоту","Item Request":"Заказ предмета","Legendary Animals":"Легендарные животные","Legendary Fish":"Легендарная рыба","Point of Interest":"Точка интереса","Rock Carving":"Наскальный рисунок","Gang Hideout":"Логово банды","Video Pins":"Видео-метки","Discovery Progress":"Прогресс открытия","Hide All":"Скрыть все","Teleport only":"Только телепорт","Unfound only":"Только ненайденные","Your Timeline":"Ваша хронология","Continue":"Продолжить","Cancel":"Отмена","Log in":"Войти","Reset":"Сброс"};var pending={},sent={},timer=null,cnt=0;var cyr=/[а-яёА-ЯЁ]/,lat=/[A-Za-z]/;function send(t){try{console.log("WANDRU_DUMP::"+btoa(unescape(encodeURIComponent(t))))}catch(e){}}function req(t){try{console.log("WANDRU_MTREQ::"+btoa(unescape(encodeURIComponent(t))))}catch(e){}}send("TR-ARMED@"+location.href);function skip(node){var p=node.parentNode;while(p&&p.nodeType===1){var tn=p.tagName;if(tn==="SCRIPT"||tn==="STYLE"||tn==="NOSCRIPT"||tn==="TEXTAREA")return true;var c=typeof p.className=="string"?p.className:"";if(c.indexOf("video-js")>=0||c.indexOf("vjs-")>=0)return true;p=p.parentNode}return false}function tr(node){var v=node.nodeValue;if(!v)return;var t=v.trim();if(t.length<2||cyr.test(t)||!lat.test(t))return;if(skip(node))return;var r=D[t];if(r){node.nodeValue=v.replace(t,r);cnt++;return}if(!sent[t]&&!pending[t]){pending[t]=1;schedule()}}function schedule(){if(!timer)timer=setTimeout(flush,400)}function flush(){timer=null;var arr=[],k;for(k in pending){if(pending.hasOwnProperty(k)){arr.push(k);sent[k]=1;delete pending[k];if(arr.length>=20)break}}if(arr.length)req(JSON.stringify(arr));for(k in pending){schedule();break}}function walk(root){if(!root)return;if(root.nodeType===3){tr(root);return}if(root.nodeType!==1)return;var w=document.createTreeWalker(root,NodeFilter.SHOW_TEXT,null,false),n,b=[];while(n=w.nextNode())b.push(n);for(var i=0;i<b.length;i++)tr(b[i])}window.__wandruApply=function(map){var o;for(o in map){if(map.hasOwnProperty(o)&&map[o])D[o]=map[o]}walk(document.body)};function arm(){walk(document.body);new MutationObserver(function(ms){for(var i=0;i<ms.length;i++){var m=ms[i];if(m.type==="characterData")tr(m.target);else for(var j=0;j<m.addedNodes.length;j++)walk(m.addedNodes[j])}}).observe(document,{childList:true,subtree:true,characterData:true});setInterval(function(){if(cnt){send("TR replaced "+cnt+" nodes");cnt=0}},2000)}if(document.body)arm();else document.addEventListener("DOMContentLoaded",arm);})();
""";

    // Инъекция в main-процесс. Плейсхолдеры __WIN__/__EL__/__DUMP__ подставляются в Patch.
    // Канал: __EL__.net (electron main HTTP) POST на 127.0.0.1:39271 -> лог инсталлера. Без fs/require.
    // raw-литерал: JS-бэкслеши (\n, \., \/) сохраняются как есть -> валидный JS.
    const string InjectTemplate = """
;/*__WANDRU_MAPHOOK__*/try{var MF=null,CACHE={};function _p(l){try{var r=__EL__.net.request({method:"POST",url:"http://127.0.0.1:39271/"});r.on("error",function(){});r.write(typeof l=="string"?l:String(l));r.end()}catch(_){}}function _mt(q,cb){if(CACHE[q]!==undefined){cb(CACHE[q]);return}try{var rq=__EL__.net.request("https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=ru&dt=t&q="+encodeURIComponent(q)),data="";rq.on("response",function(res){res.on("data",function(c){data+=c.toString()});res.on("end",function(){var out=null;try{var j=JSON.parse(data),i;out="";for(i=0;i<j[0].length;i++)out+=j[0][i][0]}catch(e){out=null}CACHE[q]=out;cb(out)})});rq.on("error",function(){cb(null)});rq.end()}catch(e){cb(null)}}_p("STAGE1 main hook installed");__WIN__.webContents.on("did-frame-navigate",function(ev,u,c,t,mn,pi,ri){_p("NAV "+(mn?"main":"sub")+" "+u);if(!mn&&/wand\.com\/maps\//.test(u)){_p("STAGE2 map matched: "+u);try{MF=__EL__.webFrameMain.fromId(pi,ri);MF.executeJavaScript(__DUMP__).then(function(){_p("STAGE3 inject resolved")}).catch(function(e){_p("STAGE3 inject ERR "+e)})}catch(e){_p("STAGE2 throw "+e)}}});__WIN__.webContents.on("console-message",function(ev,l,ms){var s=typeof ms=="string"?ms:(ev&&ev.message);if(typeof s!=="string")return;if(s.indexOf("WANDRU_MTREQ::")===0){var arr;try{arr=JSON.parse(Buffer.from(s.slice(14),"base64").toString("utf8"))}catch(e){arr=[]}var res={},pend=arr.length;if(!pend)return;arr.forEach(function(q){_mt(q,function(r){if(r&&r!==q){res[q]=r;_p("HV\t"+q+"\t"+r)}if(--pend===0&&MF){try{MF.executeJavaScript("window.__wandruApply&&window.__wandruApply("+JSON.stringify(res)+")")}catch(_){}}})});return}if(s.indexOf("WANDRU_DUMP::")===0){var txt;try{txt=Buffer.from(s.slice(13),"base64").toString("utf8")}catch(e){txt="(decode fail)"}_p(txt)}});_p("STAGE1b listeners attached");}catch(e){try{__EL__.dialog.showErrorBox("WANDRU","FATAL "+e)}catch(_){}}/*__WANDRU_MAPHOOK_END__*/
""";

    // Прошлый блок хука (парные маркеры) - для strip-then-reinject: пере-патч ставит АКТУАЛЬНЫЙ хук,
    // а не сохраняет старый (иначе обновление дампера/переводчика не встанет поверх). Singleline на всякий.
    static readonly Regex ExistingBlock = new(
        @";/\*__WANDRU_MAPHOOK__\*/.*?/\*__WANDRU_MAPHOOK_END__\*/",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // Легаси-блок (0.16.3-0.16.8, до END-маркера): заканчивался FATAL-фоллбэком. Снимаем ПОСЛЕ
    // ExistingBlock (иначе съест хвост нового блока до его END). Иначе накопление двойных хуков.
    static readonly Regex LegacyBlock = new(
        @";/\*__WANDRU_MAPHOOK__\*/.*?FATAL ""\+e\)\}catch\(_\)\{\}\}",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public static bool IsPatched(string js) => js.Contains(Marker);

    /// <summary>Есть якорь главного окна - патчим (пере-инжектим для актуальности, даже если хук уже стоит).</summary>
    public static bool NeedsPatch(string js) => MainWindow.IsMatch(js);

    /// <summary>Снимает прошлый хук-блок и вставляет актуальный после создания главного окна. Идемпотентно.</summary>
    public static string Patch(string js)
    {
        if (!MainWindow.IsMatch(js)) return js;
        var clean = ExistingBlock.Replace(js, "");           // снять END-маркированные блоки (0.16.9+)
        clean = LegacyBlock.Replace(clean, "");              // снять легаси-блоки без END (0.16.3-0.16.8)
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

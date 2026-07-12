// map-translator - впрыскивается В cross-origin map-фрейм wand.com (через main-процесс
// webFrameMain.executeJavaScript, см. MapFrameHook). Переводит текст-узлы карты:
//   словарь D -> мгновенный офлайн; промахи -> очередь -> WANDRU_MTREQ в main -> Google MT ->
//   window.__wandruApply мержит перевод обратно.
// ES5 (гостевой контекст), без fetch (CSP wand.com). Встраивается как ресурс map-translator.js;
// в main сериализуется в JSON-строку и исполняется в фрейме.
(function () {
  if (window.__wandruTr) return;
  window.__wandruTr = 1;

  // Офлайн-словарь: структурные слова, категории RDR2, кнопки, бренды (identity - не слать в MT).
  var D = {
    "Reward": "Награда", "Description": "Описание", "Details": "Детали",
    "Region": "Регион", "Requirements": "Требования", "Required": "Требуется",
    "Reward:": "Награда:", "Features": "Особенности", "Stamina": "Выносливость",
    "Recommended Weapon": "Рекомендуемое оружие", "Starting Location": "Начальная локация",
    "Collectibles": "Коллекционные", "Bounty Poster": "Плакат о розыске",
    "Cigarette Card": "Сигаретная карточка", "Dinosaur Bones": "Кости динозавра",
    "Dreamcatcher": "Ловец снов", "Hunting Request": "Заказ на охоту",
    "Item Request": "Заказ предмета", "Legendary Animals": "Легендарные животные",
    "Legendary Fish": "Легендарная рыба", "Point of Interest": "Точка интереса",
    "Rock Carving": "Наскальный рисунок", "Gang Hideout": "Логово банды",
    "Video Pins": "Видео-метки", "Discovery Progress": "Прогресс открытия",
    "Hide All": "Скрыть все", "Teleport only": "Только телепорт",
    "Unfound only": "Только ненайденные", "Your Timeline": "Ваша хронология",
    "Continue": "Продолжить", "Cancel": "Отмена", "Log in": "Войти", "Reset": "Сброс",
    // Бренды/имена собственные - identity (иначе Wand->Палочка, game title -> перевод):
    "Wand": "Wand", "Wand Maps": "Wand Maps", "Wand logo": "Логотип Wand",
    "Red Dead Redemption 2": "Red Dead Redemption 2", "Beta": "Бета", "NEW": "НОВОЕ"
  };

  // Пер-карта словарь: main ставит window.__WANDRU_SEED ДО этого скрипта -> в D до первого walk
  // (иначе гонка: строки очередятся в MT раньше, чем придёт seed).
  try { var _sd = window.__WANDRU_SEED; if (_sd) for (var _sk in _sd) if (_sd[_sk]) D[_sk] = _sd[_sk]; } catch (e) {}

  var pending = {}, sent = {}, timer = null, cnt = 0;
  var cyr = /[а-яёА-ЯЁ]/, lat = /[A-Za-z]/;

  function send(t) { try { console.log("WANDRU_DUMP::" + btoa(unescape(encodeURIComponent(t)))); } catch (e) {} }
  function req(t) { try { console.log("WANDRU_MTREQ::" + btoa(unescape(encodeURIComponent(t)))); } catch (e) {} }
  send("TR-ARMED@" + location.href);

  // Узел не переводить: внутри script/style/code/pre или video.js-контролов, либо оторван от DOM.
  function skip(node) {
    var p = node.parentNode;
    if (!p) return true;
    while (p && p.nodeType === 1) {
      var tn = p.tagName ? p.tagName.toUpperCase() : "";
      if (tn === "SCRIPT" || tn === "STYLE" || tn === "NOSCRIPT" || tn === "TEXTAREA" || tn === "CODE" || tn === "PRE") return true;
      var c = typeof p.className == "string" ? p.className : "";
      if (c.indexOf("video-js") >= 0 || c.indexOf("vjs-") >= 0) return true;
      p = p.parentNode;
    }
    return false;
  }

  // Шаблоны фильтров карты "Show/Hide [only] {C} on map" - 380+ комбо на сессию, все офлайн.
  // Скелет по-русски всегда; категория {C} - из словаря D, иначе остаётся англ. (proper-noun ок).
  var FT = [
    [/^Show only (.+) on map$/, "Показывать только %C% на карте"],
    [/^Hide (.+) on map$/, "Скрыть %C% на карте"],
    [/^Show (.+) on map$/, "Показать %C% на карте"]
  ];
  function filterTr(t) {
    for (var i = 0; i < FT.length; i++) {
      var m = FT[i][0].exec(t);
      if (m) return FT[i][1].replace("%C%", D[m[1]] || m[1]);
    }
    return null;
  }

  // Перевести один текст-узел: словарь -> шаблон фильтра -> иначе в очередь на MT.
  function tr(node) {
    var v = node.nodeValue;
    if (!v) return;
    var t = v.trim();
    if (t.length < 2 || cyr.test(t) || !lat.test(t)) return; // пусто/кириллица/без латиницы
    if (t.indexOf("{") >= 0 || t.indexOf("}") >= 0) return;   // CSS/код - не текст
    if (skip(node)) return;
    var r = D[t];
    if (r) { node.nodeValue = v.replace(t, r); cnt++; return; }
    var f = filterTr(t);
    if (f) { node.nodeValue = v.replace(t, f); cnt++; return; }
    if (!sent[t] && !pending[t]) { pending[t] = 1; schedule(); }
  }

  function schedule() { if (!timer) timer = setTimeout(flush, 400); }

  // Отправить батч непереведённого в main (cap 20, дедуп через sent).
  function flush() {
    timer = null;
    var arr = [], k;
    for (k in pending) {
      if (pending.hasOwnProperty(k)) {
        arr.push(k); sent[k] = 1; delete pending[k];
        if (arr.length >= 20) break;
      }
    }
    if (arr.length) req(JSON.stringify(arr));
    for (k in pending) { schedule(); break; } // остались - ещё раунд
  }

  // Тайм-слайсинг: per-map словарь (1281+ записей) сделал синхронный walk блокирующим -
  // рефлоу-шторм от массовых замен узлов тормозил загрузку карты (MT был быстрее, т.к. капал
  // асинхронно вне критического пути). Очередь узлов + дренаж порциями <=6мс, отдаём кадр.
  var nq = [], draining = false;
  function enqueue(node) { nq.push(node); if (!draining) { draining = true; setTimeout(drain, 0); } }
  function drain() {
    var start = Date.now(), n = 0;
    while (nq.length) {
      tr(nq.shift());
      if (++n >= 300 && Date.now() - start > 6) { setTimeout(drain, 0); return; } // отдать кадр рендеру
    }
    draining = false;
  }
  function walk(root) {
    if (!root) return;
    if (root.nodeType === 3) { enqueue(root); return; }
    if (root.nodeType !== 1) return;
    var w = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null, false), n;
    while (n = w.nextNode()) enqueue(n);
  }

  // Main зовёт после MT: мержим перевод в словарь и перепроходим DOM.
  window.__wandruApply = function (map) {
    var o;
    for (o in map) { if (map.hasOwnProperty(o) && map[o]) D[o] = map[o]; }
    walk(document.body);
  };

  function arm() {
    walk(document.body);
    new MutationObserver(function (ms) {
      for (var i = 0; i < ms.length; i++) {
        var m = ms[i];
        if (m.type === "characterData") enqueue(m.target);
        else for (var j = 0; j < m.addedNodes.length; j++) walk(m.addedNodes[j]);
      }
    }).observe(document, { childList: true, subtree: true, characterData: true });
    setInterval(function () { if (cnt) { send("TR replaced " + cnt + " nodes"); cnt = 0; } }, 2000);
  }

  // did-frame-navigate впрыскивает рано - DOM может быть пуст; ждём body.
  if (document.body) arm();
  else document.addEventListener("DOMContentLoaded", arm);
})();

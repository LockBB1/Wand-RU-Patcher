;/*__WANDRU_MAPHOOK__*/
/* Впрыскивается в main-процесс index.js СРАЗУ после создания главного окна Wand.
   Плейсхолдеры подставляет MapFrameHook.Patch: __WIN__ = окно, __EL__ = require("electron"),
   __DUMP__ = JSON-строка map-translator (исполняется в map-фрейме).
   Канал в лог инсталлера: __EL__.net POST на 127.0.0.1:39271 (без fs/require, loopback без CORS).
   Парные маркеры __WANDRU_MAPHOOK__ ... _END - для strip-then-reinject (обновляемость). */
try {
  var CACHE = {}, cacheN = 0, CACHE_CAP = 5000;
  /* Пер-карта офлайн-словари {slug:{en:ru}} - подставляет MapFrameHook.Patch (__MAPS__).
     Мгновенный офлайн-перевод POI/событий: seed переводчика по slug из URL, MT только на остаток. */
  var MAPS = __MAPS__;
  /* UI фрейма AI-помощника {strings:{en:ru},templates:[[re,ru]]} - подставляет MapFrameHook.Patch
     (__ASSIST__). Помощник = cross-origin iframe mist.wand.com/assistant/embed, та же архитектура,
     что карта. Переводим только известный хром: ответы модели приходят по-русски сами. */
  var ASSIST = __ASSIST__;
  /* Флаги подставляет MapFrameHook.Patch по настройкам: MTON = онлайн-добор карт (Google/MyMemory),
     DIAG = диагностика в инсталлер (:39271, STAGE/NAV/HV). В релизе DIAG=false -> тихо. */
  var MTON = __MTON__, DIAG = __DIAG__;

  /* Строка в лог инсталлера (MapDiagServer). Только при DIAG - иначе релиз молчит. */
  function _p(l) {
    if (!DIAG) return;
    try {
      var r = __EL__.net.request({ method: "POST", url: "http://127.0.0.1:39271/" });
      r.on("error", function () {});
      r.write(typeof l == "string" ? l : String(l));
      r.end();
    } catch (_) {}
  }

  /* --- Онлайн-MT с устойчивостью: throttle (<=2 в полёте) + Google-gtx с 429-backoff -> MyMemory-фолбэк.
     A+B (офлайн-словарь + шаблоны фильтров) уже срезали ~90% запросов; тут - остаток (описания POI). --- */
  var Q = [], inflight = 0, MAXC = 2, gCoolUntil = 0;
  /* Кулдаун Google. 429 - минута; прочий отказ (403/500/таймаут/сеть) - короче: сетевой блип не должен
     слепить провайдера надолго, но и долбить его каждой следующей строкой смысла нет. */
  var G_COOL_429 = 60000, G_COOL_ERR = 30000;

  /* Сторожевой таймаут (у пути читов он есть: https.get {timeout:6000}, тут был забыт).
     Electron net.request своего таймаута не имеет, а повисший TCP к translate.googleapis.com для РФ -
     обычное дело: два таких сокета -> inflight не убывает -> очередь встаёт НАВСЕГДА и молча
     (в релизе DIAG=false), строки уже помечены sent[] и повторно не запрашиваются.
     once: abort() шлёт "error", ответ мог уже прийти - cb обязан сработать ровно раз. */
  var HTTP_TIMEOUT = 6000;
  function once(cb) {
    var done = false;
    return function (a, b) { if (done) return; done = true; cb(a, b); };
  }
  function deadline(rq, cb) {
    var t = setTimeout(function () { try { rq.abort(); } catch (_) {} cb(null, 0); }, HTTP_TIMEOUT);
    return function (a, b) { try { clearTimeout(t); } catch (_) {} cb(a, b); };
  }

  function _mt(q, cb) {
    if (CACHE[q] !== undefined) { cb(CACHE[q]); return; }
    if (!MTON) { cb(null); return; }           // онлайн-перевод карт выключен -> только офлайн
    Q.push([q, cb]); pump();
  }
  // Бренд-гвард: MT коверкает "Wand" -> "Палочка"/"Ванде". Чиним ТОЛЬКО когда исходник содержал Wand
  // (иначе не трогаем - у настоящих слов на "Ванд" ложных срабатываний нет).
  function brandFix(q, r) {
    if (!r || !/Wand/.test(q)) return r;
    return r.replace(/Палочк[а-яё]*/gi, "Wand").replace(/Ванд[а-яё]*/gi, "Wand");
  }
  function pump() {
    while (inflight < MAXC && Q.length) {
      var it = Q.shift(); inflight++;
      (function (q, cb) {
        one(q, function (r) {
          inflight--; r = brandFix(q, r);
          // Кэш живёт в main-процессе через смены карт (не гибнет с фреймом) - в долгой сессии рос бы без
          // предела. Приближённый cap: при пороге сбрасываем целиком (потеря кэша = ре-перевод, не поломка).
          if (cacheN >= CACHE_CAP) { CACHE = {}; cacheN = 0; }
          if (CACHE[q] === undefined) cacheN++;
          CACHE[q] = r; cb(r); setTimeout(pump, 120);   // ~throttle + бренд
        });
      })(it[0], it[1]);
    }
  }
  // Google в кулдауне -> сразу MyMemory; иначе Google, при любом отказе -> кулдаун + MyMemory.
  function one(q, cb) {
    if (Date.now() < gCoolUntil) { mymemory(q, cb); return; }
    google(q, function (r, code) {
      if (code === 429) { gCoolUntil = Date.now() + G_COOL_429; mymemory(q, cb); return; }
      if (r != null) { cb(r); return; }
      /* Отказ был не только при 429: без кулдауна следующая строка снова шла бы в мёртвый Google,
         а потом в MyMemory - 2x запросов и латентности на КАЖДОЙ строке. Эхо (r == исходник) сюда
         не попадает: это валидный ответ провайдера, а не отказ. */
      gCoolUntil = Date.now() + G_COOL_ERR;
      mymemory(q, cb);
    });
  }
  function google(q, cb) {
    cb = once(cb);
    try {
      var rq = __EL__.net.request("https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=ru&dt=t&q=" + encodeURIComponent(q)), data = "", code = 0;
      var fin = deadline(rq, cb);
      rq.on("response", function (res) {
        code = res.statusCode || 0;
        res.on("data", function (c) { data += c.toString(); });
        res.on("end", function () {
          var out = null;
          try { var j = JSON.parse(data), i; out = ""; for (i = 0; i < j[0].length; i++) out += j[0][i][0]; }
          catch (e) { out = null; }
          fin(out, code);
        });
      });
      rq.on("error", function () { fin(null, 0); });
      rq.end();
    } catch (e) { cb(null, 0); }
  }
  function mymemory(q, cb) {
    cb = once(cb);
    try {
      var rq = __EL__.net.request("https://api.mymemory.translated.net/get?langpair=en%7Cru&q=" + encodeURIComponent(q)), data = "";
      var fin = deadline(rq, cb);
      rq.on("response", function (res) {
        res.on("data", function (c) { data += c.toString(); });
        res.on("end", function () {
          var out = null;
          try { var t = JSON.parse(data).responseData.translatedText; if (t && t.toUpperCase().indexOf("MYMEMORY WARNING") < 0) out = t; }
          catch (e) { out = null; }
          fin(out);
        });
      });
      rq.on("error", function () { fin(null); });
      rq.end();
    } catch (e) { cb(null); }
  }

  _p("STAGE1 main hook installed");

  /* Вооружить ОДНО окно. Карта и помощник живут не только в главном окне: игровой оверлей - отдельное
     BrowserWindow (пересоздаётся при каждом входе в игру), и его фреймы раньше оставались английскими.
     Поэтому arm зовётся и для главного окна, и для каждого нового (browser-window-created ниже).
     mf - ПЕР-ОКОННАЯ: одна общая переменная отправляла бы MT-ответ в фрейм чужого окна. */
  function arm(W) {
    if (!W || W.__wandruArmed) return;   // окно уже вооружено - второй листенер дублировал бы работу
    W.__wandruArmed = true;
    var mf = null;

  /* При навигации подфрейма на wand.com/maps - впрыск переводчика в фрейм (обход SOP). */
  W.webContents.on("did-frame-navigate", function (ev, u, c, t, mn, pi, ri) {
    _p("NAV " + (mn ? "main" : "sub") + " " + u);
    if (!mn && /wand\.com\/maps\//.test(u)) {
      _p("STAGE2 map matched: " + u);
      var sl = (u.match(/\/maps\/([^\/?]+)/) || [])[1] || "";  // slug карты из URL (/maps/<slug>/)
      // Общий UI-хром (_common, все карты) + per-map словарь; per-map перекрывает.
      var dict = {}, kk, _c = MAPS["_common"] || {}, _m = MAPS[sl] || {};
      for (kk in _c) dict[kk] = _c[kk];
      for (kk in _m) dict[kk] = _m[kk];
      try {
        mf = __EL__.webFrameMain.fromId(pi, ri);
        /* Словарь ставим ПЕРЕД переводчиком (window.__WANDRU_SEED) - иначе гонка: translator arms и
           очередит строки в MT до прихода seed (лишний MT + бренд утекал через Google). Один executeJavaScript. */
        /* MTON во фрейм: при выключенном онлайне переводчик не заводит очередь MT (иначе копил бы
           waiting со ссылками на узлы - ответ на его батч всегда пустой). */
        mf.executeJavaScript("window.__WANDRU_MTON=" + (MTON ? "true" : "false") +
          ";window.__WANDRU_SEED=" + JSON.stringify(dict) + ";" + __DUMP__)
          .then(function () { _p("STAGE3 inject resolved; dict " + sl + "=" + Object.keys(dict).length); })
          .catch(function (e) { _p("STAGE3 inject ERR " + e); });
      } catch (e) { _p("STAGE2 throw " + e); }
    }
    /* Фрейм AI-помощника. Локаль в path зануляет JsLocalePatch (/en/ -> 307 -> без префикса), поэтому
       ловим оба вида URL. Свой фрейм-хэндл (af), НЕ mf: в оверлее рядом живёт карта, и MT-ответ карты
       не должен уехать в чат. UIONLY -> только словарь и атрибуты, без MT (ответы модели уже русские). */
    if (!mn && /wand\.com\/(?:[a-z-]{2,5}\/)?assistant\/embed/.test(u)) {
      _p("STAGE2 assistant matched: " + u);
      try {
        var af = __EL__.webFrameMain.fromId(pi, ri);
        var ad = ASSIST.strings || {}, at = ASSIST.templates || [];
        af.executeJavaScript(
          "window.__WANDRU_UIONLY=true;window.__WANDRU_SEED=" + JSON.stringify(ad) +
          ";window.__WANDRU_SEED_TPL=" + JSON.stringify(at) + ";" + __DUMP__)
          .then(function () { _p("STAGE3 assistant inject resolved; strings=" + Object.keys(ad).length); })
          .catch(function (e) { _p("STAGE3 assistant inject ERR " + e); });
      } catch (e) { _p("STAGE2 assistant throw " + e); }
    }
  });

  /* console-message из фрейма: MTREQ = батч на перевод; DUMP = строка в лог. */
  W.webContents.on("console-message", function (ev, l, ms) {
    var s = typeof ms == "string" ? ms : (ev && ev.message);
    if (typeof s !== "string") return;
    if (s.indexOf("WANDRU_MTREQ::") === 0) {
      var arr;
      try { arr = JSON.parse(Buffer.from(s.slice(14), "base64").toString("utf8")); } catch (e) { arr = []; }
      var res = {}, pend = arr.length;
      if (!pend) return;
      arr.forEach(function (q) {
        _mt(q, function (r) {
          if (r && r !== q) { res[q] = r; _p("HV\t" + q + "\t" + r); } /* харвест-пара */
          if (--pend === 0 && mf) {
            try { mf.executeJavaScript("window.__wandruApply&&window.__wandruApply(" + JSON.stringify(res) + ")"); } catch (_) {}
          }
        });
      });
      return;
    }
    if (s.indexOf("WANDRU_DUMP::") === 0) {
      var txt;
      try { txt = Buffer.from(s.slice(13), "base64").toString("utf8"); } catch (e) { txt = "(decode fail)"; }
      _p(txt);
    }
  });

  _p("STAGE1b listeners attached");
  }

  arm(__WIN__);   // главное окно: создано прямо перед этим блоком, до подписки ниже
  /* Игровой оверлей и любые будущие окна Wand. Оверлей создаётся позже (вход в игру) и
     пересоздаётся через destroyOverlayWindow - каждое новое окно вооружаем заново. */
  __EL__.app.on("browser-window-created", function (e, w) { try { arm(w); } catch (_) {} });
} catch (e) {
  try { __EL__.dialog.showErrorBox("WANDRU", "FATAL " + e); } catch (_) {}
}
/*__WANDRU_MAPHOOK_END__*/

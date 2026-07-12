;/*__WANDRU_MAPHOOK__*/
/* Впрыскивается в main-процесс index.js СРАЗУ после создания главного окна Wand.
   Плейсхолдеры подставляет MapFrameHook.Patch: __WIN__ = окно, __EL__ = require("electron"),
   __DUMP__ = JSON-строка map-translator (исполняется в map-фрейме).
   Канал в лог инсталлера: __EL__.net POST на 127.0.0.1:39271 (без fs/require, loopback без CORS).
   Парные маркеры __WANDRU_MAPHOOK__ ... _END - для strip-then-reinject (обновляемость). */
try {
  var MF = null, CACHE = {};
  /* Пер-карта офлайн-словари {slug:{en:ru}} - подставляет MapFrameHook.Patch (__MAPS__).
     Мгновенный офлайн-перевод POI/событий: seed переводчика по slug из URL, MT только на остаток. */
  var MAPS = __MAPS__;
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

  function _mt(q, cb) {
    if (CACHE[q] !== undefined) { cb(CACHE[q]); return; }
    if (!MTON) { cb(null); return; }           // онлайн-перевод карт выключен -> только офлайн
    Q.push([q, cb]); pump();
  }
  function pump() {
    while (inflight < MAXC && Q.length) {
      var it = Q.shift(); inflight++;
      (function (q, cb) {
        one(q, function (r) { inflight--; CACHE[q] = r; cb(r); setTimeout(pump, 120); }); // ~throttle
      })(it[0], it[1]);
    }
  }
  // Google в кулдауне (после 429) -> сразу MyMemory; иначе Google, при 429/ошибке -> MyMemory.
  function one(q, cb) {
    if (Date.now() < gCoolUntil) { mymemory(q, cb); return; }
    google(q, function (r, code) {
      if (code === 429) { gCoolUntil = Date.now() + 60000; mymemory(q, cb); return; }
      if (r != null) { cb(r); return; }
      mymemory(q, cb);
    });
  }
  function google(q, cb) {
    try {
      var rq = __EL__.net.request("https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=ru&dt=t&q=" + encodeURIComponent(q)), data = "", code = 0;
      rq.on("response", function (res) {
        code = res.statusCode || 0;
        res.on("data", function (c) { data += c.toString(); });
        res.on("end", function () {
          var out = null;
          try { var j = JSON.parse(data), i; out = ""; for (i = 0; i < j[0].length; i++) out += j[0][i][0]; }
          catch (e) { out = null; }
          cb(out, code);
        });
      });
      rq.on("error", function () { cb(null, 0); });
      rq.end();
    } catch (e) { cb(null, 0); }
  }
  function mymemory(q, cb) {
    try {
      var rq = __EL__.net.request("https://api.mymemory.translated.net/get?langpair=en%7Cru&q=" + encodeURIComponent(q)), data = "";
      rq.on("response", function (res) {
        res.on("data", function (c) { data += c.toString(); });
        res.on("end", function () {
          var out = null;
          try { var t = JSON.parse(data).responseData.translatedText; if (t && t.toUpperCase().indexOf("MYMEMORY WARNING") < 0) out = t; }
          catch (e) { out = null; }
          cb(out);
        });
      });
      rq.on("error", function () { cb(null); });
      rq.end();
    } catch (e) { cb(null); }
  }

  _p("STAGE1 main hook installed");

  /* При навигации подфрейма на wand.com/maps - впрыск переводчика в фрейм (обход SOP). */
  __WIN__.webContents.on("did-frame-navigate", function (ev, u, c, t, mn, pi, ri) {
    _p("NAV " + (mn ? "main" : "sub") + " " + u);
    if (!mn && /wand\.com\/maps\//.test(u)) {
      _p("STAGE2 map matched: " + u);
      var sl = (u.match(/\/maps\/([^\/?]+)/) || [])[1] || "";  // slug карты из URL (/maps/<slug>/)
      // Общий UI-хром (_common, все карты) + per-map словарь; per-map перекрывает.
      var dict = {}, kk, _c = MAPS["_common"] || {}, _m = MAPS[sl] || {};
      for (kk in _c) dict[kk] = _c[kk];
      for (kk in _m) dict[kk] = _m[kk];
      try {
        MF = __EL__.webFrameMain.fromId(pi, ri);
        MF.executeJavaScript(__DUMP__)
          .then(function () {
            _p("STAGE3 inject resolved; dict " + sl + "=" + Object.keys(dict).length);
            /* seed пер-карта словарём: мгновенный офлайн на POI, без MT-шторма */
            try { MF.executeJavaScript("window.__wandruApply&&window.__wandruApply(" + JSON.stringify(dict) + ")"); } catch (_) {}
          })
          .catch(function (e) { _p("STAGE3 inject ERR " + e); });
      } catch (e) { _p("STAGE2 throw " + e); }
    }
  });

  /* console-message из фрейма: MTREQ = батч на перевод; DUMP = строка в лог. */
  __WIN__.webContents.on("console-message", function (ev, l, ms) {
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
          if (--pend === 0 && MF) {
            try { MF.executeJavaScript("window.__wandruApply&&window.__wandruApply(" + JSON.stringify(res) + ")"); } catch (_) {}
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
} catch (e) {
  try { __EL__.dialog.showErrorBox("WANDRU", "FATAL " + e); } catch (_) {}
}
/*__WANDRU_MAPHOOK_END__*/

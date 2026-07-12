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

  /* Строка в лог инсталлера (MapDiagServer). */
  function _p(l) {
    try {
      var r = __EL__.net.request({ method: "POST", url: "http://127.0.0.1:39271/" });
      r.on("error", function () {});
      r.write(typeof l == "string" ? l : String(l));
      r.end();
    } catch (_) {}
  }

  /* Перевод одной строки en->ru через Google gtx (main-процесс, без CSP). Кэш in-mem. */
  function _mt(q, cb) {
    if (CACHE[q] !== undefined) { cb(CACHE[q]); return; }
    try {
      var rq = __EL__.net.request("https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=ru&dt=t&q=" + encodeURIComponent(q)), data = "";
      rq.on("response", function (res) {
        res.on("data", function (c) { data += c.toString(); });
        res.on("end", function () {
          var out = null;
          try { var j = JSON.parse(data), i; out = ""; for (i = 0; i < j[0].length; i++) out += j[0][i][0]; }
          catch (e) { out = null; }
          CACHE[q] = out; cb(out);
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
      var dict = MAPS[sl] || {};
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

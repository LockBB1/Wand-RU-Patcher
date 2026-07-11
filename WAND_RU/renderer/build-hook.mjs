// Генерирует cheat-hook.js — self-contained IIFE для инъекции в renderer Wand.
// Источник правды: cheat-translator.js (офлайн-движок) + cheat-online.js (MT-добор) + cheat-dictionary.json.
// Хук monkey-патчит window.fetch/XHR на /v3/games/{id}/trainer -> перевод -> пересборка ответа.
// Онлайн-режим (MT) включается флагом TranslateCheatsOnline в %AppData%/WandRuInstaller/settings.json;
// требует Node в renderer (nodeIntegration:true у главного окна Wand). Запуск: node build-hook.mjs.

import { readFileSync, writeFileSync, readdirSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const strip = (f) => readFileSync(join(here, f), "utf8").replace(/^export\s+/gm, "").trim();
const engineBody = strip("cheat-translator.js");
const onlineBody = strip("cheat-online.js");
const dictMin = JSON.stringify(JSON.parse(readFileSync(join(here, "cheat-dictionary.json"), "utf8")));
// Per-game точные словари (games/<gameId>.json -> {title, names}): в хук идут только names.
const games = {};
for (const f of readdirSync(join(here, "games")).filter((f) => f.endsWith(".json")).sort()) {
  games[f.replace(/\.json$/, "")] = JSON.parse(readFileSync(join(here, "games", f), "utf8")).names;
}
const gamesMin = JSON.stringify(games);
const indent = (s) => s.split("\n").map((l) => "  " + l).join("\n");

const hook = `/* Wand RU — перехват и перевод имён читов в renderer. Сгенерировано build-hook.mjs, не править вручную. */
(function () {
  "use strict";
  if (typeof window === "undefined" || window.__wandRuCheatHook) return;
  window.__wandRuCheatHook = true;

  var DICT = ${dictMin};
  var GAMES = ${gamesMin};
  var TARGET_KEYS_ONLINE = new Set(["name", "displayName", "label"]);

${indent(engineBody)}

${indent(onlineBody)}

  var TRAINER = /\\/v3\\/games\\/(\\d+)\\/trainer/;
  // Точный per-game словарь по gameId из URL (приоритет над движком).
  function exactFor(url) {
    var m = TRAINER.exec(url || "");
    return (m && GAMES[m[1]]) || null;
  }

  // --- Node-доступ (nodeIntegration:true у главного окна) для настроек/кэша/MT. Нет Node -> офлайн. ---
  var NODE = (typeof require === "function") ? require : null;
  function nodeDeps() {
    if (!NODE) return null;
    try {
      var fs = NODE("fs"), https = NODE("https"), p = NODE("path");
      var base = (typeof process !== "undefined" && process.env && process.env.APPDATA) || "";
      if (!base) return null;
      var dir = p.join(base, "WandRuInstaller");
      return { fs: fs, https: https, settings: p.join(dir, "settings.json"), cache: p.join(dir, "cheat-cache.json") };
    } catch (e) { return null; }
  }
  // Настройки онлайн-режима из settings.json: включён ли + провайдер (auto/google/mymemory).
  function onlineSettings(d) {
    try {
      var s = JSON.parse(d.fs.readFileSync(d.settings, "utf8")) || {};
      return {
        online: s.TranslateCheatsOnline === true,
        provider: (typeof s.OnlineProvider === "string" ? s.OnlineProvider : "auto").toLowerCase()
      };
    } catch (e) { return { online: false, provider: "auto" }; }
  }
  function loadCache(d) {
    try { return JSON.parse(d.fs.readFileSync(d.cache, "utf8")) || {}; } catch (e) { return {}; }
  }
  function saveCache(d, cache) {
    try { d.fs.writeFileSync(d.cache, JSON.stringify(cache), "utf8"); } catch (e) { /* не критично */ }
  }
  function httpsGetter(d) {
    return function (url) {
      return new Promise(function (resolve, reject) {
        var req = d.https.get(url, { timeout: 6000 }, function (r) {
          var body = ""; r.setEncoding("utf8");
          r.on("data", function (c) { body += c; });
          r.on("end", function () { resolve(body); });
        });
        req.on("error", reject);
        req.on("timeout", function () { req.destroy(new Error("timeout")); });
      });
    };
  }
  function withTimeout(promise, ms, fallback) {
    return new Promise(function (resolve) {
      var done = false;
      var t = setTimeout(function () { if (!done) { done = true; resolve(fallback); } }, ms);
      var fin = function (v) { if (!done) { done = true; clearTimeout(t); resolve(v); } };
      promise.then(fin, function () { fin(fallback); });
    });
  }

  // Офлайн-перевод (синхронно) — для XHR-пути. exact — точный per-game map (или null).
  function translateOffline(text, exact) {
    try { return JSON.stringify(translateCheats(JSON.parse(text), DICT, exact)); } catch (e) { return null; }
  }
  // Офлайн + (опц.) онлайн-MT добор — для fetch-пути. Возвращает Promise<string|null>.
  function translateAsync(text, exact) {
    var data;
    try { data = JSON.parse(text); } catch (e) { return Promise.resolve(null); }
    var offline = translateCheats(data, DICT, exact);
    var d = nodeDeps();
    var conf = d ? onlineSettings(d) : null;
    if (!d || !conf.online) return Promise.resolve(JSON.stringify(offline));
    try {
      var cache = loadCache(d);
      return withTimeout(
        runOnline(offline, { cache: cache, httpsGet: httpsGetter(d), targetKeys: TARGET_KEYS_ONLINE, provider: conf.provider }),
        8000, offline
      ).then(function (result) { saveCache(d, cache); return JSON.stringify(result); },
             function () { return JSON.stringify(offline); });
    } catch (e) { return Promise.resolve(JSON.stringify(offline)); }
  }

  // --- fetch (офлайн + онлайн-MT) ---
  var _fetch = window.fetch;
  if (typeof _fetch === "function") {
    window.fetch = function (input, init) {
      var url = typeof input === "string" ? input : (input && input.url) || "";
      var p = _fetch.apply(this, arguments);
      if (!TRAINER.test(url)) return p;
      return p.then(function (res) {
        try {
          if (!res || !res.ok) return res;
          var ct = (res.headers && res.headers.get("content-type")) || "";
          if (ct.indexOf("json") < 0) return res;
          return res.clone().text().then(function (text) {
            return translateAsync(text, exactFor(url)).then(function (t) {
              if (t == null) return res;
              var headers = new Headers(res.headers);
              headers.delete("content-length");
              return new Response(t, { status: res.status, statusText: res.statusText, headers: headers });
            });
          }).catch(function () { return res; });
        } catch (e) { return res; }
      });
    };
  }

  // --- XMLHttpRequest (офлайн-only, best-effort фолбэк; онлайн-MT требует async) ---
  var XP = window.XMLHttpRequest && window.XMLHttpRequest.prototype;
  if (XP && XP.open && XP.send) {
    var _open = XP.open, _send = XP.send;
    XP.open = function (method, url) { this.__wandRuUrl = url; return _open.apply(this, arguments); };
    XP.send = function () {
      var xhr = this;
      if (xhr.__wandRuUrl && TRAINER.test(xhr.__wandRuUrl)) {
        xhr.addEventListener("readystatechange", function () {
          if (xhr.readyState === 4 && xhr.status >= 200 && xhr.status < 300) {
            try {
              var t = translateOffline(xhr.responseText, exactFor(xhr.__wandRuUrl));
              if (t != null) {
                Object.defineProperty(xhr, "responseText", { value: t, configurable: true });
                Object.defineProperty(xhr, "response", { value: t, configurable: true });
              }
            } catch (e) { /* оставить как есть */ }
          }
        });
      }
      return _send.apply(this, arguments);
    };
  }
})();
`;

writeFileSync(join(here, "cheat-hook.js"), hook, "utf8");
console.log("cheat-hook.js written (" + hook.length + " bytes)");

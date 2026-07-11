// Генерирует cheat-hook.js — self-contained IIFE для инъекции в renderer Wand.
// Источник правды: cheat-translator.js (движок) + cheat-dictionary.json (словарь).
// Хук monkey-патчит window.fetch/XHR на /v3/games/{id}/trainer -> translateCheats -> пересборка ответа.
// Запуск: node build-hook.mjs  (пишет cheat-hook.js рядом).

import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const engine = readFileSync(join(here, "cheat-translator.js"), "utf8");
const dict = readFileSync(join(here, "cheat-dictionary.json"), "utf8");

// Тело движка без ESM-экспортов (в IIFE — обычные функции).
const engineBody = engine
  .replace(/^export\s+/gm, "")
  .trim();

const dictMin = JSON.stringify(JSON.parse(dict)); // компактный словарь

const hook = `/* Wand RU — перехват и перевод имён читов в renderer. Сгенерировано build-hook.mjs, не править вручную. */
(function () {
  "use strict";
  if (typeof window === "undefined" || window.__wandRuCheatHook) return;
  window.__wandRuCheatHook = true;

  var DICT = ${dictMin};

${engineBody.split("\n").map((l) => "  " + l).join("\n")}

  var TRAINER = /\\/v3\\/games\\/\\d+\\/trainer/;
  function translateBody(text) {
    try {
      var data = JSON.parse(text);
      return JSON.stringify(translateCheats(data, DICT));
    } catch (e) {
      return null; // не JSON или сбой — не трогаем
    }
  }

  // --- fetch ---
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
            var t = translateBody(text);
            if (t == null) return res;
            var headers = new Headers(res.headers);
            headers.delete("content-length");
            return new Response(t, { status: res.status, statusText: res.statusText, headers: headers });
          }).catch(function () { return res; });
        } catch (e) { return res; }
      });
    };
  }

  // --- XMLHttpRequest (best-effort фолбэк) ---
  var XP = window.XMLHttpRequest && window.XMLHttpRequest.prototype;
  if (XP && XP.open && XP.send) {
    var _open = XP.open, _send = XP.send;
    XP.open = function (method, url) {
      this.__wandRuUrl = url;
      return _open.apply(this, arguments);
    };
    XP.send = function () {
      var xhr = this;
      if (xhr.__wandRuUrl && TRAINER.test(xhr.__wandRuUrl)) {
        xhr.addEventListener("readystatechange", function () {
          if (xhr.readyState === 4 && xhr.status >= 200 && xhr.status < 300) {
            try {
              var t = translateBody(xhr.responseText);
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

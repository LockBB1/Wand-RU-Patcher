import { test } from "node:test";
import assert from "node:assert/strict";
import {
  collectUntranslated,
  applyMap,
  googleUrl,
  parseGoogle,
  myMemoryUrl,
  parseMyMemory,
  translateOne,
  runOnline,
} from "./cheat-online.js";

const KEYS = new Set(["name", "displayName", "label"]);
const sample = () => ({
  trainer: { blueprint: { cheats: [
    { uuid: "a", name: "Бесконечное здоровье", category: "player" }, // уже переведено офлайн
    { uuid: "b", name: "Unlimited Widgets", category: "player" },     // осталось англ.
    { uuid: "c", name: "Quantum Flux", category: "world" },           // осталось англ.
  ] } },
});

test("collectUntranslated: только целевые поля с латиницей", () => {
  const got = [...collectUntranslated(sample(), KEYS)];
  assert.deepEqual(got.sort(), ["Quantum Flux", "Unlimited Widgets"]);
});

test("collectUntranslated: category/uuid игнорируются", () => {
  const got = [...collectUntranslated(sample(), KEYS)];
  assert.ok(!got.includes("player"));
  assert.ok(!got.includes("a"));
});

test("myMemoryUrl: langpair en|ru, q экранирован", () => {
  const u = myMemoryUrl("No Reload");
  assert.match(u, /q=No%20Reload/);
  assert.match(u, /langpair=en%7Cru/);
});

test("parseMyMemory: извлекает перевод / фильтрует ошибки", () => {
  assert.equal(parseMyMemory('{"responseData":{"translatedText":"Без перезарядки"}}'), "Без перезарядки");
  assert.equal(parseMyMemory('{"responseData":{"translatedText":"MYMEMORY WARNING: LIMIT"}}'), null);
  assert.equal(parseMyMemory("not json"), null);
  assert.equal(parseMyMemory('{"responseData":{"translatedText":""}}'), null);
});

test("googleUrl: gtx endpoint, q экранирован", () => {
  const u = googleUrl("No Reload");
  assert.match(u, /translate\.googleapis\.com/);
  assert.match(u, /client=gtx/);
  assert.match(u, /sl=en&tl=ru/);
  assert.match(u, /q=No%20Reload/);
});

test("parseGoogle: извлекает и склеивает сегменты / мусор -> null", () => {
  assert.equal(parseGoogle('[[["Без перезарядки","No Reload",null]],null,"en"]'), "Без перезарядки");
  assert.equal(parseGoogle('[[["Часть 1. ","p1"],["Часть 2.","p2"]]]'), "Часть 1. Часть 2.");
  assert.equal(parseGoogle('{"not":"gtx"}'), null);
  assert.equal(parseGoogle("not json"), null);
  assert.equal(parseGoogle("[[]]"), null);
});

test("translateOne: сбой обоих провайдеров -> null (не бросает)", async () => {
  const res = await translateOne("X", () => Promise.reject(new Error("net down")));
  assert.equal(res, null);
});

test("translateOne: Google первым; при его сбое - фолбэк на MyMemory", async () => {
  const seen = [];
  const httpsGet = (url) => {
    seen.push(url);
    if (url.includes("googleapis")) return Promise.reject(new Error("429"));
    return Promise.resolve('{"responseData":{"translatedText":"Без перезарядки"}}');
  };
  assert.equal(await translateOne("No Reload", httpsGet), "Без перезарядки");
  assert.match(seen[0], /googleapis/);
  assert.match(seen[1], /mymemory/);
});

test("translateOne: provider=google не зовёт MyMemory даже при сбое", async () => {
  const seen = [];
  const httpsGet = (url) => { seen.push(url); return Promise.reject(new Error("429")); };
  assert.equal(await translateOne("No Reload", httpsGet, "google"), null);
  assert.equal(seen.length, 1);
  assert.match(seen[0], /googleapis/);
});

test("translateOne: provider=mymemory не зовёт Google", async () => {
  const seen = [];
  const httpsGet = (url) => {
    seen.push(url);
    return Promise.resolve('{"responseData":{"translatedText":"Без перезарядки"}}');
  };
  assert.equal(await translateOne("No Reload", httpsGet, "mymemory"), "Без перезарядки");
  assert.equal(seen.length, 1);
  assert.match(seen[0], /mymemory/);
});

test("translateOne: эхо-ответ (перевод == оригинал) не считается переводом", async () => {
  const httpsGet = (url) =>
    Promise.resolve(url.includes("googleapis")
      ? '[[["No Reload","No Reload"]]]'
      : '{"responseData":{"translatedText":"no reload"}}');
  assert.equal(await translateOne("No Reload", httpsGet), null);
});

test("runOnline: мисс зовёт MT (Google), пишет кэш, применяет", async () => {
  const cache = {};
  const seen = [];
  const httpsGet = (url) => {
    seen.push(url);
    const q = decodeURIComponent(url.match(/q=([^&]+)/)[1]).replace(/\+/g, " ");
    const map = { "Unlimited Widgets": "Бесконечные виджеты", "Quantum Flux": "Квантовый поток" };
    return Promise.resolve(JSON.stringify([[[map[q], q]]])); // gtx-формат
  };
  const out = await runOnline(sample(), { cache, httpsGet, targetKeys: KEYS });
  const cheats = out.trainer.blueprint.cheats;
  assert.equal(cheats[1].name, "Бесконечные виджеты");
  assert.equal(cheats[2].name, "Квантовый поток");
  assert.equal(cheats[0].name, "Бесконечное здоровье"); // не тронуто
  assert.equal(seen.length, 2); // 2 мисса, Google ответил с первого раза - фолбэк не дёргался
  assert.equal(cache["unlimited widgets"], "Бесконечные виджеты"); // закэшировано
});

test("runOnline: кэш-хит минует MT", async () => {
  const cache = { "unlimited widgets": "Бесконечные виджеты", "quantum flux": "Квантовый поток" };
  let calls = 0;
  const httpsGet = () => { calls++; return Promise.resolve("{}"); };
  const out = await runOnline(sample(), { cache, httpsGet, targetKeys: KEYS });
  assert.equal(calls, 0); // всё из кэша
  assert.equal(out.trainer.blueprint.cheats[1].name, "Бесконечные виджеты");
});

test("runOnline: MT-сбой -> строка остаётся англ. (фолбэк)", async () => {
  const cache = {};
  const out = await runOnline(sample(), { cache, httpsGet: () => Promise.reject(new Error("down")), targetKeys: KEYS });
  assert.equal(out.trainer.blueprint.cheats[1].name, "Unlimited Widgets"); // не сломано
  assert.deepEqual(cache, {}); // ничего не закэшировано
});

test("runOnline+offline: офлайн перевёл целиком -> MT не зовётся, применяется офлайн", async () => {
  const offline = (s) => (s === "Set Prestige" ? "Задать престиж" : s);
  let calls = 0;
  const out = await runOnline(
    { cheats: [{ name: "Set Prestige" }] },
    { cache: {}, httpsGet: () => { calls++; return Promise.resolve("{}"); }, targetKeys: KEYS, offline }
  );
  assert.equal(out.cheats[0].name, "Задать престиж");
  assert.equal(calls, 0);
});

test("runOnline+offline: полуфабрикат -> MT зовётся с ОРИГИНАЛОМ, не с миксом", async () => {
  const offline = (s) => (s === "Set Frobnicate" ? "Задать Frobnicate" : s); // латиница осталась
  const seen = [];
  const httpsGet = (url) => {
    seen.push(decodeURIComponent(url.match(/q=([^&]+)/)[1]).replace(/\+/g, " "));
    return Promise.resolve('[[["Задать фробникацию","Set Frobnicate"]]]');
  };
  const cache = {};
  const out = await runOnline(
    { cheats: [{ name: "Set Frobnicate" }] },
    { cache, httpsGet, targetKeys: KEYS, offline }
  );
  assert.deepEqual(seen, ["Set Frobnicate"]); // в MT ушёл оригинал
  assert.equal(out.cheats[0].name, "Задать фробникацию");
  assert.equal(cache["set frobnicate"], "Задать фробникацию"); // кэш по оригиналу
});

test("runOnline+offline: MT упал -> применяется хотя бы частичный офлайн", async () => {
  const offline = (s) => (s === "Set Frobnicate" ? "Задать Frobnicate" : s);
  const out = await runOnline(
    { cheats: [{ name: "Set Frobnicate" }] },
    { cache: {}, httpsGet: () => Promise.reject(new Error("down")), targetKeys: KEYS, offline }
  );
  assert.equal(out.cheats[0].name, "Задать Frobnicate"); // лучше, чем чистый англ.
});

test("applyMap: вход не мутирует", () => {
  const s = sample();
  const before = JSON.stringify(s);
  applyMap(s, { "Unlimited Widgets": "X" }, KEYS);
  assert.equal(JSON.stringify(s), before);
});

import { test } from "node:test";
import assert from "node:assert/strict";
import {
  collectUntranslated,
  applyMap,
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

test("translateOne: сбой httpsGet -> null (не бросает)", async () => {
  const res = await translateOne("X", () => Promise.reject(new Error("net down")));
  assert.equal(res, null);
});

test("runOnline: мисс зовёт MT, пишет кэш, применяет", async () => {
  const cache = {};
  const seen = [];
  const httpsGet = (url) => {
    seen.push(url);
    const q = decodeURIComponent(url.match(/q=([^&]+)/)[1]);
    const map = { "Unlimited Widgets": "Бесконечные виджеты", "Quantum Flux": "Квантовый поток" };
    return Promise.resolve(JSON.stringify({ responseData: { translatedText: map[q] } }));
  };
  const out = await runOnline(sample(), { cache, httpsGet, targetKeys: KEYS });
  const cheats = out.trainer.blueprint.cheats;
  assert.equal(cheats[1].name, "Бесконечные виджеты");
  assert.equal(cheats[2].name, "Квантовый поток");
  assert.equal(cheats[0].name, "Бесконечное здоровье"); // не тронуто
  assert.equal(seen.length, 2); // только 2 мисса
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

test("applyMap: вход не мутирует", () => {
  const s = sample();
  const before = JSON.stringify(s);
  applyMap(s, { "Unlimited Widgets": "X" }, KEYS);
  assert.equal(JSON.stringify(s), before);
});

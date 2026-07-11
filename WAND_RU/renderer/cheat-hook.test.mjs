import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const hookSrc = readFileSync(join(here, "cheat-hook.js"), "utf8");

// Загружает хук в свежий фейковый window, возвращает его (window.fetch уже пропатчен).
function install(fakeFetch) {
  const window = { fetch: fakeFetch };
  new Function("window", "Headers", "Response", hookSrc)(window, Headers, Response);
  return window;
}
const jsonRes = (obj) =>
  new Response(JSON.stringify(obj), { status: 200, headers: { "content-type": "application/json" } });
const trainer = () => ({
  trainer: { blueprint: { cheats: [
    { uuid: "a", name: "Unlimited Health", category: "player", target: "x" },
    { uuid: "b", name: "Damage Multiplier", category: "weapons", target: "y" },
  ] } },
});

test("fetch: trainer response is translated", async () => {
  const w = install(() => Promise.resolve(jsonRes(trainer())));
  const res = await w.fetch("https://api.wemod.com/v3/games/40171/trainer?v=3");
  const data = await res.json();
  assert.equal(data.trainer.blueprint.cheats[0].name, "Бесконечное здоровье");
  assert.equal(data.trainer.blueprint.cheats[0].category, "player"); // slug не тронут (ключ локали)
  assert.equal(data.trainer.blueprint.cheats[1].name, "Множитель урона");
  assert.equal(data.trainer.blueprint.cheats[0].uuid, "a"); // non-target intact
});

test("fetch: non-trainer URL passes through untouched", async () => {
  const original = trainer();
  const w = install(() => Promise.resolve(jsonRes(original)));
  const res = await w.fetch("https://api.wemod.com/v3/account");
  const data = await res.json();
  assert.equal(data.trainer.blueprint.cheats[0].name, "Unlimited Health"); // unchanged
});

test("fetch: non-JSON trainer response returns original", async () => {
  const w = install(() =>
    Promise.resolve(new Response("<html>oops</html>", { status: 200, headers: { "content-type": "text/html" } }))
  );
  const res = await w.fetch("https://api.wemod.com/v3/games/1/trainer");
  assert.equal(await res.text(), "<html>oops</html>");
});

test("fetch: error status passes through", async () => {
  const w = install(() => Promise.resolve(new Response("nope", { status: 500 })));
  const res = await w.fetch("https://api.wemod.com/v3/games/1/trainer");
  assert.equal(res.status, 500);
  assert.equal(await res.text(), "nope"); // не тронут
});

test("hook: idempotent (second install is a no-op)", () => {
  const window = { fetch: () => Promise.resolve(jsonRes(trainer())) };
  new Function("window", "Headers", "Response", hookSrc)(window, Headers, Response);
  const firstPatched = window.fetch;
  new Function("window", "Headers", "Response", hookSrc)(window, Headers, Response);
  assert.equal(window.fetch, firstPatched); // не переоборачивает
});

test("hook: no window is a safe no-op (does not throw)", () => {
  assert.doesNotThrow(() => new Function("window", "Headers", "Response", hookSrc)(undefined, Headers, Response));
});

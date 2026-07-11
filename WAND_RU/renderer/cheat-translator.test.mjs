import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { translateText, translateCheats } from "./cheat-translator.js";

const here = dirname(fileURLToPath(import.meta.url));
const dict = JSON.parse(readFileSync(join(here, "cheat-dictionary.json"), "utf8"));
const sample = JSON.parse(readFileSync(join(here, "__fixtures__", "cheats-sample.json"), "utf8"));

// --- translateText: idioms ---
test("idiom: exact phrase translated with correct grammar", () => {
  assert.equal(translateText("God Mode", dict), "Режим бога");
  assert.equal(translateText("Unlimited Health", dict), "Бесконечное здоровье");
  assert.equal(translateText("No Reload", dict), "Без перезарядки");
});

test("idiom: case-insensitive and trimmed", () => {
  assert.equal(translateText("  GOD MODE  ", dict), "Режим бога");
});

// --- translateText: patterns ---
test("pattern: prefix rule with known tail word", () => {
  assert.equal(translateText("Set Money", dict), "Задать деньги");
  assert.equal(translateText("Freeze Gold", dict), "Заморозить золото");
});

test("pattern: unknown tail word kept as-is in template", () => {
  // 'Widgets' not in words → tail untranslated, template still applies
  assert.equal(translateText("Add Widgets", dict), "Добавить Widgets");
});

// --- translateText: fallback + idempotency ---
test("fallback: unknown phrase returned unchanged", () => {
  assert.equal(translateText("Quantum Flux Stabilizer", dict), "Quantum Flux Stabilizer");
});

test("idempotent: already-Cyrillic string untouched", () => {
  assert.equal(translateText("Режим бога", dict), "Режим бога");
  assert.equal(translateText("Бесконечное здоровье", dict), "Бесконечное здоровье");
});

test("preservation: placeholders and numbers untouched", () => {
  assert.equal(translateText("$x_day", dict), "$x_day");
  assert.equal(translateText("100", dict), "100");
});

// --- translateCheats: walker ---
test("walker: translates cheat name fields recursively", () => {
  const out = translateCheats(sample, dict);
  const cheats = out.trainerMeta.schema.cheats;
  assert.equal(cheats[0].name, "Режим бога");
  assert.equal(cheats[1].name, "Бесконечное здоровье");
  assert.equal(cheats[2].name, "Задать деньги");
  assert.equal(cheats[3].name, "Без перезарядки");
});

test("walker: translates description and category", () => {
  const out = translateCheats(sample, dict);
  const c0 = out.trainerMeta.schema.cheats[0];
  // description has no rule → unchanged (fallback); category slug unknown → unchanged
  assert.equal(c0.description, "Ignore incoming damage.");
  assert.equal(c0.category, "player");
});

test("walker: translates args.options[].label, keeps value", () => {
  const out = translateCheats(sample, dict);
  const opts = out.trainerMeta.schema.cheats[3].args.options;
  assert.equal(opts[0].label, "Режим бога");
  assert.equal(opts[0].value, "erdtree_greatshield");
  assert.equal(opts[1].label, "Скорострельность");
  assert.equal(opts[1].value, "rivers_of_blood");
});

test("walker: non-target fields untouched", () => {
  const out = translateCheats(sample, dict);
  const c2 = out.trainerMeta.schema.cheats[2];
  assert.equal(c2.uuid, "number-money");
  assert.equal(c2.target, "player_money");
  assert.equal(c2.type, "number");
  assert.equal(c2.args.max, 9999999);
});

test("walker: does not mutate input", () => {
  const before = JSON.stringify(sample);
  translateCheats(sample, dict);
  assert.equal(JSON.stringify(sample), before);
});

test("walker: idempotent on its own output", () => {
  const once = translateCheats(sample, dict);
  const twice = translateCheats(once, dict);
  assert.deepEqual(twice, once);
});

test("walker: unknown cheat name kept (fallback)", () => {
  const out = translateCheats(sample, dict);
  assert.equal(out.trainerMeta.schema.cheats[4].name, "Quantum Flux Stabilizer");
});

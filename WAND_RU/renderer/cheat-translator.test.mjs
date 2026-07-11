import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { translateText, translateCategory, translateCheats } from "./cheat-translator.js";

const here = dirname(fileURLToPath(import.meta.url));
const load = (...p) => JSON.parse(readFileSync(join(here, ...p), "utf8"));
const dict = load("cheat-dictionary.json");
const sample = load("__fixtures__", "cheats-sample.json");
const real = load("__fixtures__", "trainer-real.json");

// --- translateText: idioms ---
test("idiom: exact phrase, correct grammar", () => {
  assert.equal(translateText("God Mode", dict), "Режим бога");
  assert.equal(translateText("No Reload", dict), "Без перезарядки");
  assert.equal(translateText("Unlimited Horse Health", dict), "Бесконечное здоровье лошади");
});

test("idiom: case-insensitive and trimmed", () => {
  assert.equal(translateText("  GOD MODE  ", dict), "Режим бога");
});

// --- translateText: gender agreement (adj patterns) ---
test("pattern adj: adjective agrees with tail-word gender", () => {
  assert.equal(translateText("Unlimited Health", dict), "Бесконечное здоровье"); // n
  assert.equal(translateText("Unlimited Energy", dict), "Бесконечная энергия"); // f
  assert.equal(translateText("Unlimited Focus", dict), "Бесконечная концентрация"); // f
  assert.equal(translateText("Unlimited Ammo", dict), "Бесконечные патроны"); // pl
  assert.equal(translateText("Unlimited Items", dict), "Бесконечные предметы"); // pl
  assert.equal(translateText("Infinite Oxygen", dict), "Бесконечный кислород"); // m
});

test("pattern adj: unknown tail gender defaults masculine, tail kept", () => {
  assert.equal(translateText("Unlimited Widgets", dict), "Бесконечный Widgets");
});

// --- translateText: template patterns ---
test("pattern template: prefix rule with known tail word", () => {
  assert.equal(translateText("Set Money", dict), "Задать деньги");
  assert.equal(translateText("Freeze Gold", dict), "Заморозить золото");
});

test("pattern template: unknown tail kept as-is", () => {
  assert.equal(translateText("Add Widgets", dict), "Добавить Widgets");
});

test("idiom beats pattern (No Bounty)", () => {
  assert.equal(translateText("No Bounty", dict), "Без розыска");
});

// --- translateText: fallback + idempotency + preservation ---
test("fallback: unknown phrase unchanged", () => {
  assert.equal(translateText("Quantum Flux Stabilizer", dict), "Quantum Flux Stabilizer");
});

test("idempotent: already-Cyrillic untouched", () => {
  assert.equal(translateText("Режим бога", dict), "Режим бога");
  assert.equal(translateText("Бесконечное здоровье", dict), "Бесконечное здоровье");
});

test("preservation: placeholders and numbers untouched", () => {
  assert.equal(translateText("$x_day", dict), "$x_day");
  assert.equal(translateText("100", dict), "100");
});

// --- translateCategory ---
test("category: slug mapped to display name", () => {
  assert.equal(translateCategory("player", dict), "Игрок");
  assert.equal(translateCategory("weapons", dict), "Оружие");
  assert.equal(translateCategory("vehicles", dict), "Транспорт");
});

test("category: unknown slug unchanged", () => {
  assert.equal(translateCategory("quantum", dict), "quantum");
});

// --- translateCheats: synthetic fixture ---
test("walker: translates cheat names", () => {
  const out = translateCheats(sample, dict);
  const c = out.trainerMeta.schema.cheats;
  assert.equal(c[0].name, "Режим бога");
  assert.equal(c[1].name, "Бесконечное здоровье");
  assert.equal(c[2].name, "Задать деньги");
  assert.equal(c[3].name, "Без перезарядки");
});

test("walker: translates category slug", () => {
  const out = translateCheats(sample, dict);
  const c = out.trainerMeta.schema.cheats;
  assert.equal(c[0].category, "Игрок");
  assert.equal(c[2].category, "Инвентарь");
});

test("walker: translates options[].label, keeps value", () => {
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

test("walker: idempotent on own output", () => {
  const once = translateCheats(sample, dict);
  assert.deepEqual(translateCheats(once, dict), once);
});

test("walker: unknown cheat name kept (fallback)", () => {
  const out = translateCheats(sample, dict);
  assert.equal(out.trainerMeta.schema.cheats[4].name, "Quantum Flux Stabilizer");
});

// --- translateCheats: REAL response shape (trainer.blueprint.cheats) ---
test("real: translates names on real endpoint shape", () => {
  const out = translateCheats(real, dict);
  const names = out.trainer.blueprint.cheats.map((c) => c.name);
  assert.ok(names.includes("Бесконечное здоровье"));
  assert.ok(names.includes("Бесконечная энергия"));
  assert.ok(names.includes("Без перезарядки"));
  assert.ok(names.includes("Задать деньги"));
  assert.ok(names.includes("Бесконечное здоровье лошади"));
  assert.ok(names.includes("Бесконечная энергия лошади"));
});

test("real: translates categories, keeps uuid/target", () => {
  const out = translateCheats(real, dict);
  const cheats = out.trainer.blueprint.cheats;
  const cats = new Set(cheats.map((c) => c.category));
  assert.ok(cats.has("Игрок"));
  assert.ok(cats.has("Оружие"));
  assert.ok(cats.has("Транспорт"));
  assert.equal(cheats[0].uuid, "ffsrr6gj");
  assert.equal(cheats[0].target, "unlimited_health");
});

test("real: idempotent", () => {
  const once = translateCheats(real, dict);
  assert.deepEqual(translateCheats(once, dict), once);
});

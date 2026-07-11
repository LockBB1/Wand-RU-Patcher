import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { translateText, translateCheats } from "./cheat-translator.js";

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

// --- translateCheats: synthetic fixture ---
test("walker: translates cheat names", () => {
  const out = translateCheats(sample, dict);
  const c = out.trainerMeta.schema.cheats;
  assert.equal(c[0].name, "Режим бога");
  assert.equal(c[1].name, "Бесконечное здоровье");
  assert.equal(c[2].name, "Задать деньги");
  assert.equal(c[3].name, "Без перезарядки");
});

test("walker: leaves category slug untouched (locale key)", () => {
  const out = translateCheats(sample, dict);
  const c = out.trainerMeta.schema.cheats;
  assert.equal(c[0].category, "player");
  assert.equal(c[2].category, "inventory");
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

test("real: leaves category slugs untouched, keeps uuid/target", () => {
  const out = translateCheats(real, dict);
  const cheats = out.trainer.blueprint.cheats;
  const cats = new Set(cheats.map((c) => c.category));
  assert.ok(cats.has("player")); // slug, переводит локаль Фазы 1
  assert.ok(cats.has("weapons"));
  assert.ok(cats.has("vehicles"));
  assert.equal(cheats[0].uuid, "ffsrr6gj");
  assert.equal(cheats[0].target, "unlimited_health");
});

test("real: idempotent", () => {
  const once = translateCheats(real, dict);
  assert.deepEqual(translateCheats(once, dict), once);
});

// --- v2: cases, suffixes, compounds, nesting, capitalization ---
test("case: gen for No, acc for Set/Edit/Refill", () => {
  assert.equal(translateText("No Recoil", dict), "Без отдачи"); // gen
  assert.equal(translateText("No Fall Damage", dict), "Без урона от падения"); // gen phrase
  assert.equal(translateText("Set Jump Height", dict), "Задать высоту прыжка"); // acc fem
  assert.equal(translateText("Refill Health", dict), "Пополнить здоровье");
});

test("suffix: X Multiplier -> Множитель X(gen)", () => {
  assert.equal(translateText("Damage Multiplier", dict), "Множитель урона");
  assert.equal(translateText("XP Multiplier", dict), "Множитель опыта");
  assert.equal(translateText("Defense Multiplier", dict), "Множитель защиты");
});

test("prefix beats suffix: Set X Multiplier = Set(X Multiplier)", () => {
  assert.equal(translateText("Set Experience Multiplier", dict), "Задать Множитель опыта");
});

test("compound: split on / and & keeps separators, translates parts", () => {
  assert.equal(translateText("God Mode/Ignore Hits", dict), "Режим бога/Игнорировать удары");
  assert.equal(
    translateText("Super Damage / One-Hit Kills", dict),
    "Супер урон / Убийство с одного удара"
  );
});

test("nesting: prefix over prefix (Edit Max Health)", () => {
  assert.equal(translateText("Edit Max Health", dict), "Изменить макс. здоровье");
});

test("capitalization: standalone tail-word capitalized", () => {
  assert.equal(translateText("Game Speed", dict), "Скорость игры");
});

test("adj agreement over adverbs list (XP masculine)", () => {
  assert.equal(translateText("Unlimited XP", dict), "Бесконечный опыт");
});

// --- v3: generic suffixes, bracket tags, weapon ammo ---
test("suffix: generic noun suffixes with known tail", () => {
  assert.equal(translateText("Reload Speed", dict), "Скорость перезарядки");
  assert.equal(translateText("Sprint Speed", dict), "Скорость спринта");
  assert.equal(translateText("Item Duration", dict), "Длительность предмета");
});

test("suffix: weapon ammo", () => {
  assert.equal(translateText("Shotgun Ammo", dict), "Патроны дробовика");
  assert.equal(translateText("Pistol Ammo", dict), "Патроны пистолета");
});

test("bracket: tag translated, inner capitalized", () => {
  assert.equal(translateText("[Spaceship] Unlimited Health", dict), "[Корабль] Бесконечное здоровье");
});

test("prefix all: All X -> Все X", () => {
  assert.equal(translateText("Get All Skills", dict), "Получить Все навыки");
});

test("idiom: noclip / invincible", () => {
  assert.equal(translateText("No Clip", dict), "Сквозь стены");
  assert.equal(translateText("Invincible", dict), "Неуязвимость");
});

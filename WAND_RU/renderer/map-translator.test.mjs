// Reproduce-харнесс для map-translator.mjs (впрыскиваемый в map-фрейм IIFE - нет обычного harness).
// Грузим файл as-is через vm, даём фейковый DOM. Ключ: сеттер nodeValue ВСЕГДА шлёт characterData-
// мутацию observer'у (как реальный браузер, без сверки равенства) - так воспроизводится вечный цикл
// HIGH-5: identity-бренд пишет тот же текст -> мутация -> MO -> tr() -> снова запись. pump() гоняет
// таймеры+мутации до сходимости; невыход из цикла = maxIter = красный тест.
import { test } from 'node:test';
import assert from 'node:assert';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const SRC = readFileSync(new URL('./map-translator.mjs', import.meta.url), 'utf8');

function load(textValues, seed) {
  const timers = [], mutations = [], observers = [];
  const env = { timers, mutations, observers, writes: 0 };

  const body = { nodeType: 1, tagName: 'BODY', className: '', parentNode: null };
  const texts = textValues.map((v) => {
    const node = { nodeType: 3, _v: v, parentNode: body };
    Object.defineProperty(node, 'nodeValue', {
      configurable: true,
      get() { return this._v; },
      set(x) {
        this._v = x; env.writes++;
        for (const o of observers) mutations.push({ obs: o, rec: { type: 'characterData', target: node, addedNodes: [] } });
      }
    });
    return node;
  });
  body.__texts = texts;
  env.texts = texts;

  const document = {
    body,
    addEventListener() {},
    createTreeWalker(root) {
      const list = root && root.__texts ? root.__texts.slice() : (root && root.nodeType === 3 ? [root] : []);
      let i = 0;
      return { nextNode() { return i < list.length ? list[i++] : null; } };
    }
  };
  class MutationObserver { constructor(cb) { this.cb = cb; } observe() { observers.push(this); } disconnect() {} }

  const sandbox = {
    window: { __WANDRU_SEED: seed || null },
    document,
    location: { href: 'https://wand.com/maps/red-dead-redemption-2/' },
    console: { log() {} },
    btoa: (s) => Buffer.from(s, 'binary').toString('base64'),
    unescape, encodeURIComponent,
    NodeFilter: { SHOW_TEXT: 4 },
    MutationObserver,
    setTimeout: (fn) => { timers.push(fn); return timers.length; },
    setInterval: () => 0, // report-таймер не гоняем
    clearTimeout() {}, Date
  };
  vm.createContext(sandbox);
  vm.runInContext(SRC, sandbox);
  return env;
}

// Гоняем таймеры + доставку мутаций до пустых очередей (сходимость) либо cap (цикл).
function pump(env, maxIter = 2000) {
  let iter = 0;
  while ((env.timers.length || env.mutations.length) && iter < maxIter) {
    iter++;
    if (env.timers.length) env.timers.shift()();
    if (env.mutations.length) {
      const batch = env.mutations.splice(0), byObs = new Map();
      for (const m of batch) { if (!byObs.has(m.obs)) byObs.set(m.obs, []); byObs.get(m.obs).push(m.rec); }
      for (const [obs, recs] of byObs) obs.cb(recs);
    }
  }
  return iter;
}

test('identity brand: сходится, текст не переписан, 0 записей', () => {
  const env = load(['Wand']);
  const iter = pump(env);
  assert.ok(iter < 2000, 'вечный цикл: pump не сошёлся (identity пишет тот же текст -> MO -> снова)');
  assert.equal(env.texts[0].nodeValue, 'Wand');
  assert.equal(env.writes, 0, 'identity-бренд не должен трогать nodeValue');
});

test('real dict hit: переведён, ровно одна запись, сходится', () => {
  const env = load(['Reward']);
  const iter = pump(env);
  assert.ok(iter < 2000);
  assert.equal(env.texts[0].nodeValue, 'Награда');
  assert.equal(env.writes, 1);
});

test('микс identity + перевод: сходится без цикла', () => {
  const env = load(['Wand', 'Reward', 'Red Dead Redemption 2', 'Continue']);
  const iter = pump(env);
  assert.ok(iter < 2000, 'смешанный набор не должен зацикливаться');
  assert.equal(env.texts[0].nodeValue, 'Wand');
  assert.equal(env.texts[1].nodeValue, 'Награда');
  assert.equal(env.texts[2].nodeValue, 'Red Dead Redemption 2');
  assert.equal(env.texts[3].nodeValue, 'Продолжить');
});

test('seed-словарь с identity-парой не зацикливается', () => {
  const env = load(['Fort Wallace'], { 'Fort Wallace': 'Fort Wallace' });
  const iter = pump(env);
  assert.ok(iter < 2000, 'identity из seed тоже не должен крутить MO');
  assert.equal(env.writes, 0);
});

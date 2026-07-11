// CheatTranslator - движок перевода имён читов (Фаза 2, engine-first).
// Чистые функции, без зависимостей. Тестируется node:test.
// Рантайм-цель: renderer Wand (HAR: GET api.wemod.com/v3/games/{id}/trainer, Sec-Fetch-* => fetch/XHR).
// Словарь (idioms/words/categories/prefixes/suffixes) передаётся аргументом; данные - cheat-dictionary.json.
//
// Приоритет резолва имени: compound-split(/ , & and) -> для каждой части:
//   idiom(полная фраза) > word/phrase(полная фраза) > suffix(Multiplier/Rate) > prefix(Unlimited/No/Set/...)
//   > passthrough(как есть).
// Слово словаря: { n: им.падеж, g: род m|f|n|pl, gen?: род.падеж(«Без X»,«Множитель X»),
//   acc?: вин.падеж(«Задать X»,«Изменить X»; нужен для жен. 1-го скл.: энергия->энергию) }.

const TARGET_KEYS = new Set(["name", "displayName", "label"]);
const CYRILLIC = /[А-Яа-яЁё]/;
// Разбиение compound-имён: сохраняем разделители, чтобы собрать обратно (God Mode / Ignore Hits).
const SPLIT = /(\s*\/\s*|\s*,\s*|\s*&\s*|\s+and\s+)/i;

function hasKey(o, k) {
  return o && Object.prototype.hasOwnProperty.call(o, k);
}

const MAX_DEPTH = 5; // страховка от глубокой взаимной рекурсии resolveName<->resolveTail

// Хвост внутри фразы пишется со строчной («Задать режим полёта», не «Задать Режим полёта»):
// идиомы в словаре с заглавной (для самостоятельного показа), в хвосте её опускаем.
// Финальную заглавную ставит translateText первому кириллическому символу всей фразы.
function decapFirst(s) {
  const i = s.search(/[А-ЯЁ]/);
  return i < 0 ? s : s.slice(0, i) + s.charAt(i).toLocaleLowerCase("ru") + s.slice(i + 1);
}

// Хвост (существительное после префикса) -> {nom, gen, acc, gender}. Не нашли - рекурсия в resolveName.
function resolveTail(tail, dict, depth) {
  const t = tail.trim();
  const key = t.toLowerCase();
  if (hasKey(dict.idioms, key)) {
    return { nom: decapFirst(dict.idioms[key]), gen: null, acc: null, gender: undefined };
  }
  if (hasKey(dict.words, key)) {
    const w = dict.words[key];
    return { nom: w.n, gen: w.gen || null, acc: w.acc || null, gender: w.g };
  }
  if (depth < MAX_DEPTH) {
    const inner = resolveName(t, dict, depth + 1); // вложенные префиксы: "Max HP", "X Multiplier"
    if (inner !== t) return { nom: decapFirst(inner), gen: null, acc: null, gender: undefined };
  }
  return { nom: t, gen: null, acc: null, gender: undefined }; // англ. как есть
}

// Одна часть имени (без compound-разделителей) -> строка перевода.
function resolveName(seg, dict, depth = 0) {
  const s = seg.trim();
  if (s === "" || CYRILLIC.test(s)) return seg;
  const key = s.toLowerCase();

  if (hasKey(dict.idioms, key)) return dict.idioms[key];
  if (hasKey(dict.words, key)) return dict.words[key].n;

  // Тег в скобках: "[Spaceship] Unlimited Health" -> "[Корабль] Бесконечное здоровье".
  const br = s.match(/^\[([^\]]+)\]\s*(.+)$/);
  if (br && depth < MAX_DEPTH) {
    return "[" + resolveName(br[1], dict, depth + 1) + "] " + resolveName(br[2], dict, depth + 1);
  }

  // Prefix-паттерны (раньше suffix: "Set X Multiplier" = Set(X Multiplier), а не (Set X)Multiplier).
  for (const p of dict.prefixes || []) {
    const m = s.match(new RegExp(p.match, "i"));
    if (!m) continue;
    const r = resolveTail(m[1] !== undefined ? m[1] : "", dict, depth);
    if (p.adj) return p.adj[r.gender || "m"] + " " + r.nom; // прилагательное по роду
    if (p.form === "gen") return p.template.replace("{0}", r.gen || r.nom); // родительный
    if (p.form === "acc") return p.template.replace("{0}", r.acc || r.nom); // винительный
    return p.template.replace("{0}", r.nom); // им.падеж
  }

  // Suffix-паттерны (X Multiplier, X Rate): хвост в родительном.
  for (const suf of dict.suffixes || []) {
    const m = s.match(new RegExp(suf.match, "i"));
    if (!m) continue;
    const r = resolveTail(m[1] !== undefined ? m[1] : "", dict, depth);
    return suf.template.replace("{0}", r.gen || r.nom);
  }
  return seg; // ничего не подошло - оригинал
}

// Полное имя чита -> перевод. Идемпотентно (кириллица не трогается). Compound через разделители.
// exact (опц.) - точный per-game map «имя -> перевод» (renderer/games/*.json), приоритет над движком.
export function translateText(str, dict, exact) {
  if (typeof str !== "string") return str;
  if (str.trim() === "" || CYRILLIC.test(str)) return str;
  if (exact && hasKey(exact, str.trim())) return exact[str.trim()];
  const res = str
    .split(SPLIT)
    .map((seg) => (SPLIT.test(seg) ? seg : resolveName(seg, dict)))
    .join("");
  // Капитализация первой КИРИЛЛИЧЕСКОЙ буквы (переведённый хвост строчный; латиницу/плейсхолдеры не трогаем).
  const i = res.search(/[а-яёА-ЯЁ]/);
  return i < 0 ? res : res.slice(0, i) + res.charAt(i).toLocaleUpperCase("ru") + res.slice(i + 1);
}

// Рекурсивный walker: новый объект, вход не мутирует. Переводит только имена (TARGET_KEYS).
// ВАЖНО: category НЕ трогаем - это slug для ключа локали (trainer_cheats_list.category_<slug>),
// его переводит локаль Фазы 1. Перевод slug ломает lookup ключа.
export function translateCheats(node, dict, exact) {
  if (Array.isArray(node)) return node.map((n) => translateCheats(n, dict, exact));
  if (node && typeof node === "object") {
    const out = {};
    for (const [k, v] of Object.entries(node)) {
      if (typeof v === "string" && TARGET_KEYS.has(k)) out[k] = translateText(v, dict, exact);
      else out[k] = translateCheats(v, dict, exact);
    }
    return out;
  }
  return node;
}

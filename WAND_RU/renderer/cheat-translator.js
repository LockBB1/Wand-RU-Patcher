// CheatTranslator — движок перевода имён читов (Фаза 2, engine-first).
// Чистые функции, без зависимостей. Тестируется node:test.
// Рантайм-цель: renderer Wand (подтверждено HAR: GET api.wemod.com/v3/games/{id}/trainer,
// заголовки Sec-Fetch-* => fetch/XHR из renderer). Splice — отдельная фаза.
// Словарь (idioms/words/categories/patterns) передаётся аргументом; данные — cheat-dictionary.json.

// Ключи cheat-объектов с UI-видимым текстом (имя чита). Category — отдельным map (translateCategory).
const TARGET_KEYS = new Set(["name", "displayName", "label"]);

const CYRILLIC = /[А-Яа-яЁё]/;

// Внутр.: резолв строки в {text, gender}. gender нужен pattern'ам с согласованием прилагательного.
function resolve(str, dict) {
  if (typeof str !== "string") return { text: str, gender: undefined };
  const trimmed = str.trim();
  if (trimmed === "" || CYRILLIC.test(trimmed)) return { text: str, gender: undefined };

  const key = trimmed.toLowerCase();

  if (dict.idioms && Object.prototype.hasOwnProperty.call(dict.idioms, key)) {
    return { text: dict.idioms[key], gender: undefined };
  }
  if (dict.words && Object.prototype.hasOwnProperty.call(dict.words, key)) {
    const w = dict.words[key];
    return { text: w.t, gender: w.g };
  }
  for (const p of dict.patterns || []) {
    const m = trimmed.match(new RegExp(p.match, "i"));
    if (!m) continue;
    const tail = m[1] !== undefined ? m[1] : "";
    const r = resolve(tail, dict); // рекурсия: хвост через словарь
    if (p.adj) {
      const adj = p.adj[r.gender || "m"]; // неизвестный род → муж. по умолчанию
      return { text: adj + " " + r.text, gender: undefined };
    }
    return { text: p.template.replace("{0}", r.text), gender: undefined };
  }
  return { text: str, gender: undefined }; // ничего не подошло — оригинал целиком
}

// Одна строка: idiom > word > pattern > passthrough. Идемпотентно (уже кириллица → как есть).
export function translateText(str, dict) {
  return resolve(str, dict).text;
}

// Slug категории ("player") → имя ("Игрок"). Неизвестный slug → как есть.
export function translateCategory(slug, dict) {
  if (typeof slug !== "string") return slug;
  const cats = dict.categories || {};
  const key = slug.trim().toLowerCase();
  return Object.prototype.hasOwnProperty.call(cats, key) ? cats[key] : slug;
}

// Рекурсивный walker: новый объект, вход не мутирует. Переводит имена (TARGET_KEYS) и category;
// числа/uuid/target/type/value/hotkeys/плейсхолдеры целы.
export function translateCheats(node, dict) {
  if (Array.isArray(node)) {
    return node.map((n) => translateCheats(n, dict));
  }
  if (node && typeof node === "object") {
    const out = {};
    for (const [k, v] of Object.entries(node)) {
      if (typeof v === "string" && TARGET_KEYS.has(k)) {
        out[k] = translateText(v, dict);
      } else if (typeof v === "string" && k === "category") {
        out[k] = translateCategory(v, dict);
      } else {
        out[k] = translateCheats(v, dict);
      }
    }
    return out;
  }
  return node; // примитивы как есть
}

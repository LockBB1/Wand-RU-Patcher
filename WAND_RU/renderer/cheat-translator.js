// CheatTranslator — движок перевода имён читов (Фаза 2, engine-first).
// Чистые функции, без зависимостей. Тестируется node:test.
// Рантайм-цель: renderer Wand. Splice (перехват сети + оборачивание в IIFE) — отдельная фаза.
// Словарь (idioms/words/patterns) передаётся аргументом; данные — в cheat-dictionary.json.

// Ключи cheat-объектов с UI-видимым текстом. Только их и переводим.
const TARGET_KEYS = new Set(["name", "displayName", "description", "category", "label"]);

const CYRILLIC = /[А-Яа-яЁё]/;

// Одна строка: idiom → word → pattern → фолбэк «как есть». Идемпотентно.
export function translateText(str, dict) {
  if (typeof str !== "string") return str;
  const trimmed = str.trim();
  if (trimmed === "") return str;
  if (CYRILLIC.test(trimmed)) return str; // уже переведено — не трогаем

  const key = trimmed.toLowerCase();

  if (dict.idioms && Object.prototype.hasOwnProperty.call(dict.idioms, key)) {
    return dict.idioms[key];
  }
  if (dict.words && Object.prototype.hasOwnProperty.call(dict.words, key)) {
    return dict.words[key];
  }
  for (const p of dict.patterns || []) {
    const m = trimmed.match(new RegExp(p.match, "i"));
    if (m) {
      const tail = m[1] !== undefined ? m[1] : "";
      const tailRu = translateText(tail, dict); // рекурсия: хвост через словарь
      return p.template.replace("{0}", tailRu);
    }
  }
  return str; // ничего не подошло — оригинал целиком
}

// Рекурсивный walker: возвращает новый объект, вход не мутирует.
// Переводит текст только на целевых ключах; числа/uuid/target/value/плейсхолдеры целы.
export function translateCheats(node, dict) {
  if (Array.isArray(node)) {
    return node.map((n) => translateCheats(n, dict));
  }
  if (node && typeof node === "object") {
    const out = {};
    for (const [k, v] of Object.entries(node)) {
      out[k] =
        TARGET_KEYS.has(k) && typeof v === "string"
          ? translateText(v, dict)
          : translateCheats(v, dict);
    }
    return out;
  }
  return node; // примитивы как есть
}

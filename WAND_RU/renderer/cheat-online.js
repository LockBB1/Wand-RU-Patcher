// Онлайн-MT добор для непокрытого офлайн-словарём (Фаза 3, путь A). Чистая логика с инъекцией
// зависимостей (fs/https приходят снаружи — в хуке из require, в тестах моки). Провайдеры без ключа,
// цепочкой: Google (gtx, качественнее) → MyMemory (фолбэк). Кэш en->ru. Всё опционально:
// сбой/нет сети → офлайн-результат не трогаем.

const LATIN = /[A-Za-z]/;

// Уникальные англ. строки на целевых полях, которым нужен MT (офлайн не смог — осталась латиница).
export function collectUntranslated(node, targetKeys, out = new Set()) {
  if (Array.isArray(node)) {
    for (const n of node) collectUntranslated(n, targetKeys, out);
  } else if (node && typeof node === "object") {
    for (const [k, v] of Object.entries(node)) {
      if (typeof v === "string") {
        if (targetKeys.has(k) && LATIN.test(v)) out.add(v);
      } else collectUntranslated(v, targetKeys, out);
    }
  }
  return out;
}

// Применить map (оригинал->перевод) к целевым полям. Новый объект, вход не мутирует.
export function applyMap(node, map, targetKeys) {
  if (Array.isArray(node)) return node.map((n) => applyMap(n, map, targetKeys));
  if (node && typeof node === "object") {
    const out = {};
    for (const [k, v] of Object.entries(node)) {
      if (typeof v === "string") out[k] = targetKeys.has(k) && map[v] ? map[v] : v;
      else out[k] = applyMap(v, map, targetKeys);
    }
    return out;
  }
  return node;
}

export function googleUrl(text) {
  return "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=ru&dt=t&q=" +
    encodeURIComponent(text);
}

// Тело ответа gtx ([[["перевод","оригинал",...],...],...]) -> перевод или null.
// Эхо (Google вернул исходный текст) считаем неудачей — не засорять кэш.
export function parseGoogle(body) {
  try {
    const j = JSON.parse(body);
    if (!Array.isArray(j) || !Array.isArray(j[0])) return null;
    const t = j[0].map((p) => (Array.isArray(p) ? p[0] : "")).filter(Boolean).join("");
    if (!t) return null;
    return t;
  } catch {
    return null;
  }
}

export function myMemoryUrl(text) {
  return "https://api.mymemory.translated.net/get?q=" + encodeURIComponent(text) + "&langpair=en%7Cru";
}

// Тело ответа MyMemory -> перевод или null.
export function parseMyMemory(body) {
  try {
    const j = JSON.parse(body);
    const t = j && j.responseData && j.responseData.translatedText;
    // MyMemory при ошибке/лимите кладёт англ.-текст ошибки в translatedText — фильтруем очевидное.
    if (typeof t !== "string" || !t) return null;
    if (/MYMEMORY WARNING|QUERY LENGTH LIMIT|INVALID/i.test(t)) return null;
    return t;
  } catch {
    return null;
  }
}

// Перевести одну строку. provider: "auto" (Google -> фолбэк MyMemory, default), "google", "mymemory".
// httpsGet: (url) => Promise<body>. Сбой -> null. Эхо-ответ (перевод == оригинал) не считаем переводом.
export async function translateOne(text, httpsGet, provider) {
  const p = provider || "auto";
  const useful = (t) => (t && t.trim().toLowerCase() !== text.trim().toLowerCase() ? t : null);
  if (p !== "mymemory") {
    try {
      const g = useful(parseGoogle(await httpsGet(googleUrl(text))));
      if (g) return g;
    } catch { /* провайдер упал - дальше фолбэк (в auto) */ }
    if (p === "google") return null;
  }
  try {
    return useful(parseMyMemory(await httpsGet(myMemoryUrl(text))));
  } catch {
    return null;
  }
}

// Оркестрация: собрать непокрытое -> кэш-мисс через MT (параллельно) -> обновить кэш -> применить.
// deps: { cache (obj en_lower->ru, мутируется), httpsGet, targetKeys, provider? }. Возвращает новый node.
export async function runOnline(node, deps) {
  const { cache, httpsGet, targetKeys, provider } = deps;
  const all = [...collectUntranslated(node, targetKeys)];
  const misses = all.filter((t) => !(t.toLowerCase() in cache));

  await Promise.all(
    misses.map(async (t) => {
      const ru = await translateOne(t, httpsGet, provider);
      if (ru) cache[t.toLowerCase()] = ru; // кэшируем только успешные
    })
  );

  const map = {};
  for (const t of all) {
    const ru = cache[t.toLowerCase()];
    if (ru) map[t] = ru;
  }
  return applyMap(node, map, deps.targetKeys);
}

// Онлайн-MT добор для непокрытого офлайн-словарём (Фаза 3, путь A). Чистая логика с инъекцией
// зависимостей (fs/https приходят снаружи - в хуке из require, в тестах моки). Провайдеры без ключа,
// цепочкой: Google (gtx, качественнее) → MyMemory (фолбэк). Кэш en->ru. Всё опционально:
// сбой/нет сети → офлайн-результат не трогаем.

const LATIN = /[A-Za-z]/;

// Лимит одновременных MT-запросов: без него 150 имён = 150 параллельных Google -> 429 -> всё валится
// в MyMemory -> её лимит. map-mainhook держит тот же потолок (MAXC=2). Кэш-хиты/офлайн-покрытые слот
// не держат - их fn резолвится сразу, воркер берёт следующий.
export const MT_MAXC = 2;
export async function mapLimited(items, limit, fn) {
  const out = new Array(items.length);
  let i = 0;
  async function worker() {
    while (i < items.length) { const idx = i++; out[idx] = await fn(items[idx], idx); }
  }
  const ws = [];
  for (let w = 0; w < Math.min(limit, items.length); w++) ws.push(worker());
  await Promise.all(ws);
  return out;
}

// Уникальные англ. строки на целевых полях, которым нужен MT (офлайн не смог - осталась латиница).
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
// Эхо (Google вернул исходный текст) считаем неудачей - не засорять кэш.
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
    // MyMemory при ошибке/лимите кладёт англ.-текст ошибки в translatedText - фильтруем очевидное.
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

// Перевод значений i18n.strings (описания/заметки читов). КЛЮЧИ не трогаем - по ним UI ищет
// перевод; переводим только ЗНАЧЕНИЯ. Длинный текст офлайн не тянет - только MT + кэш.
// Строки >1500 символов пропускаем (лимит URL у GET-провайдеров).
export async function translateStrings(map, deps) {
  const { cache, httpsGet, provider } = deps;
  if (!map || typeof map !== "object") return map;
  const out = {};
  await mapLimited(Object.entries(map), MT_MAXC, async ([k, v]) => {
    out[k] = v;
    if (typeof v !== "string" || !LATIN.test(v) || v.length > 1500) return;
    const ck = v.toLowerCase();
    if (ck in cache) { out[k] = cache[ck]; return; }
    const ru = await translateOne(v, httpsGet, provider);
    if (ru) { cache[ck] = ru; out[k] = ru; }
  });
  return out;
}

// Оркестрация. node - ОРИГИНАЛЬНЫЙ (английский) ответ: MT всегда получает исходную строку,
// а не полуфабрикат офлайна («Задать Prestige» ломает MT). deps.offline (опц.) - строковый
// офлайн-переводчик: офлайн справился целиком (нет латиницы) -> MT не нужен; иначе MT по оригиналу,
// при его сбое - хотя бы частичный офлайн. Без deps.offline поведение прежнее (MT по всем миссам).
// deps: { cache (obj en_lower->ru, мутируется), httpsGet, targetKeys, provider?, offline? }.
export async function runOnline(node, deps) {
  const { cache, httpsGet, targetKeys, provider } = deps;
  const offline = deps.offline || ((s) => s);
  const all = [...collectUntranslated(node, targetKeys)]; // исходные англ. строки
  const map = {};
  const misses = [];
  for (const s of all) {
    const off = offline(s);
    if (!LATIN.test(off)) { map[s] = off; continue; } // офлайн перевёл целиком - MT не нужен
    if (s.toLowerCase() in cache) map[s] = cache[s.toLowerCase()];
    else misses.push(s);
  }

  await mapLimited(misses, MT_MAXC, async (s) => {
    const ru = await translateOne(s, httpsGet, provider);
    if (ru) { cache[s.toLowerCase()] = ru; map[s] = ru; } // кэшируем только успешные
    else { const off = offline(s); if (off !== s) map[s] = off; } // MT упал - частичный офлайн
  });

  return applyMap(node, map, targetKeys);
}

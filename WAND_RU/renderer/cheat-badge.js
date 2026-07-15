// Плашка статуса перевода на странице игры Wand. Впрыскивается В renderer через cheat-hook.js
// (build-hook стрипает export). Ставит "Ручной перевод"/"Авто-перевод" в .title-header__title-actions.
// ES5-совместимо (чужой renderer), без зависимостей.

export const MANUAL = "manual";
export const AUTO = "auto";
export const BADGE_CLASS = "__wandru-badge";

// Состояние по gameId: есть точный per-game словарь -> ручной, иначе авто (движок/MT не различаем).
export function badgeState(gameId, games) {
  return gameId != null && games && games[String(gameId)] ? MANUAL : AUTO;
}

// Текст и тултип плашки. Только короткие дефисы «-».
export function badgeLabel(state) {
  return state === MANUAL
    ? { text: "✓ Ручной перевод", title: "Переведено вручную командой WRP" }
    : { text: "Авто-перевод", title: "Автоматический перевод - точность ниже ручного" };
}

// gameId из DOM: href тайтла -> hash -> фолбэк (последний trainer-fetch). null если нигде нет.
export function resolveGameId(doc, fallbackGameId) {
  function pick(s) { var m = /[?&]gameId=(\d+)/.exec(s || ""); return m ? m[1] : null; }
  var a = doc && doc.querySelector ? doc.querySelector(".title-header__title-name") : null;
  var href = a ? (a.getAttribute ? a.getAttribute("href") : a.href) : null;
  var hash = doc && doc.location ? doc.location.hash : "";
  return pick(href) || pick(hash) || (fallbackGameId != null ? String(fallbackGameId) : null);
}

// Вставить/обновить плашку в actionsEl. Guard: если уже есть - обновляем, не дублируем.
export function insertBadge(doc, actionsEl, state) {
  if (!actionsEl) return "noop";
  var cls = BADGE_CLASS + " " + BADGE_CLASS + "--" + state;
  var label = badgeLabel(state);
  var existing = actionsEl.querySelector("." + BADGE_CLASS);
  if (existing) {
    existing.className = cls; existing.textContent = label.text; existing.title = label.title;
    return "updated";
  }
  var el = doc.createElement("div");
  el.className = cls; el.textContent = label.text; el.title = label.title;
  actionsEl.appendChild(el);
  return "inserted";
}

// Один проход: найти шапку -> gameId -> состояние -> вставить. Нет шапки -> noop (best-effort).
export function syncBadge(doc, games, fallbackGameId) {
  var actions = doc && doc.querySelector ? doc.querySelector(".title-header__title-actions") : null;
  if (!actions) return "noop";
  return insertBadge(doc, actions, badgeState(resolveGameId(doc, fallbackGameId), games));
}

// CSS плашки (namespaced). Зелёный акцент manual (как апдейт-баннер #4ADE80), приглушённый серый auto.
var BADGE_CSS =
  "." + BADGE_CLASS + "{display:inline-flex;align-items:center;height:24px;padding:0 10px;margin-left:8px;" +
  "border-radius:12px;font-size:12px;font-weight:600;line-height:1;white-space:nowrap;vertical-align:middle;cursor:default}" +
  "." + BADGE_CLASS + "--" + MANUAL + "{background:rgba(74,222,128,.15);color:#4ADE80;border:1px solid rgba(74,222,128,.4)}" +
  "." + BADGE_CLASS + "--" + AUTO + "{background:rgba(148,163,184,.15);color:#94A3B8;border:1px solid rgba(148,163,184,.3)}";

// Вставить стиль один раз (по id).
export function ensureStyle(doc) {
  if (!doc || !doc.createElement) return;
  if (doc.getElementById && doc.getElementById("__wandru-badge-style")) return;
  var s = doc.createElement("style");
  s.id = "__wandru-badge-style";
  s.textContent = BADGE_CSS;
  (doc.head || doc.documentElement).appendChild(s);
}

// Вооружить: стиль + MutationObserver (дебаунс) -> syncBadge. getFallbackGameId - () => последний trainer gameId.
export function arm(win, games, getFallbackGameId) {
  var doc = win.document;
  ensureStyle(doc);
  var timer = null;
  function run() { timer = null; try { syncBadge(doc, games, getFallbackGameId ? getFallbackGameId() : null); } catch (e) {} }
  function schedule() { if (!timer) timer = win.setTimeout(run, 100); }
  try {
    new win.MutationObserver(schedule).observe(doc.body || doc.documentElement, { childList: true, subtree: true });
  } catch (e) { /* нет observer - только первый проход */ }
  schedule(); // первый проход сразу
}

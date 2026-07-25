// Приветствие AI-помощника (Game Guide). Впрыскивается В renderer через cheat-hook.js
// (build-hook стрипает export). Без зависимостей; ES2015 ок, как cheat-badge.js.
//
// welcomeMessage приходит обычным JSON с api.wemod.com/v3/assistant/session - то есть ДО того,
// как попасть в cross-origin фрейм помощника. Значит правим его нашим renderer-хуком, без Path D.
// Формат (12.42): шапка с названием игры в `**...**`, ниже - кликабельные примеры вопросов
// `[текст](wemod://ask)`. Сами ответы модели уже приходят по-русски (модель зеркалит язык
// вопроса) - переводить их не нужно и незачем.

// Шапка. ceiling: шаблон снят с 12.42; сменит Wand формулировку - шапка останется английской
// (подсказки ниже подменятся всё равно, они важнее). Чинить сверкой при новой версии Wand.
var HEAD = /^I['’]m your AI Game Guide! Here you can ask me anything about (\*\*[\s\S]+?\*\*) like\.\.\./;
var HEAD_RU = "Я твой ИИ-гид по игре! Спрашивай меня о чём угодно про $1, например...";

// Ссылка-подсказка: клик отправляет её текст как вопрос.
var LINK = /\[[^\]]*\]\(wemod:\/\/ask\)/g;

// Наши подсказки вместо серверных. Серверные - английские и per-game, офлайн-словаря на них нет,
// а клик по английской подсказке увёл бы ОТВЕТЫ модели обратно в английский. Наши универсальны
// (годятся любой игре) и работают без сети.
export var PROMPTS = [
  "Как пройти текущую миссию?",
  "Где найти редкие предметы?"
];

// Перевод welcomeMessage. Шапка и подсказки обрабатываются независимо: не совпал шаблон шапки -
// подсказки всё равно подменяем. Лишние серверные подсказки (сверх наших) убираем, а не оставляем
// английскими. Не строка / нет совпадений -> возвращаем как есть.
export function translateWelcome(msg) {
  if (typeof msg !== "string" || !msg) return msg;
  var out = msg.replace(HEAD, HEAD_RU);
  var i = 0;
  out = out.replace(LINK, function () {
    var q = PROMPTS[i++];
    return q ? "[" + q + "](wemod://ask)" : "";
  });
  // Удалённые подсказки оставляют пустые строки - схлопываем, чтобы не было дыры в панели.
  return out.replace(/(\r?\n)(?:[ \t]*\r?\n){2,}/g, "$1$1").replace(/[\s\r\n]+$/, "");
}

// Перевод тела ответа /v3/assistant/session. Нет welcomeMessage -> null (хук оставит оригинал).
export function translateSession(data) {
  if (!data || typeof data !== "object" || typeof data.welcomeMessage !== "string") return null;
  var out = {};
  for (var k in data) out[k] = data[k];
  out.welcomeMessage = translateWelcome(data.welcomeMessage);
  return out;
}

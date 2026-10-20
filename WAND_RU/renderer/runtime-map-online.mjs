// Loaded with the existing cheat-online provider functions, only in owned app pages.
var mapCache = new Map(), mapProviderState = {}, mapHttps;
try { if (typeof require === 'function') mapHttps = require('https'); } catch (_) {}

function mapGet(url) {
  return new Promise(function (resolve, reject) {
    var request, done = false;
    var timer = setTimeout(function () {
      finish(new Error('WRP: map translation timeout'));
      if (request) request.destroy();
    }, 6000);
    function finish(error, body) {
      if (done) return;
      done = true; clearTimeout(timer);
      if (error) reject(error); else resolve(body);
    }
    try {
      request = mapHttps.get(url, function (response) {
        var body = '', bytes = 0;
        response.setEncoding('utf8');
        response.on('data', function (chunk) {
          bytes += chunk.length;
          if (bytes > 1024 * 1024) { finish(new Error('WRP: oversized map translation')); request.destroy(); }
          else body += chunk;
        });
        response.on('error', function () { finish(new Error('WRP: map translation response failed')); });
        response.on('aborted', function () { finish(new Error('WRP: map translation response closed')); });
        response.on('end', function () {
          if (response.statusCode !== 200) {
            var error = new Error('WRP: map translation request failed');
            error.status = response.statusCode; finish(error);
          } else finish(null, body);
        });
      });
      request.on('error', function () { finish(new Error('WRP: map translation network failed')); });
    } catch (_) { finish(new Error('WRP: map translation unavailable')); }
  });
}

window.__wandRuTranslateMap = async function (text) {
  if (!mapHttps || typeof text !== 'string' || text.length < 2 || text.length > 1500) return null;
  if (mapCache.has(text)) return mapCache.get(text);
  var russian = await translateOne(text, mapGet, 'auto', mapProviderState);
  if (russian && /Wand/.test(text)) russian = russian.replace(/Палочк[а-яё]*/gi, 'Wand').replace(/Ванд[а-яё]*/gi, 'Wand');
  if (mapCache.size >= 5000) mapCache.clear();
  mapCache.set(text, russian);
  return russian;
};

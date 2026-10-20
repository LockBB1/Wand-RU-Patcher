// Runtime locale hook; the native helper supplies dictionaries without writing Wand files.
(function () {
  if (window.__wandRuRuntimeReady) return;
  window.__wandRuRuntimeReady = (async function () {
    var configuration = window.__WANDRU_RUNTIME_CONFIG;
    if (!configuration || !configuration.translations) throw new Error('WRP: missing UI dictionary');
    var dictionary = configuration.translations;
    var deadline = Date.now() + 15000;
    var runtime, Repository, service;
    while (Date.now() < deadline) {
      var root = document.getElementById('root');
      var container = root && root.aurelia && root.aurelia.container;
      var chunks = window.webpackChunkWeMod;
      if (container && Array.isArray(chunks)) {
        try {
          if (!runtime) chunks.push([['wandru-runtime-' + Date.now()], {}, function (r) { runtime = r; }]);
          Repository = runtime(82063).x;
          var I18n = runtime(34721).F2;
          if (container.hasResolver(I18n, true)) {
            service = container.get(I18n);
            if (service.getEffectiveLocale()) break;
          }
        } catch (_) { /* Aurelia may still be registering its services. */ }
      }
      await new Promise(function (resolve) { setTimeout(resolve, 100); });
    }
    if (!service || !service.getEffectiveLocale() || !Repository) throw new Error('WRP: locale service contract not ready');
    var original = Repository.prototype.getTemplates;
    Repository.prototype.getTemplates = async function (locale) {
      var source = await original.call(this, locale);
      if (!(source instanceof Map)) throw new Error('WRP: locale repository contract changed');
      var translated = new Map(source);
      for (var key of Object.keys(dictionary)) translated.set(key, dictionary[key]);
      return translated;
    };
    try {
      var before = service.getEffectiveLocale().toString();
      var preferred = service.getPreferredLocale();
      await service.setLocale(preferred ? preferred.toString() : null, 'wandru');
      if (service.getEffectiveLocale().toString() !== before) throw new Error('WRP: unexpected locale change');
      var marker = Object.keys(dictionary)[0];
      if (!marker || typeof service.getValue !== 'function' || service.getValue(marker) !== dictionary[marker])
        throw new Error('WRP: UI dictionary did not apply');
      runtime('aurelia-framework').signalBindings('i18n:translation-locale-changed');
      var result = {ui: true, locale: before, keys: Object.keys(dictionary).length,
        cheats: configuration.translateCheats ? window.__wandRuCheatHook === true : null};
      window.__wandRuRuntimeStatus = result;
      return result;
    } catch (error) {
      Repository.prototype.getTemplates = original;
      throw error;
    }
  })();
  window.__wandRuRuntimeReady.catch(function (error) {
    window.__wandRuRuntimeStatus = {ui: false, error: error.message};
  });
})();

// Register in-memory locale modules before Wand constructs its native services.
(function () {
  if (window.__wandRuRuntimeReady) return;
  window.__wandRuRuntimeReady = (async function () {
    var configuration = window.__WANDRU_RUNTIME_CONFIG;
    if (!configuration || !configuration.translations) throw new Error('WRP: missing UI dictionary');
    var dictionary = configuration.translations;
    var localeTag = 'ru-RU', failure = null, probeKey;
    var wrapped = new WeakSet();
    var decorators = {
      73508: function (exports) {
        if (!Array.isArray(exports.d)) throw new Error('WRP: supported locales contract changed');
        if (!exports.d.includes(localeTag)) exports.d.splice(1, 0, localeTag);
      },
      82063: function (exports) {
        var prototype = exports.x && exports.x.prototype;
        if (!prototype || typeof prototype.getTemplates !== 'function') throw new Error('WRP: locale repository contract changed');
        var original = prototype.getTemplates;
        prototype.getTemplates = async function (locale) {
          if (locale.language !== 'ru') return original.call(this, locale);
          var source = await original.call(this, new Intl.Locale('en-US'));
          if (!(source instanceof Map)) throw new Error('WRP: locale repository contract changed');
          probeKey = Object.keys(dictionary).find(function (key) { return source.has(key) && source.get(key) !== dictionary[key]; });
          var translated = new Map(source);
          for (var key of Object.keys(dictionary)) translated.set(key, dictionary[key]);
          return translated;
        };
      },
      34721: function (exports) {
        var prototype = exports.F2 && exports.F2.prototype;
        if (!prototype || typeof prototype.setLocale !== 'function' || Object.getOwnPropertyDescriptor(prototype, 'locales'))
          throw new Error('WRP: locale metadata contract changed');
        Object.defineProperty(prototype, 'locales', {
          configurable: true,
          set: function (locales) {
            if (Array.isArray(locales)) {
              var russian = locales.find(function (locale) { return locale.tag === localeTag; });
              if (russian) russian.name = configuration.language;
            }
            Object.defineProperty(this, 'locales', {value: locales, writable: true, enumerable: true, configurable: true});
          }
        });
      },
      'resources/value-converters/flags': function (exports) {
        if (!(exports.flags instanceof Map)) throw new Error('WRP: locale flags contract changed');
        exports.flags.set(localeTag, configuration.flag);
      }
    };
    function decorate(chunk) {
      var modules = Array.isArray(chunk) && chunk[1];
      if (!modules || typeof modules !== 'object') return;
      for (var id of Object.keys(decorators)) {
        var original = modules[id];
        if (typeof original !== 'function' || wrapped.has(original)) continue;
        (function (id, original) {
          var factory = function (module, exports, require) {
            var result = original.apply(this, arguments);
            try { decorators[id](module.exports || exports, require); }
            catch (error) { failure = error; }
            return result;
          };
          wrapped.add(factory);
          modules[id] = factory;
        })(id, original);
      }
    }
    var chunks = window.webpackChunkWeMod = window.webpackChunkWeMod || [];
    if (!Array.isArray(chunks)) throw new Error('WRP: bundle loader contract changed');
    function wrapPush(next) {
      return function () {
        for (var chunk of arguments) decorate(chunk);
        return next.apply(this, arguments);
      };
    }
    chunks.forEach(decorate);
    var push = wrapPush(chunks.push);
    // Each wrapper keeps its predecessor: webpack calls the captured old push itself.
    Object.defineProperty(chunks, 'push', {
      configurable: true,
      get: function () { return push; },
      set: function (next) { if (next !== push) push = wrapPush(next); }
    });

    var deadline = Date.now() + 15000;
    var runtime, service, container;
    while (Date.now() < deadline) {
      if (failure) throw failure;
      var root = document.getElementById('root');
      container = root && root.aurelia && root.aurelia.container;
      if (container) {
        try {
          if (!runtime) chunks.push([['wandru-runtime-' + Date.now()], {}, function (r) { runtime = r; }]);
          var I18n = runtime(34721).F2;
          if (container.hasResolver(I18n, true)) {
            service = container.get(I18n);
            if (service.getEffectiveLocale()) break;
          }
        } catch (_) { /* Root services may still be registering. */ }
      }
      await new Promise(function (resolve) { setTimeout(resolve, 100); });
    }
    if (failure) throw failure;
    if (!service || !service.locales.some(function (locale) { return locale.tag === localeTag; }))
      throw new Error('WRP: Russian locale was not registered before bootstrap');
    var flags = runtime('resources/value-converters/flags').flags;
    if (flags.get(localeTag) !== configuration.flag) throw new Error('WRP: Russian flag did not register');
    var overlay = /\/overlay\.html$/i.test(window.location.pathname);
    var Store = runtime(11700).il;
    var action = runtime(overlay ? 55674 : 35256).Kc;
    var store = container.get(Store);
    if (!store || typeof store.dispatch !== 'function' || !store.state || typeof store.state.subscribe !== 'function' ||
        action !== 'ACTION_SET_SETTINGS') throw new Error('WRP: locale persistence contract changed');
    var currentLanguage, stateReady = false;
    store.state.subscribe(function (state) {
      currentLanguage = state && state.settings ? state.settings.language ?? null : null;
      stateReady = true;
    });
    while (!stateReady && Date.now() < deadline) await new Promise(function (resolve) { setTimeout(resolve, 50); });
    if (!stateReady) throw new Error('WRP: locale preferences not ready');
    function preferred() {
      var value = service.getPreferredLocale();
      return value ? value.toString() : null;
    }
    function persist(source) {
      var language = preferred();
      return language === currentLanguage ? Promise.resolve() : store.dispatch(action, {language: language}, source || 'wandru');
    }
    function updateStatus() {
      if (!window.__wandRuRuntimeStatus || !window.__wandRuRuntimeStatus.ui) return;
      window.__wandRuRuntimeStatus.locale = service.getEffectiveLocale().toString();
      window.__wandRuRuntimeStatus.active = service.getEffectiveLocale().language === 'ru';
    }
    // The native account observer is inactive on the sign-in screen.
    service.onLocaleChanged(function (event) {
      updateStatus();
      Promise.resolve(persist(event.source)).catch(function () {
        if (window.__wandRuRuntimeStatus) window.__wandRuRuntimeStatus.persistence = false;
      });
    });
    var fileSystem, pending = false;
    if (!overlay && configuration.activationFile) {
      fileSystem = runtime(73024).promises;
      try {
        var identity = await fileSystem.readFile(configuration.activationFile, 'utf8');
        if (identity !== configuration.activationIdentity) throw new Error('WRP: activation belongs to another installation');
        pending = true;
      } catch (error) { if (error.code !== 'ENOENT') throw error; }
    }
    if (pending) {
      await store.dispatch(action, {language: localeTag}, 'wandru');
      await service.setLocale(localeTag, 'wandru');
    }
    var active = service.getEffectiveLocale().language === 'ru';
    if (active) {
      var applied = probeKey && service.getValue(probeKey) === dictionary[probeKey];
      if (!applied) throw new Error('WRP: UI dictionary did not apply');
    }
    if (pending) {
      // Native dispatch saves after a debounce; keep crash recovery until disk agrees.
      var namespace = runtime(51873).A.storageNamespace;
      if (typeof namespace !== 'string' || !namespace) throw new Error('WRP: locale storage contract changed');
      var saved = false, saveDeadline = Date.now() + 15000;
      while (Date.now() < saveDeadline) {
        var value = localStorage.getItem(namespace + ':globalStore');
        if (value && JSON.parse(value).settings?.language === localeTag) { saved = true; break; }
        await new Promise(function (resolve) { setTimeout(resolve, 100); });
      }
      if (!saved) throw new Error('WRP: Russian preference did not save');
      await fileSystem.unlink(configuration.activationFile);
    }
    runtime('aurelia-framework').signalBindings('i18n:translation-locale-changed');
    var result = {ui: true, locale: service.getEffectiveLocale().toString(), active: active,
      keys: Object.keys(dictionary).length, cheats: configuration.translateCheats ? window.__wandRuCheatHook === true : null};
    window.__wandRuRuntimeStatus = result;
    return result;
  })();
  window.__wandRuRuntimeReady.catch(function (error) {
    window.__wandRuRuntimeStatus = {ui: false, error: error.message};
  });
})();

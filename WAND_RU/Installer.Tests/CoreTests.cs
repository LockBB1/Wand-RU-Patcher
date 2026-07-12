using System.Text.Json;
using WandRuInstaller.Core;
using WandRuInstaller.Models;
using Xunit;

namespace WandRuInstaller.Tests;

public class RuOverridesTests
{
    [Fact]
    public void LoadEmbedded_parses_native_and_translations()
    {
        var o = RuOverrides.LoadEmbedded();
        Assert.Equal("Русский", o.LanguageNative);
        Assert.Equal("Главная", o.Translations["app_header.home"]);
        Assert.True(o.Translations.Count > 10);
    }
}

public class WandLocatorTests
{
    [Fact]
    public void FindAppDirs_sorted_desc_by_version()
    {
        var dirs = WandLocator.FindAppDirs(TestPaths.WandRoot());
        Assert.EndsWith("app-12.37.0", dirs[0]);
        Assert.Contains(dirs, d => d.EndsWith("app-12.36.0"));
    }

    [Fact]
    public void Detect_returns_install_with_root()
    {
        var w = WandLocator.Detect(new[] { TestPaths.WandRoot() });
        Assert.NotNull(w);
        Assert.EndsWith("Wand", w!.RootDir);
        Assert.EndsWith("app-12.37.0", w.SelectedAppDir!);
    }
}

public class LocaleBuilderTests
{
    [Fact]
    public void BuildRuJson_overrides_nested_and_keeps_rest()
    {
        var en = """{ "app_header": { "home": "Home", "maps": "Maps" }, "kept": "X" }""";
        var ov = new RuOverrides { Translations = new() { ["app_header.home"] = "Главная" } };
        using var doc = JsonDocument.Parse(LocaleBuilder.BuildRuJson(en, ov));
        var h = doc.RootElement.GetProperty("app_header");
        Assert.Equal("Главная", h.GetProperty("home").GetString());
        Assert.Equal("Maps", h.GetProperty("maps").GetString());
        Assert.Equal("X", doc.RootElement.GetProperty("kept").GetString());
    }

    [Fact]
    public void BuildRuJson_creates_missing_path()
    {
        var ov = new RuOverrides { Translations = new() { ["a.b.c"] = "Ц" } };
        using var doc = JsonDocument.Parse(LocaleBuilder.BuildRuJson("{}", ov));
        Assert.Equal("Ц", doc.RootElement.GetProperty("a").GetProperty("b").GetProperty("c").GetString());
    }
}

public class JsLocalePatchTests
{
    const string Native = "Русский";
    const string Flag = "data:image/svg+xml;base64,AAAA";

    [Fact]
    public void Adds_ru_to_locale_list_after_enUS()
    {
        var js = "var L=[\"en-US\",\"zh-CN\",\"de-DE\",\"th-TH\"];";
        Assert.Contains("\"en-US\",\"ru-RU\"", JsLocalePatch.Patch(js, Native, Flag));
    }

    [Fact]
    public void Adds_ru_metadata_object()
    {
        var js = "M={en:{name:\"English\",native:\"English\",locale:\"en-US\"},th:{name:\"Thai\",native:\"ไทย\",locale:\"th-TH\"}}";
        Assert.Contains("ru:{name:\"Russian\",native:\"Русский\",locale:\"ru-RU\"}", JsLocalePatch.Patch(js, Native, Flag));
    }

    [Fact]
    public void Is_idempotent()
    {
        var js = "var L=[\"en-US\",\"zh-CN\",\"th-TH\"];";
        var once = JsLocalePatch.Patch(js, Native, Flag);
        Assert.Equal(once, JsLocalePatch.Patch(once, Native, Flag));
    }

    [Fact]
    public void No_anchor_returns_input_unchanged()
    {
        var js = "console.log('hi');";
        Assert.Equal(js, JsLocalePatch.Patch(js, Native, Flag));
    }

    // --- Регресс 0.15.2: жадные якоря портили не-locale-list места в бандлах Wand 12.38 ---

    [Fact]
    public void Does_not_touch_lone_enUS_array()
    {
        // supportedLocales:["en-US"] - не список локалей, ru-RU туда не вставлять.
        var js = "supportedLocales:[\"en-US\"],featureFlagId:\"x\"";
        Assert.Equal(js, JsLocalePatch.Patch(js, Native, Flag));
    }

    [Fact]
    public void Does_not_corrupt_enUS_in_map_entry()
    {
        // new Map([["en-US",f],...]) - ["en-US",f] это [ключ,значение], не список локалей.
        var js = "var y=new Map([[\"en-US\",f],[\"pt-BR\",r]]);";
        var outp = JsLocalePatch.Patch(js, Native, Flag);
        Assert.Contains("[\"en-US\",f]", outp);              // запись цела
        Assert.DoesNotContain("\"en-US\",\"ru-RU\",f", outp); // не разорвана вставкой
    }

    [Fact]
    public void Does_not_corrupt_delete_member_access()
    {
        var js = "var m={};delete m[\"en-US\"];const y=1;";
        Assert.Equal(js, JsLocalePatch.Patch(js, Native, Flag)); // delete m["en-US"] не трогаем
    }

    [Fact]
    public void Does_not_pollute_email_domain_map()
    {
        // Карта e-mail опечаток (все значения ASCII) - не список языков, ["ru",...] не добавлять.
        var js = "f([[\"vcom\",\"com\"],[\"vom\",\"com\"],[\"yk\",\"uk\"]]);";
        Assert.Equal(js, JsLocalePatch.Patch(js, Native, Flag));
    }

    [Fact]
    public void Adds_ru_pair_to_real_language_list()
    {
        // Настоящий список языков (native-имена с не-ASCII) - ru-RU/Русский добавляем.
        var js = "x([[\"pt\",\"português\"],[\"tr\",\"Türkçe\"]]);";
        Assert.Contains("[\"ru\",\"Русский\"]", JsLocalePatch.Patch(js, Native, Flag));
    }

    [Theory]
    [InlineData("var y=new Map([[\"en-US\",\"ru-RU\",f]]);")]   // порча Map-записи
    [InlineData("delete m[\"en-US\",\"ru-RU\"];")]              // порча delete
    [InlineData("supportedLocales:[\"en-US\",\"ru-RU\"],x")]     // одиночный список
    public void HasCorruption_flags_bad_anchor_hits(string js) => Assert.True(JsLocalePatch.HasCorruption(js));

    [Theory]
    [InlineData("var a=[\"en-US\",\"ru-RU\",\"zh-CN\",\"de-DE\"];")] // валидный список локалей
    [InlineData("var a=[\"en-US\",\"zh-CN\"];")]                      // без ru-RU
    public void HasCorruption_passes_valid_locale_list(string js) => Assert.False(JsLocalePatch.HasCorruption(js));

    // --- Регресс: локаль ru течёт в embed-URL wand.com -> /ru/maps + /ru/assistant/embed -> 404 ---

    [Fact]
    public void Neutralizes_map_locale_prefix()
    {
        // Билдер карты: r=lang&&"en"!==lang?`/${lang}`:"" -> с ru даёт /ru/maps (404). Форсим "".
        var js = "function a(t,e){const r=t.language&&\"en\"!==t.language?`/${t.language}`:\"\",i=new URL(`${r}/maps/${t.titleSlug}/${t.mapSlug}`,e);}";
        var outp = JsLocalePatch.NeutralizeEmbedLocale(js);
        Assert.Contains("const r=\"\",i=new URL", outp);      // сегмент занулён
        Assert.DoesNotContain(".language", outp);
    }

    [Fact]
    public void Neutralizes_assistant_locale_segment()
    {
        // Ассистент: `${base}/${a}/assistant/embed` -> с ru даёт /ru/assistant/embed (404). Форсим /en.
        var js = "const a=this.x.getEffectiveLocale().language,i=new URL(`${this.S}/${a}/assistant/embed`);";
        var outp = JsLocalePatch.NeutralizeEmbedLocale(js);
        Assert.Contains("/en/assistant/embed", outp);
        Assert.DoesNotContain("/${a}/assistant/embed", outp);
    }

    [Fact]
    public void Embed_neutralize_is_idempotent()
    {
        var js = "r=e.language&&\"en\"!==e.language?`/${e.language}`:\"\";u=`${b}/${t}/assistant/embed`;";
        var once = JsLocalePatch.NeutralizeEmbedLocale(js);
        Assert.Equal(once, JsLocalePatch.NeutralizeEmbedLocale(once));
    }

    [Fact]
    public void Embed_neutralize_leaves_unrelated_js_untouched()
    {
        var js = "console.log(x.language);const u=`${b}/maps/${id}`;";
        Assert.Equal(js, JsLocalePatch.NeutralizeEmbedLocale(js));
    }
}

public class MapFrameHookTests
{
    // Минифицированный шейп создания главного окна (как в реальном index.js Wand 12.36-12.38).
    const string MainWin = "function z(){Me=new o.BrowserWindow(p.windowOptions);Me.setMenu(null);}";

    [Fact]
    public void Patch_inserts_hook_after_main_window()
    {
        var outp = MapFrameHook.Patch(MainWin);
        Assert.Contains(MapFrameHook.Marker, outp);
        Assert.Contains("Me.webContents.on(\"did-frame-navigate\"", outp);   // захвачено имя окна
        Assert.Contains("o.webFrameMain.fromId", outp);                       // захвачен алиас electron
        Assert.Contains("STAGE1 main hook installed", outp);                  // staged-диагностика
        Assert.Contains("o.net.request", outp);                               // канал - electron net, не fs
        Assert.Contains($"127.0.0.1:{MapDiagServer.Port}", outp);             // -> приёмник инсталлера
        Assert.DoesNotContain("require(\"fs\")", outp);                       // fs/require не в scope точки
    }

    [Fact]
    public void Patch_is_idempotent()
    {
        var once = MapFrameHook.Patch(MainWin);
        Assert.Equal(once, MapFrameHook.Patch(once));
    }

    [Fact]
    public void No_anchor_returns_input_unchanged()
    {
        var js = "const app=require('electron');console.log('no window here');";
        Assert.Equal(js, MapFrameHook.Patch(js));
        Assert.False(MapFrameHook.NeedsPatch(js));
    }

    [Fact]
    public void NeedsPatch_true_when_anchor_present()
    {
        Assert.True(MapFrameHook.NeedsPatch(MainWin));
        Assert.True(MapFrameHook.NeedsPatch(MapFrameHook.Patch(MainWin))); // пере-инжект для актуальности
        Assert.False(MapFrameHook.NeedsPatch("const x=1;console.log('no window');"));
    }

    [Fact]
    public void Repatch_replaces_stale_hook_block()
    {
        // index.js с прошлой версией хука (устаревшее тело между парными маркерами)
        var stale = "Me=new o.BrowserWindow(p.windowOptions);/*__WANDRU_MAPHOOK__*/try{OLD_STALE_BODY}catch(_){}/*__WANDRU_MAPHOOK_END__*/;Me.setMenu(null);";
        var outp = MapFrameHook.Patch(stale);
        Assert.DoesNotContain("OLD_STALE_BODY", outp);                                                    // старьё снято
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(outp, "__WANDRU_MAPHOOK__\\*/").Count); // один блок, не задвоен
        Assert.Contains("did-frame-navigate", outp);                                                       // актуальный хук встал
        Assert.Contains("Me.setMenu(null)", outp);                                                         // оригинальный код цел
    }

    [Fact]
    public void Repatch_removes_legacy_no_end_block()
    {
        // легаси-блок 0.16.3-0.16.8: без END-маркера, заканчивался FATAL-фоллбэком -> тоже снять
        var legacy = "Me=new o.BrowserWindow(p.windowOptions);/*__WANDRU_MAPHOOK__*/try{OLD_LEGACY_BODY}catch(e){try{o.dialog.showErrorBox(\"WANDRU\",\"FATAL \"+e)}catch(_){}};Me.setMenu(null);";
        var outp = MapFrameHook.Patch(legacy);
        Assert.DoesNotContain("OLD_LEGACY_BODY", outp);                                                    // легаси снято
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(outp, "__WANDRU_MAPHOOK__\\*/").Count); // один блок, не задвоен
        Assert.Contains("Me.setMenu(null)", outp);                                                         // оригинал цел
    }

    [Fact]
    public void Patch_captures_alternate_minified_names()
    {
        // Другая версия Wand могла переименовать win/electron -> захват групп должен подстроиться.
        var js = "W=new E.BrowserWindow(Q.windowOptions);";
        var outp = MapFrameHook.Patch(js);
        Assert.Contains("W.webContents.on(\"did-frame-navigate\"", outp);
        Assert.Contains("E.webFrameMain.fromId", outp);
    }
}

public class MapDiagServerTests
{
    // Round-trip: сервер поднимается, принимает POST-строку (как шлёт o.net из Wand), отдаёт в колбэк.
    [Fact]
    public async Task Receives_posted_line()
    {
        var got = new TaskCompletionSource<string>();
        using var srv = new MapDiagServer(line => got.TrySetResult(line));
        Assert.True(srv.Start());
        using (var http = new System.Net.Http.HttpClient())
            await http.PostAsync($"http://127.0.0.1:{MapDiagServer.Port}/",
                new System.Net.Http.StringContent("STAGE1 main hook installed"));
        var completed = await Task.WhenAny(got.Task, Task.Delay(3000));
        Assert.True(completed == got.Task, "приёмник не получил строку за 3с");
        Assert.Equal("STAGE1 main hook installed", got.Task.Result);
    }
}

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
}

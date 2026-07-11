using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using WandRuInstaller.Models;

namespace WandRuInstaller.Core;

public static class LocaleBuilder
{
    public static string BuildRuJson(string enUsJson, RuOverrides overrides)
    {
        var root = JsonNode.Parse(string.IsNullOrWhiteSpace(enUsJson) ? "{}" : enUsJson)!.AsObject();
        foreach (var kv in overrides.Translations)
            SetNested(root, kv.Key.Split('.'), kv.Value);
        // UnsafeRelaxedJsonEscaping: кириллица остаётся литеральной (не \uXXXX) - читаемо и как у оригинальных локалей.
        return root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    static void SetNested(JsonObject obj, string[] path, string value)
    {
        var cursor = obj;
        for (int i = 0; i < path.Length - 1; i++)
        {
            var name = path[i];
            if (cursor[name] is not JsonObject child)
            {
                child = new JsonObject();
                cursor[name] = child;
            }
            cursor = child;
        }
        cursor[path[^1]] = value;
    }
}

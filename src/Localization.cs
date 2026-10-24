using System.Globalization;
using System.Text.Json;

namespace HardcoreProgression;

public sealed class Localization
{
    public Dictionary<string, Dictionary<string, string>> Languages { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static Localization Load(string root)
    {
        var result = new Localization();
        foreach (var path in Directory.GetFiles(Path.Combine(root, "locales"), "*.json"))
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                ?? throw new InvalidDataException($"Empty locale: {Path.GetFileName(path)}");
            result.Languages[Path.GetFileNameWithoutExtension(path)] = values;
        }
        if (!result.Languages.TryGetValue("en", out var english)) throw new InvalidDataException("Missing English fallback locale.");
        foreach (string key in new[] { "Raids", "Unlocked", "Repeat", "ExchangeLocked" })
            if (!english.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException($"Missing English locale key: {key}");
        foreach (var language in result.Languages.Values)
            foreach (var entry in english)
                if (!language.ContainsKey(entry.Key)) language[entry.Key] = entry.Value;
        // Validate formatting before a rejected trade needs to report its reason.
        foreach (var language in result.Languages.Values)
            _ = string.Format(CultureInfo.InvariantCulture, language["ExchangeLocked"], 6);
        return result;
    }

    public string Format(string language, string key, params object[] args)
    {
        var selected = Languages.GetValueOrDefault(language) ?? Languages["en"];
        return string.Format(CultureInfo.InvariantCulture, selected.GetValueOrDefault(key) ?? Languages["en"][key], args);
    }
}

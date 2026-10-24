using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using HardcoreProgression;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;
using Path = System.IO.Path;
using Settings = HardcoreProgression.Settings;

public static class ReleaseVerification
{
    public static void Run(string sptRoot)
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        var root = AppContext.BaseDirectory;
        var config = Settings.Load(root);
        Check(new Metadata().ModDependencies is null, "Standalone mod declares required third-party mods.");
        Check(!config.EnableMedicineLimits && !config.EnableSecureContainerLimit, "Standalone defaults change unrelated rules.");
        var english = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "locales/en.json")))!.AsObject();
        var russian = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "locales/ru.json")))!.AsObject();
        Check(english.Select(entry => entry.Key).Order().SequenceEqual(russian.Select(entry => entry.Key).Order()), "Locale key mismatch.");
        Check(Localization.Load(root).Format("unknown", "ExchangeLocked", 6).Contains("6 completed PMC raids"), "English fallback failed.");
        var temp = Path.Combine(root, "config-cases");
        Directory.CreateDirectory(temp);
        File.Copy(Path.Combine(root, "offers.json"), Path.Combine(temp, "offers.json"), true);
        var original = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "config.json")))!.AsObject();
        foreach (int count in new[] { 0, -1, 1000001 })
        {
            var invalid = original.DeepClone().AsObject(); invalid["RaidsPerExchange"] = count;
            File.WriteAllText(Path.Combine(temp, "config.json"), invalid.ToJsonString());
            try { Settings.Load(temp); throw new Exception("Invalid raid count accepted."); } catch (InvalidDataException) { }
        }
        var disabled = original.DeepClone().AsObject(); disabled["Enabled"] = false;
        File.WriteAllText(Path.Combine(temp, "config.json"), disabled.ToJsonString());
        File.Delete(Path.Combine(temp, "offers.json"));
        Check(!Settings.Load(temp).Enabled, "Disabled mod requires a catalog.");

        // A standalone server has no Hardcore Playthrough template; optional integration must be a no-op.
        var table = Activator.CreateInstance<TemplateTable>();
        typeof(TemplateTable).GetProperty("Profiles")!.SetValue(table, new Dictionary<string, ProfileSides>());
        var log = DispatchProxy.Create<ISptLogger<StarterKitService>, SilentLogger>();
        new StarterKitService(table, new JsonUtil([new SPTarkov.Server.Core.Utils.Json.SptJsonConverterRegistrator()]), log)
            .OnLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        Check(table.Profiles.Count == 0, "Standalone startup created or replaced a profile template.");

        var database = Path.Combine(sptRoot, "SPT_Runtime/SPT_Data/database");
        using var quests = JsonDocument.Parse(File.ReadAllText(Path.Combine(database, "templates/quests.json")));
        using var items = JsonDocument.Parse(File.ReadAllText(Path.Combine(database, "templates/items.json")));
        int existing = config.Offers.Count(offer => quests.RootElement.TryGetProperty(offer.Quest, out _) && items.RootElement.TryGetProperty(offer.Template, out _));
        Check(existing > 0, "Catalog has no vanilla entries.");
        Console.WriteLine($"PASS release configuration: no required mods; English/Russian keys and fallback; invalid config rejection; disabled catalog independence; missing Hardcore profile safely skipped; {existing}/{config.Offers.Count} catalog pairs have vanilla quests/items.");
    }
}

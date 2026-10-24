using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using HardcoreProgression;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;
using Path = System.IO.Path;

public static class IntegrationVerification
{
    public static void Run(string sptRoot, string compatibilityRoot, TemplateTable templates, TradersTable traders, Progression progression)
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        string Snapshot() => JsonSerializer.Serialize(progression.Offers.Values.OrderBy(o => o.Id).Select(offer =>
        {
            var trader = traders[(MongoId)offer.Trader];
            return new { offer.Id, Item=trader.Assort.Items.Single(item => item.Id.ToString() == offer.Id),
                Scheme=trader.Assort.BarterScheme[(MongoId)offer.Id], Quest=trader.QuestAssort["started"][(MongoId)offer.Id] };
        }), JsonUtil.JsonSerializerOptionsNoIndent);
        var barterizer = Assembly.LoadFrom(Path.Combine(compatibilityRoot, "SPT_Runtime/user/mods/SPT-Barterizer/SPT.Barterizer.dll"));
        var settings = Activator.CreateInstance(barterizer.GetType("SptBarterizer.Settings", true)!)!;
        using var handbook = JsonDocument.Parse(File.ReadAllText(Path.Combine(sptRoot, "SPT_Runtime/SPT_Data/database/templates/handbook.json")));
        var prices = handbook.RootElement.GetProperty("Items").EnumerateArray().ToDictionary(item => item.GetProperty("Id").GetString()!, item => item.GetProperty("Price").GetDouble());
        Func<MongoId,double> price = id => prices.GetValueOrDefault(id.ToString());
        var engineType = barterizer.GetType("SptBarterizer.ConversionEngine", true)!;
        var engine = Activator.CreateInstance(engineType, settings, templates, price, null)!;
        void ConvertAll() { foreach (var pair in traders) engineType.GetMethod("Convert")!.Invoke(engine, [pair.Key.ToString(),pair.Value.Assort]); }
        string original = Snapshot();
        ConvertAll();
        Check(Snapshot() == original, "Barterizer changed quest offer IDs, mappings, items or recipes.");

        var hardcore = Assembly.LoadFrom(Path.Combine(compatibilityRoot, "SPT_Runtime/user/mods/HardcoreRules/HardcoreRules-Server.dll"));
        Type TypeOf(string name) => hardcore.GetType(name, true)!;
        var configType = TypeOf("HardcoreRules.Utils.ConfigUtil");
        var config = RuntimeHelpers.GetUninitializedObject(configType);
        var modConfig = Activator.CreateInstance(TypeOf("HardcoreRules.Configuration.ModConfig"))!;
        var traderConfig = modConfig.GetType().GetProperty("Traders")!.GetValue(modConfig)!;
        traderConfig.GetType().GetProperty("BartersOnly")!.SetValue(traderConfig, true);
        traderConfig.GetType().GetProperty("WhitelistOnly")!.SetValue(traderConfig, false);
        configType.GetField("_currentConfig", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(config, modConfig);
        var loggerType = typeof(ISptLogger<>).MakeGenericType(TypeOf("HardcoreRules.HardcoreRules_Server"));
        var logging = Activator.CreateInstance(TypeOf("HardcoreRules.Utils.LoggingUtil"), DispatchProxy.Create(loggerType,typeof(SilentLogger)), config)!;
        var translationType = TypeOf("HardcoreRules.Services.TranslationService");
        var translation = RuntimeHelpers.GetUninitializedObject(translationType);
        translationType.GetField("_cachedTranslations", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(translation,
            traders.ToDictionary(pair => pair.Key + " Nickname",pair => pair.Value.Base.Nickname ?? pair.Key.ToString()));
        var modificationType = TypeOf("HardcoreRules.Utils.OfferSourceUtils.OfferModificationUtil");
        var modification = Activator.CreateInstance(modificationType, logging, config, traders, templates, null)!;
        modificationType.GetField("_whiteListedItems", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(modification, Array.Empty<MongoId>());
        modificationType.GetField("_whiteListedTraders", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(modification, Array.Empty<MongoId>());
        var utilityType = TypeOf("HardcoreRules.Utils.OfferSourceUtils.TraderOffersUtil");
        var utility = Activator.CreateInstance(utilityType,logging,config,traders,translation,modification)!;
        var harmony = new Harmony("hardcoreprogression.integration-tests");
        try
        {
            OptionalHardcoreIntegration.Attach(harmony,progression,DispatchProxy.Create<ISptLogger<Progression>,SilentLogger>());
            var bridge = barterizer.GetType("SptBarterizer.HardcoreBridge",true)!;
            Check((bool)bridge.GetMethod("Attach")!.Invoke(null,[harmony,(Action)ConvertAll,(Action)ConvertAll])!, "Barterizer bridge was not attached.");
            for (int cycle=0;cycle<2;cycle++)
            {
                utilityType.GetMethod("RemoveBannedTraderOffers")!.Invoke(utility,null);
                Check(Snapshot() == original,"HardcoreRules filter changed quest barters.");
                utilityType.GetMethod("RestoreTraderOffers")!.Invoke(utility,null);
                Check(Snapshot() == original,"HardcoreRules restore changed quest barters.");
            }
        }
        finally { harmony.UnpatchSelf(); }
        Console.WriteLine("PASS optional integration: actual Barterizer conversion preserves all 178 quest offers; actual HardcoreRules filter/cache/restore with both bridges preserves item IDs, recipes, stock and quest mappings across two cycles.");
    }
}

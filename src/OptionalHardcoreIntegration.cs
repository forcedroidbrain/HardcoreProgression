using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using Path = System.IO.Path;

namespace HardcoreProgression;

// Reflection keeps HardcoreRules optional: no compile-time or package dependency.
public static class OptionalHardcoreIntegration
{
    private static Progression? progression;
    private static bool restrictionsActive;
    private static bool whitelistOnly;
    private static HashSet<string> allowedItems = [];
    private static HashSet<string> allowedTraders = [];

    public static void Attach(Harmony harmony, Progression owner, ISptLogger<Progression> logger)
    {
        progression = owner;
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("HardcoreRules.Utils.OfferSourceUtils.TraderOffersUtil"))
            .FirstOrDefault(candidate => candidate is not null);
        if (type is null) return;
        string path = Path.Combine(Path.GetDirectoryName(type.Assembly.Location)!, "config.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        restrictionsActive = !root.TryGetProperty("enabled", out var enabled) || enabled.GetBoolean();
        if (root.TryGetProperty("traders", out var traders))
        {
            whitelistOnly = traders.TryGetProperty("whitelist_only", out var only) && only.GetBoolean();
            allowedItems = ReadIds(traders, "whitelist_items");
            allowedTraders = ReadIds(traders, "whitelist_traders");
        }
        var remove = type.GetMethod("RemoveBannedTraderOffers", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new MissingMethodException(type.FullName, "RemoveBannedTraderOffers");
        var restore = type.GetMethod("RestoreTraderOffers", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new MissingMethodException(type.FullName, "RestoreTraderOffers");
        harmony.Patch(remove, prefix: new HarmonyMethod(typeof(OptionalHardcoreIntegration), nameof(BeforeRestriction)));
        harmony.Patch(restore, postfix: new HarmonyMethod(typeof(OptionalHardcoreIntegration), nameof(AfterRestore)));
        logger.Info("[HardcoreProgression] Optional HardcoreRules assortment integration enabled.");
    }

    public static bool Allows(OfferDefinition offer, Dictionary<MongoId, TemplateItem> items)
    {
        if (!restrictionsActive || !whitelistOnly || allowedTraders.Contains(offer.Trader)) return true;
        string id = offer.Template;
        var seen = new HashSet<string>();
        while (seen.Add(id))
        {
            if (allowedItems.Contains(id)) return true;
            if (!items.TryGetValue((MongoId)id, out var item) || item.Parent.IsEmpty) break;
            id = item.Parent.ToString();
        }
        return false;
    }

    private static HashSet<string> ReadIds(JsonElement node, string key) => node.TryGetProperty(key, out var ids)
        ? ids.EnumerateArray().Select(id => id.GetString() ?? "").ToHashSet(StringComparer.Ordinal) : [];

    private static void BeforeRestriction()
    {
        restrictionsActive = true;
        progression?.EnsureOffers();
    }

    private static void AfterRestore()
    {
        restrictionsActive = false;
        progression?.EnsureOffers();
    }
}

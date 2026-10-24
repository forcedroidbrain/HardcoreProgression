using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using HarmonyLib;
using HardcoreProgression.Display;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Helpers.Commerce;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Traders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Eft.Trade;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;
using Path = System.IO.Path;

namespace HardcoreProgression;

public record Metadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.forcedroidbrain.hardcoreprogression";
    public string Name { get; init; } = "HardcoreProgression";
    public string Author { get; init; } = "forcedroidbrain";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("2.0.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.6");
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public List<string>? Incompatibilities { get; init; } = ["zzzzz.local.hardcoreprogression"];
    public string? Url { get; init; } = "https://github.com/forcedroidbrain/HardcoreProgression";
    public bool HasPrepatcher { get; init; }
    public string License { get; init; } = "MIT";
}

public sealed record Cost(string Template, int Count);
public sealed class OfferDefinition
{
    public string Id { get; set; } = "";
    public string Quest { get; set; } = "";
    public string QuestName { get; set; } = "";
    public string Trader { get; set; } = "";
    public string Template { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string Kind { get; set; } = "key";
    public int MinimumLevel { get; set; }
    public List<Cost> Costs { get; set; } = [];
}

public sealed class Settings
{
    public bool Enabled { get; set; } = true;
    public bool EnableMedicineLimits { get; set; }
    public bool EnableSecureContainerLimit { get; set; }
    public bool EnableHardcoreStarterKit { get; set; } = true;
    public bool WriteExchangeCatalog { get; set; }
    public string ServerMessageLanguage { get; set; } = "en";
    public int RaidsPerExchange { get; set; } = 6;
    public Dictionary<string, int> MedicineLimits { get; set; } = [];
    public List<Cost> KeyCosts { get; set; } = [];
    public List<Cost> LateKeyCosts { get; set; } = [];
    public List<Cost> PartCosts { get; set; } = [];
    public int LateQuestLevel { get; set; } = 16;
    [System.Text.Json.Serialization.JsonIgnore]
    public List<OfferDefinition> Offers { get; set; } = [];

    public static Settings Load(string root)
    {
        var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(root, "config.json")), RaidLedger.Json)
            ?? throw new InvalidDataException("Empty config.json.");
        if (!settings.Enabled) return settings;
        if (settings.RaidsPerExchange is < 1 or > 1000000 || settings.LateQuestLevel < 0)
            throw new InvalidDataException("RaidsPerExchange must be 1-1000000; LateQuestLevel cannot be negative.");
        if (settings.MedicineLimits is null || settings.KeyCosts is null || settings.LateKeyCosts is null || settings.PartCosts is null
            || string.IsNullOrWhiteSpace(settings.ServerMessageLanguage)) throw new InvalidDataException("Null or empty configuration field.");
        if (settings.MedicineLimits.Any(entry => !IsId(entry.Key) || entry.Value < 1))
            throw new InvalidDataException("MedicineLimits requires item template IDs and positive counts.");
        settings.Offers = JsonSerializer.Deserialize<List<OfferDefinition>>(File.ReadAllText(Path.Combine(root, "offers.json")), RaidLedger.Json)
            ?? throw new InvalidDataException("Empty offers.json.");
        foreach (var offer in settings.Offers)
            if (offer is null || !IsId(offer.Quest) || !IsId(offer.Template) || !IsId(offer.Trader)
                || offer.Costs is null || offer.Kind is not ("key" or "part") || offer.MinimumLevel < 0)
                throw new InvalidDataException("Invalid quest, item, trader, kind or level in offers.json.");
        if (settings.Offers.Select(o => o.Quest + ":" + o.Template).Distinct().Count() != settings.Offers.Count)
            throw new InvalidDataException("Duplicate quest/item pairs in offers.json.");
        var costs = settings.KeyCosts.Concat(settings.LateKeyCosts).Concat(settings.PartCosts).Concat(settings.Offers.SelectMany(o => o.Costs));
        if (costs.Any(cost => cost is null || !IsId(cost.Template) || cost.Count < 1 || Money.GetMoneyTpls().Contains((MongoId)cost.Template)))
            throw new InvalidDataException("Barter costs require item template IDs, positive counts and no currency.");
        return settings;
    }

    private static bool IsId(string? id) => id is { Length: 24 } && id.All(Uri.IsHexDigit);
}

public sealed class QuestRaidProgress
{
    public double StartedAt { get; set; }
    public long CompletedRaids { get; set; }
    public Dictionary<string, long> NextExchangeAt { get; set; } = [];
}
public sealed class PendingRaid
{
    public string ServerId { get; set; } = "";
    public Dictionary<string, double> ActiveQuests { get; set; } = [];
}
public sealed class ProfileRaidProgress
{
    public Dictionary<string, QuestRaidProgress> Quests { get; set; } = [];
    public PendingRaid? PendingRaid { get; set; }
    public List<string> CompletedRaidIds { get; set; } = [];
}
public sealed class RaidProgress
{
    public int Version { get; set; } = 2;
    public Dictionary<string, ProfileRaidProgress> Profiles { get; set; } = [];
}

public sealed class RaidLedger(string path)
{
    private readonly object sync = new();
    public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private RaidProgress Read()
    {
        var progress = File.Exists(path)
            ? JsonSerializer.Deserialize<RaidProgress>(File.ReadAllText(path), Json) ?? throw new InvalidDataException("Empty raid ledger")
            : new();
        if (progress.Version != 2) throw new InvalidDataException("Unsupported raid ledger version");
        return progress;
    }

    private void Write(RaidProgress progress)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(progress, Json));
        File.Move(temporary, path, true);
    }

    public void StartRaid(RaidSnapshot snapshot, string serverId)
    {
        lock (sync)
        {
            using var lease = new LedgerLease(path);
            var progress = Read();
            if (!progress.Profiles.TryGetValue(snapshot.Profile, out var profile))
                progress.Profiles[snapshot.Profile] = profile = new();
            if (profile.CompletedRaidIds.Contains(serverId)) return;
            profile.PendingRaid = new() { ServerId = serverId, ActiveQuests = new(snapshot.ActiveQuests) };
            Write(progress);
        }
    }

    public int CompleteRaid(string profileId, string serverId, Dictionary<string, double> activeAtEnd, ExitStatus status)
    {
        lock (sync)
        {
            using var lease = new LedgerLease(path);
            var progress = Read();
            if (!progress.Profiles.TryGetValue(profileId, out var profile) || profile.PendingRaid?.ServerId != serverId
                || profile.CompletedRaidIds.Contains(serverId)) return 0;
            var pending = profile.PendingRaid;
            profile.PendingRaid = null;
            profile.CompletedRaidIds.Add(serverId);
            if (profile.CompletedRaidIds.Count > 512) profile.CompletedRaidIds.RemoveAt(0);
            int counted = 0;
            if (status is ExitStatus.SURVIVED or ExitStatus.KILLED or ExitStatus.RUNNER or ExitStatus.MISSINGINACTION)
                foreach (var quest in pending.ActiveQuests)
                {
                    if (!activeAtEnd.TryGetValue(quest.Key, out var startedAt) || startedAt != quest.Value) continue;
                    if (!profile.Quests.TryGetValue(quest.Key, out var record) || record.StartedAt != startedAt)
                        profile.Quests[quest.Key] = record = new() { StartedAt = startedAt };
                    record.CompletedRaids++;
                    counted++;
                }
            Write(progress);
            return counted;
        }
    }

    public bool IsEligible(string profile, OfferDefinition offer, double startedAt, int required)
    {
        lock (sync)
        {
            using var lease = new LedgerLease(path);
            var progress = Read();
            return progress.Profiles.TryGetValue(profile, out var data)
                && data.Quests.TryGetValue(offer.Quest, out var record)
                && record.StartedAt == startedAt
                && record.CompletedRaids >= record.NextExchangeAt.GetValueOrDefault(offer.Id, required);
        }
    }

    public Dictionary<string, QuestRaidProgress> GetQuestRecords(string profile)
    {
        lock (sync)
        {
            using var lease = new LedgerLease(path);
            var progress = Read();
            return progress.Profiles.TryGetValue(profile, out var data) ? data.Quests : [];
        }
    }

    public void RecordExchange(string profile, OfferDefinition offer, double startedAt, int required)
    {
        lock (sync)
        {
            using var lease = new LedgerLease(path);
            var progress = Read();
            if (!progress.Profiles.TryGetValue(profile, out var data) || !data.Quests.TryGetValue(offer.Quest, out var record) || record.StartedAt != startedAt)
                throw new InvalidDataException("Exchange has no matching quest raid record");
            record.NextExchangeAt[offer.Id] = checked(record.CompletedRaids + required);
            Write(progress);
        }
    }
}

public sealed class LedgerLease : IDisposable
{
    private readonly Mutex mutex;
    public LedgerLease(string path)
    {
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant())));
        mutex = new Mutex(false, "HardcoreProgression.Ledger." + key);
        try { if (!mutex.WaitOne(TimeSpan.FromSeconds(5))) { mutex.Dispose(); throw new IOException("Raid ledger is busy"); } }
        catch (AbandonedMutexException) { }
    }
    public void Dispose() { mutex.ReleaseMutex(); mutex.Dispose(); }
}

public static class Policy
{
    public static string OfferId(string quest, string template) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes("local-hardcore:" + quest + ":" + template)))[..24].ToLowerInvariant();

    public static bool IsActive(PmcData profile, OfferDefinition offer) => profile.GetQuestStatus((MongoId)offer.Quest) == QuestStatusEnum.Started;

    public static Dictionary<string, double> ActiveQuests(PmcData profile) => (profile.Quests ?? [])
        .DistinctBy(q => q.QId)
        .Where(q => q.Status == QuestStatusEnum.Started).ToDictionary(q => q.QId.ToString(), q => q.StartTime);

    public static double CountItem(PmcData profile, string template) => profile.Inventory?.Items?
        .Where(x => x.Template.ToString() == template).Sum(x => x.Upd?.StackObjectsCount ?? 1) ?? 0;

    public static void AddOffer(Trader trader, OfferDefinition offer)
    {
        MongoId id = (MongoId)offer.Id;
        if (trader.Assort.Items.Any(x => x.Id == id)) return;
        trader.Assort.Items.Add(new Item
        {
            Id = id, Template = (MongoId)offer.Template, ParentId = "hideout", SlotId = "hideout",
            // The ledger, rather than trader-restock purchase counters, controls repeats.
            Upd = new Upd { StackObjectsCount = 999999, UnlimitedCount = true, SpawnedInSession = false }
        });
        trader.Assort.BarterScheme[id] = [offer.Costs.Select(c => new BarterScheme { Template = (MongoId)c.Template, Count = c.Count }).ToList()];
        trader.Assort.LoyalLevelItems[id] = 1;
        if (trader.QuestAssort is null) throw new InvalidDataException("Trader has no quest assortment dictionary");
        if (!trader.QuestAssort.ContainsKey("started")) trader.QuestAssort["started"] = [];
        trader.QuestAssort["started"][id] = (MongoId)offer.Quest;
    }
}

[Injectable(TypePriority = int.MaxValue)]
public sealed class Progression(TemplateTable templates, TradersTable traders, ProfileHelper profiles,
    HttpResponseUtil responses, ISptLogger<Progression> logger) : IOnLoad
{
    internal static Progression? Instance;
    internal static string Root => Path.GetDirectoryName(typeof(Progression).Assembly.Location)!;
    public Settings Config { get; private set; } = new();
    public Dictionary<MongoId, OfferDefinition> Offers { get; } = [];
    private Localization localization = null!;
    private bool ledgerWriteFailed;
    private RaidLedger ledger = new(Path.Combine(Root, "raid-progress.json"));

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        Config = Settings.Load(Root);
        if (!Config.Enabled) { logger.Info("[HardcoreProgression] Disabled."); return Task.CompletedTask; }
        localization = Localization.Load(Root);
        int skipped = 0;
        // Validate and stage the entire catalog before adding anything to the database.
        foreach (var offer in Config.Offers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!templates.Quests.ContainsKey((MongoId)offer.Quest) || !templates.Items.ContainsKey((MongoId)offer.Template)) { skipped++; continue; }
            if (templates.Items[(MongoId)offer.Template].Properties?.QuestItem == true)
                throw new InvalidDataException($"Quest pickup cannot be offered: {offer.Template}");
            offer.Id = Policy.OfferId(offer.Quest, offer.Template);
            if (offer.Costs.Count == 0) offer.Costs = offer.Kind == "part" ? Config.PartCosts : offer.MinimumLevel >= Config.LateQuestLevel ? Config.LateKeyCosts : Config.KeyCosts;
            if (offer.Costs.Count == 0 || offer.Costs.Any(x => x.Count < 1 || x.Template == offer.Template || !templates.Items.ContainsKey((MongoId)x.Template)))
                throw new InvalidDataException($"Invalid barter recipe for {offer.ItemName}");
            if (!traders.TryGetValue((MongoId)offer.Trader, out var trader) || trader.Assort is null) { skipped++; continue; }
            if (trader.QuestAssort is null) throw new InvalidDataException($"Trader has no quest assortment: {offer.Trader}");
            Offers[(MongoId)offer.Id] = offer;
        }
        var harmony = new Harmony("com.forcedroidbrain.hardcoreprogression");
        OptionalHardcoreIntegration.Attach(harmony, this, logger);
        EnsureOffers();
        if (Config.EnableMedicineLimits) ApplyMedicineLimits();
        if (Config.EnableSecureContainerLimit) ApplySecureContainerSizes();
        if (Config.WriteExchangeCatalog)
            File.WriteAllText(Path.Combine(Root, "exchange-catalog.json"), JsonSerializer.Serialize(Offers.Values.OrderBy(x => x.QuestName).ThenBy(x => x.ItemName), RaidLedger.Json));
        Instance = this;
        harmony.PatchAll(typeof(Progression).Assembly);
        if (skipped > 0) logger.Info($"[HardcoreProgression] Skipped {skipped} catalog entries whose quest, item or trader is not installed.");
        logger.Info($"[HardcoreProgression] {Offers.Count} quest barters; automatic unlock after {Config.RaidsPerExchange} completed PMC raids with the quest active. No profile inventory changes.");
        return Task.CompletedTask;
    }

    public void EnsureOffers()
    {
        foreach (var offer in Offers.Values)
            if (OptionalHardcoreIntegration.Allows(offer, templates.Items)
                && traders.TryGetValue((MongoId)offer.Trader, out var trader) && trader.Assort is not null)
                Policy.AddOffer(trader, offer);
    }

    public static bool FitsSecureGrid(JsonElement inventoryItems, string templateId, Dictionary<MongoId, TemplateItem> itemTemplates)
    {
        var items = inventoryItems.EnumerateArray().ToList();
        foreach (var container in items.Where(x => x.GetProperty("_tpl").GetString() == templateId))
        {
            string id = container.GetProperty("_id").GetString()!;
            foreach (var child in items.Where(x => x.TryGetProperty("parentId", out var p) && p.GetString() == id))
            {
                string childId = child.GetProperty("_id").GetString()!;
                // Attachments can expand an item's footprint; do not shrink around an assembly.
                if (items.Any(x => x.TryGetProperty("parentId", out var p) && p.GetString() == childId)) return false;
                if (!itemTemplates.TryGetValue((MongoId)child.GetProperty("_tpl").GetString()!, out var item) || item.Properties is null) return false;
                if (!child.TryGetProperty("location", out var location) || !location.TryGetProperty("x", out var x) || !location.TryGetProperty("y", out var y)) return false;
                var rotation = location.TryGetProperty("r", out var r) ? r.ToString() : "Horizontal";
                var width = item.Properties.Width ?? 1; var height = item.Properties.Height ?? 1;
                if (rotation is "Vertical" or "1") (width, height) = (height, width);
                if (x.GetInt32() < 0 || y.GetInt32() < 0 || x.GetInt32() + width > 2 || y.GetInt32() + height > 2) return false;
            }
        }
        return true;
    }

    private void ApplySecureContainerSizes()
    {
        // LocalStackLimits intentionally only enlarges grids. Enforce the hardcore size here, after checking every saved inventory.
        var inventories = new List<JsonElement>();
        string directory = Path.GetFullPath(Path.Combine(Root, "../../profiles"));
        if (Directory.Exists(directory))
            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                inventories.Add(document.RootElement.GetProperty("characters").GetProperty("pmc").GetProperty("Inventory").GetProperty("items").Clone());
            }
        int count = 0;
        foreach (var template in templates.Items.Values)
        {
            if (template.Type != "Item" || template.Id.ToString() is "5c0a5a5986f77476aa30ae64" or "5c0a794586f77461c458f892") continue;
            var id = template.Id; var seen = new HashSet<MongoId>(); bool secure = false;
            while (seen.Add(id) && templates.Items.TryGetValue(id, out var parent))
            { if (id.ToString() == "5448bf274bdc2dfc2f8b456a") { secure = true; break; } id = parent.Parent; }
            if (!secure) continue;
            if (inventories.Any(x => !FitsSecureGrid(x, template.Id.ToString(), templates.Items)))
            { logger.Warning($"[HardcoreProgression] Secure container {template.Id} not shrunk: move its saved contents inside 2x2 and restart."); continue; }
            foreach (var grid in template.Properties?.Grids ?? [])
                if (grid.Properties is not null) { grid.Properties.CellsH = 2; grid.Properties.CellsV = 2; }
            count++;
        }
        logger.Info($"[HardcoreProgression] {count} secure-container templates limited to 2x2 after saved-inventory checks.");
    }

    private void ApplyMedicineLimits()
    {
        // Keep one cash listing per medicine, on Therapist only. Alternate listings would multiply the cap.
        foreach (var entry in Config.MedicineLimits)
        {
            bool kept = false;
            foreach (var pair in traders.OrderByDescending(t => t.Key == Traders.THERAPIST))
            {
                if (pair.Value.Assort is null) continue;
                foreach (var item in pair.Value.Assort.Items.Where(x => x.ParentId == "hideout" && x.Template.ToString() == entry.Key).ToList())
                {
                    if (!pair.Value.Assort.BarterScheme.TryGetValue(item.Id, out var scheme) || scheme.Count == 0) continue;
                    bool cash = scheme[0].Any(c => Money.GetMoneyTpls().Contains(c.Template));
                    if (!cash) continue;
                    if (pair.Key != Traders.THERAPIST || kept) { pair.Value.Assort.RemoveItemFromAssort(item.Id); continue; }
                    item.Upd ??= new();
                    item.Upd.BuyRestrictionMax = entry.Value;
                    item.Upd.BuyRestrictionCurrent = 0;
                    kept = true;
                }
            }
        }
    }

    public bool Eligible(PmcData profile, MongoId sessionId, OfferDefinition offer)
    {
        if (ledgerWriteFailed || !Policy.IsActive(profile, offer)) return false;
        try { return ledger.IsEligible(sessionId.ToString(), offer, profile.Quests!.First(q => q.QId.ToString() == offer.Quest).StartTime, Config.RaidsPerExchange); }
        catch (Exception ex) { logger.Error($"[HardcoreProgression] Cannot read raid ledger, exchange locked: {ex.Message}"); return false; }
    }

    public QuestProgressResponse GetQuestProgress(MongoId sessionId)
    {
        var result = new QuestProgressResponse { Locales = localization.Languages };
        if (sessionId.IsEmpty) { result.Available = false; return result; }
        try
        {
            var profile = profiles.GetPmcProfile(sessionId);
            if (profile is null) return result;
            var records = ledger.GetQuestRecords(sessionId.ToString());
            var active = Policy.ActiveQuests(profile);
            foreach (var group in Offers.Values.GroupBy(o => o.Quest))
            {
                if (!active.TryGetValue(group.Key, out double startedAt)) continue;
                records.TryGetValue(group.Key, out var record);
                if (record?.StartedAt != startedAt) record = null;
                long completed = Math.Max(0, record?.CompletedRaids ?? 0);
                var display = new QuestProgressInfo
                {
                    Raids = (int)Math.Min(completed, Config.RaidsPerExchange),
                    Required = Config.RaidsPerExchange,
                    Unlocked = completed >= Config.RaidsPerExchange
                };
                if (record is not null)
                    foreach (var offer in group)
                        if (record.NextExchangeAt.TryGetValue(offer.Id, out long next) && next > completed)
                            display.Repeats.Add(new RepeatExchangeProgress
                            {
                                ItemName = offer.ItemName,
                                ItemTemplate = offer.Template,
                                Required = Config.RaidsPerExchange,
                                Raids = (int)Math.Clamp(Config.RaidsPerExchange - (next - completed), 0, Config.RaidsPerExchange)
                            });
                result.Quests[group.Key] = display;
            }
        }
        catch (Exception ex)
        {
            result.Available = false;
            result.Quests.Clear();
            logger.Error($"[HardcoreProgression] Cannot read description progress: {ex.Message}");
        }
        return result;
    }

    public void Filter(MongoId sessionId, TraderAssort assort)
    {
        var profile = profiles.GetPmcProfile(sessionId);
        foreach (var item in assort.Items.Where(x => Offers.ContainsKey(x.Id)).ToList())
            if (profile is null || !Eligible(profile, sessionId, Offers[item.Id])) assort.RemoveItemFromAssort(item.Id);
    }

    public bool BeforeBuy(PmcData profile, ProcessBuyTradeRequestData request, MongoId sessionId, ref bool foundInRaid,
        ItemEventRouterResponse output, out PurchaseState? state)
    {
        state = null;
        if (!Offers.TryGetValue(request.ItemId, out var offer)) return true;
        if (request.TransactionId.ToString() != offer.Trader || request.Count != 1 || !Eligible(profile, sessionId, offer))
        {
            responses.AppendErrorToOutput(output, localization.Format(Config.ServerMessageLanguage, "ExchangeLocked", Config.RaidsPerExchange));
            return false;
        }
        foundInRaid = false;
        state = new(offer, sessionId.ToString(), Policy.CountItem(profile, offer.Template), profile.Quests!.First(q => q.QId.ToString() == offer.Quest).StartTime);
        return true;
    }

    public void AfterBuy(PmcData profile, PurchaseState? state)
    {
        if (state is not null && Policy.CountItem(profile, state.Offer.Template) > state.Before)
        {
            try { ledger.RecordExchange(state.Profile, state.Offer, state.StartedAt, Config.RaidsPerExchange); }
            catch (Exception ex)
            {
                ledgerWriteFailed = true;
                logger.Error($"[HardcoreProgression] Cannot save purchase progress; further exchanges locked until restart: {ex.Message}");
            }
        }
    }

    public RaidSnapshot? CaptureRaid(MongoId sessionId, StartLocalRaidRequestData request)
    {
        if (!string.Equals(request.PlayerSide, "pmc", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(request.Location) || string.Equals(request.Location, "hideout", StringComparison.OrdinalIgnoreCase)) return null;
        var profile = profiles.GetPmcProfile(sessionId);
        if (profile is null) return null;
        var questIds = Offers.Values.Select(o => o.Quest).ToHashSet();
        return new(sessionId.ToString(), Policy.ActiveQuests(profile).Where(q => questIds.Contains(q.Key)).ToDictionary());
    }

    public async Task<StartLocalRaidResponseData> AfterRaidStart(Task<StartLocalRaidResponseData> original, RaidSnapshot? snapshot)
    {
        var response = await original.ConfigureAwait(false);
        if (snapshot is not null && !string.IsNullOrWhiteSpace(response.ServerId))
            try { ledger.StartRaid(snapshot, response.ServerId); }
            catch (Exception ex) { logger.Error($"[HardcoreProgression] Cannot save raid start: {ex.Message}"); }
        return response;
    }

    public async Task AfterRaidEnd(Task original, MongoId sessionId, EndLocalRaidRequestData request)
    {
        // Await the game's complete save, so failed/cancelled saves and duplicate requests cannot award raids.
        await original.ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(request.ServerId) || request.Results?.Result is not ExitStatus status) return;
        try
        {
            var profile = profiles.GetPmcProfile(sessionId);
            if (profile is null) return;
            int credited = ledger.CompleteRaid(sessionId.ToString(), request.ServerId, Policy.ActiveQuests(profile), status);
            if (credited > 0) logger.Info($"[HardcoreProgression] Completed PMC raid credited to {credited} active quest(s).");
        }
        catch (Exception ex) { logger.Error($"[HardcoreProgression] Cannot save raid progress: {ex.Message}"); }
    }

}

public sealed record PurchaseState(OfferDefinition Offer, string Profile, double Before, double StartedAt);
public sealed record RaidSnapshot(string Profile, Dictionary<string, double> ActiveQuests);

[Injectable(TypePriority = OnLoadOrder.Routers + 10)]
public sealed class QuestProgressRouter(JsonUtil jsonUtil, HttpResponseUtil responses) : StaticRouter(jsonUtil,
[
    new RouteAction<EmptyRequestData>("/hardcore-progression/quest-progress",
        (url, info, sessionId, output, cancellationToken) => new ValueTask<string>(responses.NoBody(
            Progression.Instance?.GetQuestProgress(sessionId) ?? new QuestProgressResponse { Available = false })))
]);

[HarmonyPatch(typeof(MatchController), nameof(MatchController.StartLocalRaidAsync))]
public static class RaidStartPatch
{
    [HarmonyPrefix]
    public static void Prefix(MongoId sessionId, StartLocalRaidRequestData request, out RaidSnapshot? __state)
        => __state = Progression.Instance?.CaptureRaid(sessionId, request);
    [HarmonyPostfix]
    public static void Postfix(ref Task<StartLocalRaidResponseData> __result, RaidSnapshot? __state)
    { if (Progression.Instance is { } instance) __result = instance.AfterRaidStart(__result, __state); }
}

[HarmonyPatch(typeof(MatchController), nameof(MatchController.EndLocalRaidAsync))]
public static class RaidEndPatch
{
    [HarmonyPostfix]
    public static void Postfix(ref Task __result, MongoId sessionId, EndLocalRaidRequestData request)
    { if (Progression.Instance is { } instance) __result = instance.AfterRaidEnd(__result, sessionId, request); }
}

[HarmonyPatch(typeof(TraderAssortHelper), nameof(TraderAssortHelper.GetAssort))]
public static class AssortPatch
{
    [HarmonyPrefix]
    public static void Prefix() => Progression.Instance?.EnsureOffers();
    [HarmonyPostfix]
    public static void Postfix(MongoId sessionId, TraderAssort __result) => Progression.Instance?.Filter(sessionId, __result);
}

[HarmonyPatch(typeof(TradeHelper), nameof(TradeHelper.BuyItem))]
public static class BuyPatch
{
    [HarmonyPrefix]
    public static bool Prefix(PmcData pmcData, ProcessBuyTradeRequestData buyRequestData, MongoId sessionId,
        ref bool foundInRaid, ItemEventRouterResponse output, out PurchaseState? __state)
    {
        __state = null;
        return Progression.Instance?.BeforeBuy(pmcData, buyRequestData, sessionId, ref foundInRaid, output, out __state) ?? true;
    }
    [HarmonyPostfix]
    public static void Postfix(PmcData pmcData, PurchaseState? __state) => Progression.Instance?.AfterBuy(pmcData, __state);
}

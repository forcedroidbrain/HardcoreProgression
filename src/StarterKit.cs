using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FastCloner;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;
using Path = System.IO.Path;

namespace HardcoreProgression;

public sealed class StarterKitSettings
{
    public List<Item> Items { get; set; } = [];
}

// Only template data is reachable here. No saved-profile, raid or death hooks are installed.
[Injectable(TypePriority = int.MaxValue)]
public sealed class StarterKitService(TemplateTable templates, JsonUtil jsonUtil, ISptLogger<StarterKitService> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var config = Settings.Load(Progression.Root);
        if (!config.Enabled || !config.EnableHardcoreStarterKit) return Task.CompletedTask;
        if (!templates.Profiles.ContainsKey(StarterKit.ProfileName))
        {
            logger.Info("[HardcoreProgression] Hardcore Playthrough template is absent; optional starter kit skipped.");
            return Task.CompletedTask;
        }
        try
        {
            var settings = jsonUtil.Deserialize<StarterKitSettings>(File.ReadAllText(Path.Combine(Progression.Root, "starter-kit.json")))
                ?? throw new InvalidDataException("Starter kit configuration is empty.");
            StarterKit.Apply(templates, settings);
            logger.Info($"[HardcoreProgression] Starter kit applied to BEAR/USEC Hardcore Playthrough templates ({settings.Items.Count} item records each). Existing profiles are not accessed.");
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException or ArgumentException)
        {
            logger.Warning($"[HardcoreProgression] Optional starter kit skipped; original template retained: {ex.Message}");
        }
        return Task.CompletedTask;
    }
}

public static class StarterKit
{
    public const string ProfileName = "Hardcore Playthrough";

    public static void Apply(TemplateTable templates, StarterKitSettings settings)
    {
        if (!templates.Profiles.TryGetValue(ProfileName, out var original))
            throw new InvalidDataException("HardcoreRules has not created its profile template.");
        if (settings.Items.Count == 0 || settings.Items.Select(i => i.Id).Distinct().Count() != settings.Items.Count)
            throw new InvalidDataException("Starter kit is empty or contains duplicate item IDs.");
        // Stage both factions before publishing either; never mutate Zero to hero or other editions.
        var staged = FastCloner.FastCloner.DeepClone(original) ?? throw new InvalidDataException("Cannot clone the Hardcore template.");
        Build(staged.Bear?.Character?.Inventory ?? throw new InvalidDataException("Missing BEAR inventory."), "bear", templates, settings);
        Build(staged.Usec?.Character?.Inventory ?? throw new InvalidDataException("Missing USEC inventory."), "usec", templates, settings);
        templates.Profiles[ProfileName] = staged;
    }

    private static void Build(BotBaseInventory inventory, string faction, TemplateTable templates, StarterKitSettings settings)
    {
        string equipment = inventory.Equipment?.ToString() ?? throw new InvalidDataException("Missing equipment root.");
        var existing = inventory.Items ?? throw new InvalidDataException("Missing template inventory items.");
        var pockets = existing.Single(i => i.ParentId == equipment && i.SlotId == "Pockets");
        var secure = existing.Single(i => i.ParentId == equipment && i.SlotId == "SecuredContainer");
        if (secure.Template.ToString() != "544a11ac4bdc2d470e8b456a")
            throw new InvalidDataException("Expected an Alpha in the Hardcore template.");
        // Retain inventory infrastructure, empty pockets and Alpha; replace all playable starting items.
        var retained = existing.Where(i => string.IsNullOrEmpty(i.ParentId) || i.Id == pockets.Id || i.Id == secure.Id).ToList();
        var additions = FastCloner.FastCloner.DeepClone(settings.Items) ?? throw new InvalidDataException("Cannot clone starter items.");
        var ids = additions.ToDictionary(i => i.Id.ToString(), i => NewId(faction, i.Id.ToString()).ToString());
        foreach (var item in additions)
        {
            string oldId = item.Id.ToString();
            item.Id = (MongoId)ids[oldId];
            item.ParentId = item.ParentId switch
            {
                "equipment" => equipment,
                "pockets" => pockets.Id.ToString(),
                string parent when ids.TryGetValue(parent, out string? replacement) => replacement,
                _ => throw new InvalidDataException($"Unknown starter parent for {oldId}.")
            };
            if (!templates.Items.TryGetValue(item.Template, out var template) || template.Properties is null)
                throw new InvalidDataException($"Missing starter item template {item.Template}.");
            item.Upd ??= new();
            item.Upd.SpawnedInSession = false;
            item.Upd.StackObjectsCount ??= 1;
            if (item.Upd.StackObjectsCount < 1 || item.Upd.StackObjectsCount > template.Properties.StackMaxSize)
                throw new InvalidDataException($"Invalid starter stack for {item.Template}.");
            // Resolve consumable resources against the final modded templates, including MMGA.
            if (template.Properties.MaxHpResource > 0)
                item.Upd.MedKit = new() { HpResource = template.Properties.MaxHpResource };
            if (template.Properties.MaxResource > 0)
                item.Upd.FoodDrink = new() { HpPercent = template.Properties.MaxResource };
        }
        retained.AddRange(additions);
        foreach (var item in retained)
        {
            item.Upd ??= new();
            item.Upd.SpawnedInSession = false;
        }
        Validate(retained, templates);
        inventory.Items = retained;
        inventory.FastPanel = [];
    }

    private static MongoId NewId(string faction, string id) => (MongoId)Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes($"local-hardcore-starter:{faction}:{id}")))[..24].ToLowerInvariant();

    public static void Validate(List<Item> items, TemplateTable templates)
    {
        var byId = items.ToDictionary(i => i.Id.ToString());
        var occupied = new HashSet<string>();
        var equipmentSlots = new HashSet<string>();
        foreach (var item in items.Where(i => !string.IsNullOrEmpty(i.ParentId)))
        {
            if (!byId.TryGetValue(item.ParentId!, out var parent))
                throw new InvalidDataException($"Orphan starter item {item.Id}.");
            var parentProps = templates.Items[parent.Template].Properties;
            var grid = parentProps?.Grids?.FirstOrDefault(g => g.Name == item.SlotId);
            if (grid is not null)
            {
                ItemLocation location = item.Location is ItemLocation typed ? typed
                    : item.Location is JsonElement json ? JsonSerializer.Deserialize<ItemLocation>(json.GetRawText(), JsonUtil.JsonSerializerOptionsNoIndent)!
                    : throw new InvalidDataException($"Missing starter grid location: {item.Id}.");
                var props = templates.Items[item.Template].Properties!;
                int width = props.Width ?? 1, height = props.Height ?? 1;
                if ((int)location.R != 0) (width, height) = (height, width);
                int x = location.X ?? -1, y = location.Y ?? -1;
                if (x < 0 || y < 0 || x + width > grid.Properties!.CellsH || y + height > grid.Properties.CellsV)
                    throw new InvalidDataException($"Starter item exceeds its grid: {item.Id}.");
                for (int dx = 0; dx < width; dx++)
                    for (int dy = 0; dy < height; dy++)
                        if (!occupied.Add($"{item.ParentId}:{item.SlotId}:{x + dx}:{y + dy}"))
                            throw new InvalidDataException("Overlapping starter items.");
            }
            else if (parentProps?.Cartridges?.FirstOrDefault(c => c.Name == item.SlotId) is { } cartridge)
            {
                if (item.Upd?.StackObjectsCount > cartridge.MaxCount
                    || !equipmentSlots.Add($"{item.ParentId}:{item.SlotId}"))
                    throw new InvalidDataException("Invalid starter magazine contents.");
            }
            else if (parentProps?.Slots?.Any(s => s.Name == item.SlotId) != true
                || !equipmentSlots.Add($"{item.ParentId}:{item.SlotId}"))
                throw new InvalidDataException($"Invalid or duplicate starter slot: {item.SlotId}.");
        }
    }
}

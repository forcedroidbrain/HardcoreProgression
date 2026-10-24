using System.Text.Json;
using FastCloner;
using HardcoreProgression;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;

public static class StarterKitVerification
{
    public static void Run(string database)
    {
        var options = JsonUtil.JsonSerializerOptionsNoIndent!;
        string Encode<T>(T value) => JsonSerializer.Serialize(value, options);
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        var editions = JsonSerializer.Deserialize<Dictionary<string, ProfileSides>>(File.ReadAllText(System.IO.Path.Combine(database, "templates/profiles.json")), options)!;
        var vanilla = editions["SPT Zero to hero"];
        // Same isolated clone and final-item removal used by HardcoreRules 3.1.0.
        var original = FastCloner.FastCloner.DeepClone(vanilla)!;
        original.Bear!.Character!.Inventory!.Items!.RemoveAt(original.Bear.Character.Inventory.Items.Count - 1);
        original.Usec!.Character!.Inventory!.Items!.RemoveAt(original.Usec.Character.Inventory.Items.Count - 1);
        editions[StarterKit.ProfileName] = original;
        string vanillaBefore = Encode(vanilla), standardBefore = Encode(editions["Standard"]), originalBefore = Encode(original);
        var table = Activator.CreateInstance<TemplateTable>();
        typeof(TemplateTable).GetProperty("Profiles")!.SetValue(table, editions);
        var items = JsonSerializer.Deserialize<Dictionary<MongoId, TemplateItem>>(File.ReadAllText(System.IO.Path.Combine(database, "templates/items.json")), options)!;
        // Confirm resources follow this build's active medical rebalance, rather than stale base values.
        items[(MongoId)"5755356824597772cb798962"].Properties!.MaxHpResource = 200;
        typeof(TemplateTable).GetProperty("Items")!.SetValue(table, items);
        var settings = JsonSerializer.Deserialize<StarterKitSettings>(File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "starter-kit.json")), options)!;
        string settingsBefore = Encode(settings);
        StarterKit.Apply(table, settings);
        var applied = editions[StarterKit.ProfileName];
        var expected = new Dictionary<string, int>
        {
            ["5448bd6b4bdc2dfc2f8b4569"] = 1, ["5448c12b4bdc2d02308b456f"] = 2,
            ["6374a822e629013b9c0645c8"] = 1, ["63c6adcfb4ba094317063742"] = 1,
            ["6374a7e7417239a7bf00f042"] = 1, ["573719762459775a626ccbc1"] = 3,
            ["5bffdc370db834001d23eca8"] = 1, ["56e33680d2720be2748b4576"] = 1,
            ["5755356824597772cb798962"] = 1, ["544fb25a4bdc2dfb738b4567"] = 2,
            ["5e831507ea0a7c419c2f9bd9"] = 1, ["544fb3364bdc2d34748b456a"] = 1,
            ["5448fee04bdc2dbc018b4567"] = 1, ["5673de654bdc2d180f8b456d"] = 1
        };
        foreach (var side in new[] { applied.Bear!, applied.Usec! })
        {
            var inventory = side.Character!.Inventory!;
            var all = inventory.Items!;
            Check(all.Count == 27, "Unexpected items or missing template infrastructure.");
            Check(all.All(i => i.Upd?.SpawnedInSession == false), "A starter item has FIR or unspecified FIR state.");
            Check(all.Select(i => i.Id).Distinct().Count() == all.Count, "Duplicate inventory IDs.");
            var pm = all.Single(i => i.Template.ToString() == "5448bd6b4bdc2dfc2f8b4569");
            Check(pm.ParentId == inventory.Equipment.ToString() && pm.SlotId == "Holster", "PM is not equipped.");
            Check(pm.Upd!.Repairable!.Durability == 100 && pm.Upd.Repairable.MaxDurability == 100, "PM is not fully serviceable.");
            foreach (string slot in new[] { "mod_magazine", "mod_reciever", "mod_pistolgrip" })
                Check(all.Count(i => i.ParentId == pm.Id.ToString() && i.SlotId == slot) == 1, "Incomplete PM: " + slot);
            var slide = all.Single(i => i.ParentId == pm.Id.ToString() && i.SlotId == "mod_reciever");
            Check(all.Any(i => i.ParentId == slide.Id.ToString() && i.SlotId == "mod_sight_rear"), "PM rear sight missing.");
            var magazines = all.Where(i => i.Template.ToString() == "5448c12b4bdc2d02308b456f").ToArray();
            foreach (var magazine in magazines)
                Check(all.Single(i => i.ParentId == magazine.Id.ToString() && i.SlotId == "cartridges").Upd!.StackObjectsCount == 8, "Magazine must contain 8 rounds.");
            var ammo = all.Where(i => i.Template.ToString() == "573719762459775a626ccbc1").ToArray();
            Check(ammo.Sum(i => i.Upd!.StackObjectsCount) == 32, "Starter ammunition must total 32 rounds.");
            Check(ammo.Single(i => i.SlotId == "pocket2").Upd!.StackObjectsCount == 16, "Loose ammunition must contain 16 rounds.");
            var secure = all.Single(i => i.SlotId == "SecuredContainer");
            Check(secure.Template.ToString() == "544a11ac4bdc2d470e8b456a" && !all.Any(i => i.ParentId == secure.Id.ToString()), "Alpha changed or contains supplies.");
            foreach (var quantity in expected)
                Check(all.Count(i => i.Template.ToString() == quantity.Key) == quantity.Value, "Wrong quantity: " + quantity.Key);
            Check(!all.Any(i => new[] { "5449016a4bdc2d6f028b456f", "5696686a4bdc2da3298b456a", "569668774bdc2da2298b4568" }.Contains(i.Template.ToString())), "Starter currency found.");
            Check(!all.Any(i => i.ParentId == inventory.Stash.ToString()), "Unrequested supplies in stash.");
            Check(all.Single(i => i.Template.ToString() == "5755356824597772cb798962").Upd!.MedKit!.HpResource == 200, "Medical rebalance not respected.");
            Check(all.Single(i => i.Template.ToString() == "5448fee04bdc2dbc018b4567").Upd!.FoodDrink!.HpPercent == 60, "Water is not full.");
            var roundTrip = JsonSerializer.Deserialize<ProfileSides>(Encode(applied), options)!;
            StarterKit.Validate(roundTrip.Usec!.Character!.Inventory!.Items!, table);
        }
        string appliedBefore = Encode(applied);
        StarterKit.Apply(table, settings);
        Check(Encode(editions[StarterKit.ProfileName]) == appliedBefore, "Repeated initialization duplicates or changes the kit.");
        Check(Encode(original) == originalBefore, "A pre-existing template/profile reference was mutated.");
        Check(Encode(vanilla) == vanillaBefore && Encode(editions["Standard"]) == standardBefore, "Another edition changed.");
        Check(Encode(settings) == settingsBefore, "Starter configuration was mutated.");
        var newProfile = FastCloner.FastCloner.DeepClone(editions[StarterKit.ProfileName].Usec!.Character)!;
        newProfile.Inventory!.Items!.Clear();
        Check(editions[StarterKit.ProfileName].Usec!.Character!.Inventory!.Items!.Count == 27, "A created profile shares template inventory references.");

        var bad = FastCloner.FastCloner.DeepClone(settings)!;
        bad.Items.First(i => i.SlotId == "cartridges").Upd!.StackObjectsCount = 9;
        void Reject(StarterKitSettings invalid)
        {
            var before = editions[StarterKit.ProfileName];
            try { StarterKit.Apply(table, invalid); throw new Exception("Invalid starter kit was accepted."); }
            catch (InvalidDataException) { }
            Check(ReferenceEquals(before, editions[StarterKit.ProfileName]) && Encode(before) == appliedBefore, "Failed staging partially changed the template.");
        }
        Reject(bad);
        bad = FastCloner.FastCloner.DeepClone(settings)!;
        bad.Items.First(i => i.SlotId == "main").Location = new ItemLocation { X = 99, Y = 99 };
        Reject(bad);
        bad = FastCloner.FastCloner.DeepClone(settings)!;
        bad.Items[0].Template = (MongoId)"ffffffffffffffffffffffff";
        Reject(bad);
        Console.WriteLine("PASS starter kit: BEAR/USEC; complete PM; 2x8 loaded + 16 loose; exact supplies; all non-FIR; valid placement and serialization; empty Alpha/stash; final medicine resources; idempotency; other editions/existing references untouched; atomic rejection of invalid kits.");
    }
}

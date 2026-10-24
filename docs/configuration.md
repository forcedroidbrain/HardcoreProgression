# Configuration reference

All setting names and technical descriptions are English. Player-facing strings live in `locales/`; changing a translation never changes a gameplay setting. Restart the server after editing.

## config.json

| Setting | Type/default | Details |
| --- | --- | --- |
| `Enabled` | boolean, `true` | False skips catalog loading, progression hooks and starter-kit changes. The HTTP route remains registered and reports unavailable. |
| `RaidsPerExchange` | integer, `6` | Range 1–1,000,000. Applies to the initial unlock and to the fresh raids after each purchase. Changing it mid-playthrough does not rewrite repeat targets already stored in the ledger. |
| `EnableMedicineLimits` | boolean, `false` | Keep one cash listing per configured medicine at Therapist; remove alternative cash listings at other traders and duplicate cash listings. Existing item barters are unchanged. If Barterizer has already converted a listing, this cash-only rule does not change it. |
| `EnableSecureContainerLimit` | boolean, `false` | Set grids of eligible secure containers to 2x2. Two known bot secure-container templates are excluded. All saved PMC inventories are checked before shrinking each item template; uncertain or overflowing contents prevent that template from shrinking. This does not create a key-only filter. |
| `EnableHardcoreStarterKit` | boolean, `true` | Replace the playable starting loadout only in an existing `Hardcore Playthrough` template. Missing templates are normal in standalone mode. No other edition or existing profile is changed. |
| `WriteExchangeCatalog` | boolean, `false` | Write `exchange-catalog.json` with resolved recipes for catalog entries that exist in the current database. It is a diagnostic file, not the input catalog. |
| `ServerMessageLanguage` | string, `en` | `en` or `ru` for blocked-purchase messages. Unknown codes use English. Logs remain English. |
| `MedicineLimits` | object | Item template ID to positive per-restock purchase limit. Used only with `EnableMedicineLimits`. Shipped limits: AI-2 1, aseptic bandage 2, Esmarch 1, immobilizing splint 1. |
| `LateQuestLevel` | integer, `16` | A key offer whose configured `MinimumLevel` meets this threshold uses `LateKeyCosts`; otherwise it uses `KeyCosts`. It does not impose an additional player-level requirement. |
| `KeyCosts` | cost array | Default key recipe: 1 Toolset, 1 Pliers, 2 Duct tape. |
| `LateKeyCosts` | cost array | Default later-key recipe: 2 Toolsets, 1 Pliers Elite, 3 Duct tape. |
| `PartCosts` | cost array | Fallback part recipe when an entry has no explicit costs: 2 Weapon parts, 1 Toolset. Shipped part entries have their own costs, prepared using a reference budget of at least 50,000 RUB or three times the part's reference handbook value. These are static recipes, not live prices. |

Cost entries use `{ "Template": "24-character item template ID", "Count": 1 }`. Counts must be positive integers; currencies are rejected. Ingredients must exist in the current database before a matching offer is installed.

Optional medicine and container changes are global. The mod does not infer a profile's preferred rules or change HardcoreRules settings. Make backups before enabling rules for an existing playthrough.

## offers.json

The input is an array of offers:

```json
{
  "Quest": "596b43fb86f77457ca186186",
  "QuestName": "The Extortionist",
  "Trader": "54cb57776803fa99248b456e",
  "Template": "593962ca86f774068014d9af",
  "ItemName": "Unknown key",
  "Kind": "key",
  "MinimumLevel": 7,
  "Costs": []
}
```

`Quest`, `Trader` and `Template` are SPT IDs. `Kind` is `key` or `part`. `Costs: []` selects a configured fallback recipe. `QuestName` and `ItemName` are English reference labels; the client localizes repeated items through SPT. `Id`, if present, is recomputed from the stable quest/item pair for compatibility with the private prototype's version-2 ledger. Do not use duplicate quest/item pairs.

The catalog is curated. The mod does not parse every location's locks, discover new mod quests, solve arbitrary Gunsmith builds, or regenerate recipes from live prices. Missing quests, items or traders are skipped and reported in one summary. Invalid IDs, duplicate pairs, invalid recipes and quest pickup items are rejected before catalog items are added to trader assortments.

## starter-kit.json

Contains SPT item records for a PM, two loaded 8-round magazines and 16 loose rounds (32 total), 6Kh5 bayonet, T-Bag, AI-2, two aseptic bandages, Esmarch, splint, saury and water. The profile's empty Alpha and required inventory infrastructure are retained. Supplies are non-FIR; consumable resource values come from the final item templates.

Parent IDs `equipment` and `pockets` are resolved to the new-profile template's actual inventory IDs. Other parents reference records within the kit. Grid placement is zero-based. The service stages both factions and publishes neither if validation fails. Keep the complete PM assembly and valid magazine capacities. This integration does not create a standalone Hardcore profile template.

## Translations

The shipped locale keys are `Raids`, `Unlocked`, `Repeat`, and `ExchangeLocked`. Preserve the `{0}` placeholder in `ExchangeLocked`. English is required and supplies missing keys in other languages. Unsupported game languages use English; repeat-item names still use the game's own locale.

Translations are sent from the SPT backend with the progress response. Keep locale files beside the server mod, not in the client plugin folder. The mod does not rewrite SPT's global locale or another mod's descriptions.

## Progress ledger

`raid-progress.json` is generated on the first recorded raid. A fresh installation does not ship another user's ledger. Its version-2 records are scoped to profile ID and quest start timestamp. A recorded purchase stores that offer's next eligible completed-raid total. Keep this file with profile backups; never edit it while the server runs.

# Hardcore Progression

**Get another chance at a quest key or weapon part — earn it through raids.**

Hardcore Progression adds quest-specific recovery barters at Therapist and Mechanic. Accept a supported quest and complete six PMC raids while it remains active to unlock the corresponding exchanges. Each purchase gives one non-FIR item. Need another? Complete six more raids after that purchase.

Works on **SPT 4.1.6** as a standalone mod. **HardcoreRules and Barterizer are optional.**

## Features

- A curated catalog of 178 quest/item pairs: 79 key offers at Therapist and 99 weapon-part offers at Mechanic.
- Automatic, persistent raid progression for each profile and quest assignment.
- Independent repeat waits for each offer; buying one part does not lock the other parts for the same quest.
- Raid counters and repeat progress above the original quest descriptions, in English and Russian, with English fallback.
- Optional cash-medicine limits, secure-container size limits, and a minimal starter kit for HardcoreRules' new-profile template.

Survived, killed, run-through and missing-in-action PMC results count. Scav raids, transit, aborted raids and failed raid saves do not. The quest must be the same active assignment at the start and end of the raid.

This is a recovery path for selected keys and parts, not automatic completion of every quest or Gunsmith build. It does not give quest pickup items, complete weapons, FIR status, or credit toward quest objectives.

## Install

1. Back up your profile, then close the game and server.
2. Extract the release ZIP into your SPT root, keeping its `SPT_Runtime` and `BepInEx` folders.
3. Review `SPT_Runtime/user/mods/HardcoreProgression/config.json`.
4. Restart the server and game. Check the startup summary and a supported active quest.

Install both the server component and companion client plugin for the counters. Remove the private Local-HardcoreProgression prototype before installing this version.

## Settings and languages

Configs and technical descriptions use English. Player-facing strings are separated into `locales/en.json` and `locales/ru.json`; missing translations fall back to English. Repeat-item names use SPT's item localization.

The default setup enables progression, leaves medicine/container rules disabled, and applies the starter kit only if the `Hardcore Playthrough` template exists. Optional medicine/container rules affect the whole server, including existing profiles. The kit affects only new characters created from that template.

See the [configuration reference](https://github.com/forcedroidbrain/HardcoreProgression/blob/main/docs/configuration.md) for every setting and editable recipe.

## Optional integrations

- **HardcoreRules:** use its hardcore service/trading/container restrictions alongside progression. This mod does not overwrite its config. Whitelist-only restrictions still apply.
- **Barterizer:** convert eligible regular cash offers to item barters. Our quest offers already use item barters and remain separate. Barterizer does not supply all HardcoreRules restrictions.

Better Keys, QuestBackport and LocalStackLimits are not required. Missing catalog quests/items are skipped. Fika is untested; mods that replace assortments, quest descriptions or raid saving require additional checks.

## Saves and removal

Back up `raid-progress.json` with your profile. Do not edit it while the server is running. A damaged journal locks these exchanges. Stop the game/server and remove the mod's server/client folders to uninstall. Purchased items and starter items already granted remain in the character's inventory. Keep the journal if you may reinstall.

## My mods

**[SPT Barterizer — Forge](https://sp-mod.com/mod/3195/spt-barterizer)** · [GitHub / releases](https://github.com/forcedroidbrain/SPT-Barterizer)

Turns eligible cash trader offers into item barters while preserving existing barters and normal unlock requirements. Pair it with Hardcore Progression if you want to spend looted materials on regular trader goods as well as quest recovery items. Optional, installed separately.

## Credits and source

By **forcedroidbrain**. [Source, build instructions and support](https://github.com/forcedroidbrain/HardcoreProgression).

Key-to-quest catalog relationships were prepared using MIT-licensed Better Keys NG data by SirTyler, maxloo2 and flir; its license and attribution are included. Optional mods and game assemblies are distributed separately.

Codex assisted with research, code development/revision, automated testing and documentation. The icon was generated using OpenAI image generation. **Contains AI Content** must be enabled for this submission. Forge eligibility under its AI-code policy must be resolved before submission; this draft does not assert approval.

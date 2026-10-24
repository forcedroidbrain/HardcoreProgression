# Release checks — 2.0.0

This is a preparation build, not a claim of Forge approval. Updated test evidence is recorded under ignored `artifacts/`; never include game databases, optional-mod binaries, profiles or raw personal logs in source control.

## Completed automated checks

- Server compiles against SPT 4.1.6; client compiles against the clean installation's game/BepInEx references.
- All 178 catalog quest/item pairs exist in the clean SPT 4.1.6 database.
- Actual Harmony-patched async raid-start/raid-save calls are exercised in process with controlled controller results, including failed and delayed saves, duplicate results, different profiles and quest start timestamps.
- First and repeat unlocks, per-item repeat isolation, non-FIR output gating, bulk rejection and corrupt-ledger locking are checked.
- Starter-kit staging checks both factions, complete PM/magazines, exact supplies, grid placement, all non-FIR, unchanged other editions, idempotency and rejection without partial template changes.
- Standalone missing-profile behavior, disabled-mod independence, English/Russian locale keys/fallback, original quest descriptions and client patch method/parameter/field bindings are checked.

## Clean-server matrix

Run `tools/Test-CleanServer.ps1` only after `tools/Build.ps1`. The script copies runtime files from the clean installation and excludes its user folder, so existing profiles are not used. Supported scenarios are standalone, Barterizer, HardcoreRules, both integrations, and enabled optional medicine/container rules. Test processes are stopped individually in `finally` and evidence is retained.

The 2026-10-24 matrix passed against a copy of the clean SPT 4.1.6 runtime. HardcoreRules 3.1.0 and Barterizer 0.1.3 were copied separately into compatibility fixtures; HardcoreRules' client DLL was present for its integrity check. No existing profiles were copied. All five owned server processes were stopped after verification.

| Scenario | Startup/progress API | Quest offers | Gamma |
| --- | --- | --- | --- |
| Progression only | Passed | 178 | 3x3 |
| With Barterizer | Passed | 178 | 3x3 |
| With HardcoreRules | Passed; BEAR/USEC starter template applied | 178 | 3x3 |
| With both | Passed; BEAR/USEC starter template applied | 178 | 3x3 |
| Optional medicine/container rules enabled | Passed | 178 | 2x2 |

The in-process integration check also invoked the actual Barterizer converter and HardcoreRules filter/cache/restore implementations with both bridges attached. All 178 quest offers retained IDs, item state, recipes and quest mappings across two complete cycles. The real-data medicine fixture verified one capped cash listing per configured medicine at Therapist.

Evidence: `artifacts/clean-server-results.json` and each isolated server's stdout/stderr. Do not describe startup/API checks as in-game raid or purchase validation.

## Clean-client gameplay, 2026-10-24

The maintainer operated the game manually on `D:\SPT4Clean`; Codex inspected saved profiles, ledger state, API responses and fresh logs. The original profile was held outside the active profiles folder. The disposable USEC profile had level 5, two pre-accepted quests and barter supplies. `RaidsPerExchange=1` accelerated these checks; this is not a natural six-raid playthrough.

- Three real Factory PMC raids recorded two `Survived` results and one `Killed` result. Both active quests reached three completed raids, with distinct raid IDs and no pending raid in the ledger. The third raid ran with HardcoreRules and Barterizer together.
- First and repeat Machinery key and GK-02 purchases issued non-FIR items and charged their exact configured recipes. A key purchased again after the third raid moved its next exchange to raid four. Other item offers remained independent.
- A premature repeat submitted through the live server's item-event API was rejected, leaving inventory and the ledger unchanged. This was a server API check, separate from the manual UI purchases.
- Manual testing exposed visible rich-text tags in the task-list description. The client now respects each TMP field's `richText` setting. The maintainer confirmed readable Russian and English counters in both trader and task-list descriptions, including repeat-item names.

| Gameplay mode | Confirmed purchase and result |
| --- | --- |
| Progression only | First key/GK-02 purchases; key repeat after a real raid; Russian and English counters |
| With Barterizer 0.1.3 | Repeat GK-02 purchase; original recipe preserved; non-FIR item and repeat 0/1 |
| With HardcoreRules 3.1.0 | MP-133 forestock purchase; non-FIR item and repeat 0/1 |
| With both | RK-6 purchase, survived raid, key repeat; independent offers reopen after the raid |
| With both and OpenBarters | Insufficient basket blocks DEAL; substitute payment buys the MP-133/153 pistol grip; non-FIR item and repeat 0/1 |

The OpenBarters check used the maintainer's [OpenBarters fork at cd66ed5](https://github.com/forcedroidbrain/OpenBarters/commit/cd66ed5f355c9c6d4ddce88b915c12fc12109749), version 1.0.1, with its default substitute multiplier of 1.5. Weapon parts were absent from the test stash. The purchase consumed three Toolsets and two Pliers, issued one pistol grip, hid its offer and set its next exchange to raid four. This covers that build and combination; it does not establish compatibility with every OpenBarters version.

The final rebuilt server (`f0dd773a1e0a7a5d08d1f8e248ac8d65c4f5c6a088dea5e20a1dd99eed993509`) and client (`85e685087ab8703f8edb12b0c5793250cb63df4da608e1367aa37e660f60cc44`) were used together for the OpenBarters purchase. The rebuilt server also passed isolated standalone/both startup/API checks with 178 offers. The final OpenBarters session had no BepInEx Error/Fatal entries. Separate game-log errors during earlier raids were retained without attributing them to this mod.

After testing, the assistant-owned game/server processes were stopped. The original clean profile and settings were restored, with 168 backed-up files verified by hash. Temporary mods, generated plugin configs and the disposable profile were archived outside the installation. The main installation was not changed. Logs and generated caches are retained as diagnostics.

Private evidence is under ignored `artifacts/gameplay-20261024-121903/`. Keep raw profiles, credentials, logs and optional-mod binaries out of Git and release assets.

## Before Forge submission

- The scoped gameplay smoke tests above are complete. The default six-raid interval, other exit statuses, quest/profile isolation and optional starter-kit rules have automated coverage; they were not all replayed manually.
- The original draft DLLs completed VirusTotal analysis: **0/71** each on 2026-10-24. See [reports and hashes](security-scans.md). Refresh reports for binaries rebuilt after the gameplay fix; a newly generated lookup URL alone is not scan evidence.
- Resolve Forge's restriction on substantial AI-written functionality. Enabling Contains AI Content alone does not resolve eligibility. Research/code/testing/documentation assistance and generated artwork must be disclosed accurately.
- Confirm the release archive is built from the public source revision and contains only our own binaries, configuration, docs and required license notices. Set category/tags and SPT constraints to tested versions; Fika remains unknown.
- Recheck the live [Content Guidelines](https://forge.sp-tarkov.com/content-guidelines) and [Community Standards](https://forge.sp-tarkov.com/community-standards).

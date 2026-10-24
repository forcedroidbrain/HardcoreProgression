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

- Real Factory raids recorded one `Survived` and one `Killed` result. Both active quests reached two completed raids in the ledger.
- The first Machinery key and GK-02 purchases issued one non-FIR item each and charged their exact configured recipes. The same key template was purchased again after the second raid; its next exchange moved to raid three. Other item offers remained independent.
- A premature repeat submitted through the live server's item-event API was rejected, leaving inventory and the ledger unchanged. This was a server API check, separate from the manual UI purchases.
- Manual testing exposed visible rich-text tags in the task-list description. The client now respects each TMP field's `richText` setting. The maintainer confirmed correct Russian display after the fix. English trader counters were confirmed; explicit task-list English verification and optional-mod gameplay checks are still in progress.
- The test client after this fix has SHA-256 `85e685087ab8703f8edb12b0c5793250cb63df4da608e1367aa37e660f60cc44`. The original client scan does not cover this file. The public draft archive has not yet been replaced.

Private evidence is under ignored `artifacts/gameplay-20261024-121903/`. Keep raw profiles, credentials, logs and optional-mod binaries out of Git and release assets.

## Before Forge submission

- Complete in-game smoke tests on a test profile: quest counters in both panels, first and repeat purchases, real raid return, failed purchase, localization and supported optional-mod combinations.
- The original draft DLLs completed VirusTotal analysis: **0/71** each on 2026-10-24. See [reports and hashes](security-scans.md). Refresh reports for binaries rebuilt after the gameplay fix; a newly generated lookup URL alone is not scan evidence.
- Resolve Forge's restriction on substantial AI-written functionality. Enabling Contains AI Content alone does not resolve eligibility. Research/code/testing/documentation assistance and generated artwork must be disclosed accurately.
- Confirm the release archive is built from the public source revision and contains only our own binaries, configuration, docs and required license notices. Set category/tags and SPT constraints to tested versions; Fika remains unknown.
- Recheck the live [Content Guidelines](https://forge.sp-tarkov.com/content-guidelines) and [Community Standards](https://forge.sp-tarkov.com/community-standards).

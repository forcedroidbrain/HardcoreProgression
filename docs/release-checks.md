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

## Before Forge submission

- Complete in-game smoke tests on a test profile: quest counters in both panels, first and repeat purchases, real raid return, failed purchase, localization and supported optional-mod combinations.
- Both release DLLs completed VirusTotal analysis: **0/71** each on 2026-10-24. See [reports and hashes](security-scans.md). Reconfirm reports after any binary change; a newly generated lookup URL alone is not scan evidence.
- Resolve Forge's restriction on substantial AI-written functionality. Enabling Contains AI Content alone does not resolve eligibility. Research/code/testing/documentation assistance and generated artwork must be disclosed accurately.
- Confirm the release archive is built from the public source revision and contains only our own binaries, configuration, docs and required license notices. Set category/tags and SPT constraints to tested versions; Fika remains unknown.
- Recheck the live [Content Guidelines](https://forge.sp-tarkov.com/content-guidelines) and [Community Standards](https://forge.sp-tarkov.com/community-standards).

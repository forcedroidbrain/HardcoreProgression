# Forge submission preparation

- Display title: **Hardcore Progression**
- Author: **forcedroidbrain**
- GUID, server and client: `com.forcedroidbrain.hardcoreprogression`
- Version: `2.0.0`
- SPT version: `4.1.6` (metadata constraint `~4.1.6`)
- Suggested category: Quests; mention trader and UI features in tags/description.
- Required third-party mods: **none**. The client plugin requires the SPT core supplied by SPT.
- Optional tested integrations: Hardcore Rules 3.1.0, SPT Barterizer 0.1.3; OpenBarters 1.0.1 from the maintainer's fork at cd66ed5 with both. See [the exact test scope](release-checks.md).
- Fika: **unknown / untested**.
- License: MIT for our code, with Better Keys NG data notice/license preserved.
- Source for the prepared revision: https://github.com/forcedroidbrain/HardcoreProgression/tree/fix/quest-counter-rendering
- Icon: `assets/forge-icon.png`.
- English description: [forge-description.md](forge-description.md).
- Russian description: [forge-description.ru.md](forge-description.ru.md).
- Archive: `dist/HardcoreProgression-2.0.0.zip`, generated from the public source revision.
- Executable checks: both current DLL analyses completed on 2026-10-24: server **0/70**, client **0/71**. [Reports and exact hashes](security-scans.md); SHA-256 values are also in `dist/binary-manifest.json`.
- AI disclosure: retained in both descriptions. Enable the relevant field if offered by the submission form; the current published guidelines do not state the old domain's AI-code prohibition or flag requirement.

Both descriptions contain **My mods / Мои моды**, linking SPT Barterizer on Forge and GitHub and explaining its optional economy conversion role.

The scoped gameplay checks are complete. The release stays a draft until the maintainer authorizes publishing. A public GitHub release with a direct ZIP download is needed before submitting its download URL to Forge. No Forge page is created by the tooling in this repository.

Live requirements reviewed on 2026-10-24 at the site hosting the maintainer's Barterizer page:

- https://sp-mod.com/content-guidelines — updated 2026-10-14.
- https://sp-mod.com/community-standards — updated 2025-08-26.

The old forge.sp-tarkov.com pages failed to load during this review. Their search-cache copies include an AI policy absent from the live sp-mod.com text; the earlier preparation note treated that historical policy as current. This review corrects it without asserting moderation approval.

Metadata, source/build instructions, installation-ready ZIP layout, EN/RU documentation, licenses/attribution, local-backend network disclosure and the recorded clean gameplay checks were reviewed. The package contains only this project's two DLLs; optional-mod/game binaries and private test data are excluded. VirusTotal reports must match these exact DLL hashes.

Both descriptions link https://sp-mod.com/mod/3195/spt-barterizer in My mods / Мои моды and explain its optional role.

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
- Source: https://github.com/forcedroidbrain/HardcoreProgression
- Icon: `assets/forge-icon.png`.
- English description: [forge-description.md](forge-description.md).
- Russian description: [forge-description.ru.md](forge-description.ru.md).
- Archive: `dist/HardcoreProgression-2.0.0.zip`, generated from the public source revision.
- Executable checks: the original draft DLL reports displayed **0/71** on 2026-10-24. Both current DLLs changed after the gameplay fix and need new verified reports. [Current hashes and historical reports](security-scans.md); SHA-256 values are also in `dist/binary-manifest.json`.
- **Contains AI Content: yes.** Describe research, code assistance, testing, documentation and generated artwork accurately. Do not represent the flag as an exemption from the AI-code eligibility rule.

Both descriptions contain **My mods / Мои моды**, linking SPT Barterizer on Forge and GitHub and explaining its optional economy conversion role.

The scoped gameplay checks are complete. Publication is pending current binary scans and resolution of Forge's AI-code eligibility requirement. A GitHub draft release and preparation archive do not assert completion of those steps. No Forge page is created by the tooling in this repository.

Live requirements reviewed on 2026-10-24:

- https://forge.sp-tarkov.com/content-guidelines
- https://forge.sp-tarkov.com/community-standards

The current Barterizer Forge page was verified at https://sp-mod.com/mod/3195/spt-barterizer; the old Forge-domain URL could not be retrieved during preparation.

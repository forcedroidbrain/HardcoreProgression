Hardcore Progression adds 178 quest recovery barters for SPT 4.1.6, with English and Russian progress counters in trader and task descriptions.

Accept a supported quest and complete six PMC raids to unlock its recovery offers. Each purchase gives one non-FIR item; repeating that offer requires six new raids after purchase. HardcoreRules and Barterizer are optional. Medicine and secure-container changes are disabled by default.

### Installation

1. Back up your profile and close the game/server.
2. Extract `HardcoreProgression-2.0.0.zip` into the SPT root containing `SPT_Runtime` and `BepInEx`.
3. Review `SPT_Runtime/user/mods/HardcoreProgression/config.json`, then restart the server and game.

[English instructions](https://github.com/forcedroidbrain/HardcoreProgression/blob/v2.0.0/README.md) · [Русское описание](https://github.com/forcedroidbrain/HardcoreProgression/blob/v2.0.0/README.ru.md) · [Configuration](https://github.com/forcedroidbrain/HardcoreProgression/blob/v2.0.0/docs/configuration.md)

Gameplay checks covered first/repeat purchases, survived/killed raid credit, EN/RU counters and optional HardcoreRules 3.1.0 / Barterizer 0.1.3 combinations. Substitute payments were also checked with the maintainer's OpenBarters 1.0.1 fork at cd66ed5. [Exact test scope](https://github.com/forcedroidbrain/HardcoreProgression/blob/v2.0.0/docs/release-checks.md). Fika is untested.

VirusTotal reports verified on 2026-10-24: [server DLL — 0/70](https://www.virustotal.com/gui/file/f0dd773a1e0a7a5d08d1f8e248ac8d65c4f5c6a088dea5e20a1dd99eed993509/detection), [client DLL — 0/71](https://www.virustotal.com/gui/file/85e685087ab8703f8edb12b0c5793250cb63df4da608e1367aa37e660f60cc44/detection). Check `SHA256SUMS.txt` and `binary-manifest.json` against downloaded files.

Codex assisted with research, code development/revision, automated testing and documentation. The icon was generated with OpenAI image generation. [License and attribution](https://github.com/forcedroidbrain/HardcoreProgression/blob/v2.0.0/THIRD-PARTY-NOTICES.md).

# Third-party notices

The key-to-quest relationships used to prepare the key entries in `offers.json` were derived from the MIT-licensed Better Keys NG database. Credit: Tyler Martin (SirTyler), Max Lo (maxloo2), and flir. The original license is preserved in [licenses/BetterKeysNG-MIT.txt](licenses/BetterKeysNG-MIT.txt).

- Better Keys NG: https://gitlab.com/flir063-spt/betterkeys-ng
- Forge page: https://forge.sp-tarkov.com/mod/1888/better-keys-ng
- Upstream license: https://gitlab.com/flir063-spt/betterkeys-ng/-/blob/main/LICENSE

Weapon-part entries were prepared from exact WeaponAssembly requirements in the local SPT/QuestBackport reference setup, with a small curated selection for early Gunsmith tasks. Entries reference game IDs; they do not include game assemblies, complete game databases, quest implementations, or third-party translations. English labels were resolved against the clean SPT 4.1.6 locale and serve as readable fallbacks.

Optional integration targets:

- Hardcore Rules by DanW: https://github.com/dwesterwick/SPTHardcoreRules. Separate project and license (CC BY-NC-SA 4.0 as listed on Forge). No HardcoreRules source or binaries are redistributed by this package. Reflection hooks address its public assortment restriction/restore methods.
- SPT Barterizer by forcedroidbrain: https://github.com/forcedroidbrain/SPT-Barterizer. Distributed separately under MIT. Existing item barters are preserved by its converter.

SPT, BepInEx, Harmony, Newtonsoft.Json, Unity, TMPro, FastCloner, SemanticVersioning and game APIs are build/runtime references supplied by the user's SPT installation. They are not bundled or relicensed by this project. Their respective authors retain their rights.

The icon was generated with OpenAI image generation for this project. It does not use third-party game logos or existing mod artwork.

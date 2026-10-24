# Executable scan reports — 2.0.0

Both current DLLs were submitted to VirusTotal with the maintainer's permission. Completed report pages were verified on **2026-10-24**: the server displayed **0/70**, and the client **0/71**. These are observed detection counts, not a guarantee of safety or a substitute for source review and gameplay testing.

These are the rebuilt files used together in the final clean gameplay check:

| Current binary | SHA-256 | Completed report |
| --- | --- | --- |
| `HardcoreProgression.dll` | `f0dd773a1e0a7a5d08d1f8e248ac8d65c4f5c6a088dea5e20a1dd99eed993509` | [VirusTotal — 0/70](https://www.virustotal.com/gui/file/f0dd773a1e0a7a5d08d1f8e248ac8d65c4f5c6a088dea5e20a1dd99eed993509/detection) |
| `HardcoreProgression.Client.dll` | `85e685087ab8703f8edb12b0c5793250cb63df4da608e1367aa37e660f60cc44` | [VirusTotal — 0/71](https://www.virustotal.com/gui/file/85e685087ab8703f8edb12b0c5793250cb63df4da608e1367aa37e660f60cc44/detection) |

The original draft files were also scanned before the task-list formatting fix. Those historical reports are retained below for traceability and do not describe the current package.

| Binary | SHA-256 | Completed report |
| --- | --- | --- |
| `HardcoreProgression.dll` | `81455d67a8b744ec6606871002d5c84c668cda89c3d7fcd250f81df1c3499ea0` | [VirusTotal — 0/71](https://www.virustotal.com/gui/file/81455d67a8b744ec6606871002d5c84c668cda89c3d7fcd250f81df1c3499ea0/detection) |
| `HardcoreProgression.Client.dll` | `24ca94ce269adeceef4af93d031c8a9028ca85e24b303651c1883d0e84b867ec` | [VirusTotal — 0/71](https://www.virustotal.com/gui/file/24ca94ce269adeceef4af93d031c8a9028ca85e24b303651c1883d0e84b867ec/detection) |

Any binary change requires new hashes and current reports. Archive/documentation-only updates can change the ZIP hash while retaining these exact DLL hashes. The release asset `SHA256SUMS.txt` records the final ZIP hash; `binary-manifest.json` records the DLLs. Reports and vendor counts can change after this verification.

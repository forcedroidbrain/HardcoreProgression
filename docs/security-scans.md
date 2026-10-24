# Executable scan reports — 2.0.0

The original draft's two DLLs were submitted to VirusTotal with the maintainer's permission. Completed report pages were verified on **2026-10-24**. Each displayed **0 detections / 71 vendors** at verification. This records the observed scan result, not a guarantee of safety or a substitute for source review and gameplay testing.

The subsequent task-list formatting fix changed both assemblies because they compile the shared formatter. Completed scans for the current binaries have not been verified:

| Current binary | SHA-256 | Scan status |
| --- | --- | --- |
| `HardcoreProgression.dll` | `f0dd773a1e0a7a5d08d1f8e248ac8d65c4f5c6a088dea5e20a1dd99eed993509` | Pending verification |
| `HardcoreProgression.Client.dll` | `85e685087ab8703f8edb12b0c5793250cb63df4da608e1367aa37e660f60cc44` | Pending verification |

The completed reports below apply only to the original draft files, which are no longer the current package. Refresh reports for the current hashes before publication.

| Binary | SHA-256 | Completed report |
| --- | --- | --- |
| `HardcoreProgression.dll` | `81455d67a8b744ec6606871002d5c84c668cda89c3d7fcd250f81df1c3499ea0` | [VirusTotal — 0/71](https://www.virustotal.com/gui/file/81455d67a8b744ec6606871002d5c84c668cda89c3d7fcd250f81df1c3499ea0/detection) |
| `HardcoreProgression.Client.dll` | `24ca94ce269adeceef4af93d031c8a9028ca85e24b303651c1883d0e84b867ec` | [VirusTotal — 0/71](https://www.virustotal.com/gui/file/24ca94ce269adeceef4af93d031c8a9028ca85e24b303651c1883d0e84b867ec/detection) |

Any binary change requires new hashes and current reports. Archive/documentation-only updates can change the ZIP hash while retaining these exact DLL hashes. The release asset `SHA256SUMS.txt` records the final ZIP hash; `binary-manifest.json` records the DLLs. Reports and vendor counts can change after this verification.

# Revit Open Guard

**Turn it on. Open your model normally. Let Revit get past known opening blockers.**

A standalone Revit 2022-2027 add-in with one persistent **ON / OFF** ribbon button. It handles approved model-opening dialogs and recoverable annotation failures, including Remove Reference and Delete Dimension(s), without asking for confirmation on each occurrence.

## Using it

The button is at **MM Tools > Model Opening > Open Guard**. A first installation starts **OFF**. Click to enable; the state is remembered at the next Revit startup. Click again to disable. Open RVT models through Revit's normal workflow; no special Open command, exporter, batch manager, or additional account is required.

The service remains registered for the Revit session, but only acts during an observed **RVT document opening**. It does not handle ordinary editing, saving, synchronizing, exporting or link reloads outside that opening scope. It never initiates Save, Save As, Sync, model closing or central-file detachment.

**ON authorizes the listed recovery actions, including removing invalid annotation references and deleting invalid dimensions when Revit permits the resolution. These changes affect the document in memory and may be saved when you later Save/Sync.** There are no safety modes or per-error approval dialogs. Unknown or unsupported cases keep Revit's normal prompt.

## Build and install (version 0.2.1)

Build all six versions with `.\scripts\Build.ps1`, then package with `.\installer\Build.ps1 -SkipBuild`. Or run `.\installer\Build.ps1` to do both. Requirements: Windows, .NET 10 SDK and the .NET Framework 4.8 targeting pack.

Output: `installer\dist\Revit-Open-Guard-2022-2027-Setup.exe`.

Run the setup and select target Revit years. Detected installations are preselected. No administrator access is needed. **Revit can remain open during installation.** All DLLs, dependency descriptors and the uninstaller are written and verified first; `.addin` files are published atomically as the final installation files.

Assemblies live under `%APPDATA%\Autodesk\Revit\Addins\<year>\RevitOpenGuard\<content-hash>`. Manifests are in the parent year folder. New builds use new content folders, allowing updates while an earlier DLL is loaded. An already loaded DLL switches to the updated version next Revit session. First-install discovery remains subject to Revit's normal loading prompts and behavior.

Remove through **Windows Installed apps > Revit Open Guard (2022-2027)**. Close Revit before removal. Settings, downloaded rules and logs are preserved. The running uninstaller retains its small EXE under `%LOCALAPPDATA%\MoustafaMagdi\RevitOpenGuard\Setup`.

| Revit | Build target |
| --- | --- |
| 2022-2024 | net48 |
| 2025-2026 | net8.0-windows |
| 2027 | net10.0-windows |

Each payload compiles against its own pinned year-specific API package. Autodesk assemblies are compile-only and are never included in the installer. Build one year using `.\scripts\Build.ps1 -Years 2024`. To use installed Autodesk references, add `-RevitInstallPath 'C:\Program Files\Autodesk\Revit 2024'` with a single year. Build outputs are under `RevitOpenGuard\bin\<year>\Release\<framework>`.

The 2025/2026 DLLs omit an application runtimeconfig, allowing the Revit host runtime to load them. The .NET 10 migration updates for 2025/2026 require actual host validation. See [installer details](installer/README.md).

## Error catalog updates

Use **MM Tools > Model Opening > Error Catalog Updates** to check for new rules and choose **Update**. Version 0.2.1 checks in the background on every Revit startup, even if another session checked less than 24 hours ago. Long sessions retain the daily background check. Network failures remain quiet and preserve the offline catalog. New verified versions are offered at idle, outside model openings.

The notification is a standard Revit TaskDialog titled **Open Guard - Error catalog update**. It shows **A new error catalog is available: <version>**, release notes, an **Update error catalog** command and a **Close** button. The catalog is applied only if the user selects Update; closing the dialog leaves the current catalog unchanged. This check schedule is a DLL behavior change and requires installing version 0.2.1; it cannot be delivered through a rules-only JSON update.

The [live feed](https://github.com/moustafamagdi/RevitOpenGuard/tree/catalog-updates) is independent of add-in binaries. The client verifies an RSA/SHA-256 signature against its embedded public key, validates the schema and action allowlist, rejects downgrades and catalogs requiring a newer tool, then atomically saves a local snapshot and signed backup. Failed or corrupt downloads cannot replace working rules. An invalid cache falls back to a valid backup or embedded baseline. No executable code is downloaded.

Rules never change during a model opening. Other running Revit instances read saved rules at their next model opening. Settings remain shared but ON/OFF changes are loaded at startup, not broadcast. Catalog cache: `%APPDATA%\MoustafaMagdi\RevitOpenGuard\catalog.signed.json`. A successful-check timestamp is saved as `catalog-check.utc`. Logs include the active catalog version. Model paths and logs are not uploaded.

Developer publishing instructions: [CATALOG_UPDATES.md](docs/CATALOG_UPDATES.md). New rules using existing actions need no installer update; new repair logic requires a DLL update.

## Included handling

| Opening case | Action |
| --- | --- |
| Dimension references no longer parallel | Remove Reference / `DetachElements` |
| Cannot form radial dimension | Delete Dimension(s) / `DeleteElements` |
| Dimension perpendicular to current view | Delete Dimension(s) / `DeleteElements` |
| Recognized missing/lost/invalid annotation references | `DetachElements` if offered; known TaskDialog text uses the first recovery command |
| Empty DWG Paper Space with Model Space fallback question | Yes |
| Imported numerical data already truncated | Close (TaskDialog) / OK (standard message box) |
| Recognized far-origin, coordinate, extents, off-axis/import warnings | OK when accepted by that dialog |
| Recognized load/display/deleted-element notices | OK when accepted by that TaskDialog |

See [the policy catalog](docs/HANDLING_CATALOG.md) for exact IDs, text-fallback limitations and source provenance. There is no generic auto-clicker, no `DeleteAllWarnings`, no attempt to bypass ACC permissions, and no NWC-export logic in this repository.

## Settings and logs

- State: `%APPDATA%\MoustafaMagdi\RevitOpenGuard\settings.xml`.
- Audit logs: `%APPDATA%\MoustafaMagdi\RevitOpenGuard\logs\OpenGuard_*.log`.

Writes use a same-directory temporary file, flush and replace, with a `.bak` of the previous settings. A persistence failure is reported once on toggle; the new state still applies to the running session. Each Revit instance loads state at startup; changes are not broadcast to other running instances.

Logs record Revit version/build, opening correlation ID, model path, dialog/failure ID, selected resolution, affected IDs reported by Revit, unknown cases and final opening status. They contain no authentication tokens. Logs rotate around 5 MB and retention runs at startup/rotation (30 days / up to 60 older segments). No report window is shown on successful opening.

## Scope and limitations

`DocumentOpening` starts the scope; `DocumentOpened` ends it, including failed/cancelled opening events. Normal `Idling` clears an orphaned scope as a fallback. Nested openings are tracked, family opens do not enable repair, and failures in documents that were already open are ignored.

Only dialogs/events exposed by Revit within that scope can be handled. A prompt before `DocumentOpening`, after `DocumentOpened`, a native window not reported by `DialogBoxShowing`, an unknown dialog or an unrecoverable file/authorization error can still block opening. English text policies are compatibility fallbacks; the three built-in dimension IDs do not depend on message language. Repeated failed recovery is not retried indefinitely.

Version 0.2.0 provides separate builds for Revit 2022 through 2027. It does not promise every model will open or repair a corrupt RVT. It is independent of ScheduledNwcExporter; using both handlers together requires coexistence testing, especially for the same failure. Start validation with Open Guard alone to attribute behavior correctly.

## Validation status

All six Release builds compile against their year-specific API references. Catalog lifecycle tests pass on .NET Framework 4.8, .NET 8 and .NET 10, including a live HTTPS feed download and signature verification. Run `.\scripts\Test-Catalog.ps1 -Online` with the local developer signing key.

The installer self-test checks all six payloads, their Revit API major versions, absolute manifest paths published last, reinstall/update with a locked old DLL, rollback before publication, uninstall and unrelated-file preservation. It does not install into real Revit directories or register an installed-app entry.

**Actual model-opening integration inside Revit 2022-2027 has not been tested for this change.** Follow [the validation checklist](docs/VALIDATION.md) in each host, including the 2025.5/2026.5 .NET migration updates, before treating it as runtime-validated. The Windows setup EXE is unsigned; downloaded catalogs carry their separate developer signature.

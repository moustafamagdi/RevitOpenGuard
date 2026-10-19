# Revit Open Guard

**Turn it on. Open your model normally. Let Revit get past known opening blockers.**

A standalone Revit 2024 add-in with one persistent **ON / OFF** ribbon button. It handles approved model-opening dialogs and recoverable annotation failures, including Remove Reference and Delete Dimension(s), without asking for confirmation on each occurrence.

## Using it

The button is at **MM Tools > Model Opening > Open Guard**. A first installation starts **OFF**. Click to enable; the state is remembered at the next Revit startup. Click again to disable. Open RVT models through Revit's normal workflow; no special Open command, exporter, batch manager, or additional account is required.

The service remains registered for the Revit session, but only acts during an observed **RVT document opening**. It does not handle ordinary editing, saving, synchronizing, exporting or link reloads outside that opening scope. It never initiates Save, Save As, Sync, model closing or central-file detachment.

**ON authorizes the listed recovery actions, including removing invalid annotation references and deleting invalid dimensions when Revit permits the resolution. These changes affect the document in memory and may be saved when you later Save/Sync.** There are no safety modes or per-error approval dialogs. Unknown or unsupported cases keep Revit's normal prompt.

## Build in Visual Studio 2022

Open `RevitOpenGuard.sln`, select **Release**, then **Rebuild Solution**. Requirements: Windows, Revit 2024, Visual Studio 2022 with .NET desktop development and the .NET Framework 4.8 targeting pack. The project targets **net48 / x64** and references `RevitAPI.dll` and `RevitAPIUI.dll` from the installed Revit directory. Autodesk DLLs are not copied into the output. There are no third-party runtime dependencies.

Default Revit path: `C:\Program Files\Autodesk\Revit 2024`.

Alternatively, from PowerShell in the repository root:

```powershell
.\scripts\Build.ps1
# Non-default Revit installation:
.\scripts\Build.ps1 -RevitInstallPath 'D:\Autodesk\Revit 2024'
```

Output: `RevitOpenGuard\bin\Release\net48\RevitOpenGuard.dll`.

## Install / update

Close Revit, then run:

```powershell
.\scripts\Install.ps1
# For a Debug build:
.\scripts\Install.ps1 -Configuration Debug
```

The script installs for the current user under `%APPDATA%\Autodesk\Revit\Addins\2024`, generates an absolute assembly path in the Application manifest, and leaves settings/logs intact. No administrator access is required. Restart Revit afterward. If corporate script policy blocks execution, use an approved PowerShell session or install the DLL and edited manifest manually; the scripts do not change machine execution policy.

Manual installation: copy `RevitOpenGuard.dll` into `%APPDATA%\Autodesk\Revit\Addins\2024\RevitOpenGuard\`, copy `deploy\RevitOpenGuard.addin` into the parent `2024` folder, and replace `__ASSEMBLY_PATH__` with the absolute DLL path. The template is not directly loadable until that placeholder is replaced. Keep only one installed Application manifest.

`ToggleCommand` can bootstrap the service when run through Add-In Manager for a development session. Automatic startup on later Revit sessions requires the installed `.addin` manifest. Restart Revit when replacing a loaded DLL; hot reload and loading duplicate assembly copies are not supported.

To uninstall:

```powershell
.\scripts\Uninstall.ps1
```

Uninstall removes only this add-in's manifest/DLL/symbols. Settings and logs remain available.

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

This first version targets Revit 2024 only. It does not promise every model will open or repair a corrupt RVT. It is independent of ScheduledNwcExporter; using both handlers together requires coexistence testing, especially for the same failure. Start validation with Open Guard alone to attribute behavior correctly.

## Validation status

Source, project/manifest structure and policy/lifecycle paths have been reviewed. **This initial implementation has not been compiled against a local Revit 2024 installation or executed inside Revit in the authoring environment.** Existing exporter cases supplied the recovery rules; manual-opening integration remains to be validated on actual models. Follow [the validation checklist](docs/VALIDATION.md) before relying on unattended opening. No automated test/CI pipeline is added in this version.

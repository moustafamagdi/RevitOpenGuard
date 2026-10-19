# Opening handling catalog - v0.1.0

Derived from `moustafamagdi/ScheduledNwcExporter`, branch `product/operations-phase-2` (policy blob `f0cd915e177b2ff24e5b7c981bcbd28d90e8481e`, document manager blob `98f8b1442ae5ee5ae4cfd9c72a0a7c87f707e730`). This is a new manual-opening integration; inclusion in the catalog is not a claim that it has been runtime-tested in this new add-in.

## Typed failure resolutions

| Revit built-in failure | Action |
| --- | --- |
| `BuiltInFailures.DimensionFailures.LinearConstraintNotParallel` | `DetachElements` / Remove Reference |
| `BuiltInFailures.DimensionFailures.RadialDimensionCannotProjectToArc` | `DeleteElements` / Delete Dimension(s) |
| `BuiltInFailures.DimensionFailures.DimensionPerpendicularToView` | `DeleteElements` / Delete Dimension(s) |

The first case was reported working by the user in the exporter. The other cases were added in response to real screenshots; no per-case automated runtime evidence is bundled. English text fallbacks recognize the corresponding radial/perpendicular descriptions and missing/lost/invalid annotation references.

The engine checks permitted resolutions, prevents repeat attempts for the same failure/elements in one opening, resolves via `FailuresAccessor`, then requests `ProceedWithCommit` only after a resolution was requested. It does not call `Document.Delete`, does not delete all warnings, and does not override rollback requested by another handler. Logged element IDs are those reported by Revit as failing elements, not a claimed exhaustive list of all cascading changes.

## Dialog signatures

| Signature | Result |
| --- | --- |
| Both `import detected no valid elements in the file's paper space` and `import from the model space` | Yes |
| Both `some numerical data within the imported file was out of range` and `this numerical data has been truncated` | Close for TaskDialog; OK for standard message box |
| `far from the origin`, `large coordinate`, `extents are greater`, `geometry extents`, `imported geometry`, or `slightly off axis` | OK |
| TaskDialog containing missing/lost/invalid dimension/tag/element reference phrases | CommandLink1 |
| TaskDialog containing `cannot be displayed`, `could not be loaded`, `failed to load`, or `some elements were deleted` | OK |

All dialog rules are **text fallbacks**, not verified exact DialogIds. `CommandLink1` assumes the first recovery command matches the known dialog; its button caption cannot be inferred from the event message. Likewise a load/display phrase does not prove an error is harmless. These legacy rules are kept opening-scoped and result acceptance is logged; validate them in the target Revit build. No result is tried repeatedly after it is rejected other than on a new actual dialog event.

The service now reads both `TaskDialogShowingEventArgs.Message` and `MessageBoxShowingEventArgs.Message`. Standard message boxes also check the Win32 button layout before returning OK/Yes. Generic native windows without exposed message text are left alone. Unknown cases and unsupported results remain normal Revit prompts.

## Scope

Persistent enablement is separate from an opening scope. Only observed project openings activate actions. Family openings suspend a parent scope. Each post-open event pops a scope; normal Idling clears any orphaned scope. Already-open documents are excluded from failure resolutions. No arbitrary time limit interrupts slow opening.

NWC output-lock dialogs, exporting, authentication bypasses, Save/Sync automation, and generic UI clicking are deliberately excluded.

## API reference notes

Official Autodesk documentation used for the lifecycle and failure handling:
- [DocumentOpened: includes failed/cancelled opens](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/7e5bc7a1-0475-b2ec-0aec-c410015737fe.htm)
- [DialogBoxShowing: no document modifications in the callback](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/a5b8870c-d2b8-d3e8-fa35-e9e2166d54f5.htm)
- [OverrideResult: valid codes depend on the actual dialog type](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/49ba2725-74c9-02b3-4321-eac1f2295bd3.htm)
- [FailuresProcessingEventArgs: ProceedWithCommit after resolving failures](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/a35dc3de-c8a4-8af0-6a3c-706716e5f885.htm)
- [Attempted resolution types: avoid repeated ineffective resolutions](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/442f5727-d827-033d-fd50-4daf1cf77d5d.htm)

The references describe the API contract; the project targets the installed **Revit 2024** assemblies. Confirm integration using the validation checklist.

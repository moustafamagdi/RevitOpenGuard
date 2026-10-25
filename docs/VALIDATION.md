# Revit 2024 validation

Initial implementation: code review only, not a claimed successful Revit build/runtime test. No test/CI framework is added. Use copies of your example files for this first integration pass.

1. Rebuild Release in Visual Studio 2022 against installed Revit 2024 assemblies. Install using scripts/Install.ps1 with Revit closed.
2. Start Revit without a project. Confirm the one ribbon button appears and starts OFF on a clean install.
3. Turn ON, restart Revit, confirm ON is remembered. Turn OFF, restart, confirm OFF. No confirmation should appear when persistence succeeds.
4. Open a normal RVT from Revit File > Open. Confirm no added dialogs and an OpeningStarted / OpeningFinished pair in the log.
5. Repeat with the known nonparallel, invalid radial and perpendicular dimension samples. Check the failure ID, chosen resolution, Revit-reported element IDs and final opening status in the log.
6. Test the three observed CAD prompts, including Paper Space fallback and truncated data. Confirm DialogAnswered has the expected result. Rejected results must remain logged, not described as successful.
7. Repeat a problematic open with Open Guard OFF. The normal Revit message must appear.
8. After opening, perform a normal edit that generates a warning. Open Guard must not act. Also verify family open, Save, Sync and NWC export are not treated as opening sessions.
9. Cancel an opening, and try a failed opening. Confirm scopes close and subsequent editing is unaffected.
10. Open more than one model consecutively and a host with links. Confirm nested callbacks do not end a parent scope too early or leak it into editing.
11. Test normal ACC opening using the existing Revit login; Open Guard must not store tokens or attempt to bypass denied access.
12. Test with other installed add-ins. First disable ScheduledNwcExporter's simultaneous run to isolate this integration, then check coexistence.

For a new blocker, capture the screenshot plus matching log lines. A dialog before DocumentOpening or after DocumentOpened is outside this version's scope; do not broaden to whole-session auto-clicking to work around it.

The source project should not be distributed as a tested binary until steps 1-10 pass on a real Revit 2024 installation.

## Version 0.2.0 host matrix

Repeat opening, ON/OFF and unknown-dialog checks in Revit 2022 through 2027, including 2025.5/2026.5. Check initial installation while Revit is open, catalog notification, manual download/apply, offline startup and rule snapshot stability during opening. Compile/package tests do not establish host runtime validation.

## Host observation — 2026-10-20

The user confirmed that the automatic catalog-update notification works with add-in 0.2.2 in Revit 2024. This validates notification delivery only; it does not complete model-recovery, ACC, other-year or 2025.5/2026.5 migration testing.

## Version 0.2.3 — dimension reference fallback

Revit 2024 logs supplied on 2026-10-25 show failure `b44c8ba0-7a86-44c1-bbf1-2de8e2017266` with the same invalid-dimension-reference description: deletion was permitted for some affected dimensions and rejected for others. The accompanying screenshot offers Remove Reference(s). Version 0.2.3 tries DetachElements only for this observed failure in 2024, after a matching catalog DeleteElements policy is rejected; both resolution availability and permission are required for reference removal. Unknown failures, other years and other deletion policies gain no fallback. Existing opening scope, attempted-resolution guards and no automatic Save/Sync behavior are preserved.

`tests-recovery` links the production matcher and selector with API stubs. Twelve checks cover deletion preference, permitted fallback, unavailable/forbidden removal, unknown ID/text, year restriction and existing detach policies. Run on net48, net8.0-windows and net10.0-windows. All six application builds and the installer self-test pass. These tests do not establish actual Revit recovery: reopen a copy of the reported model in Revit 2024, confirm RecoveryRequested reports DetachElements where deletion was unavailable, and review dimensions before saving. No live model was modified during development.

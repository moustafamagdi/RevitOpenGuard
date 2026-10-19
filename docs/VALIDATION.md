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

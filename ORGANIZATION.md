# RevitOpenGuard — organized project

Collected on 19 October 2026.

- Category: Revit
- Original source: C:\Users\m.abdellatif\source\repos\moustafamagdi\RevitOpenGuard
- Build: passed
- Installer: RevitOpenGuard-Setup.exe (existing-installer-preserved)
- Target release(s): 2022-2027

## Entry points found in compiled assemblies

- `RevitOpenGuard.App`
- `RevitOpenGuard.CatalogUpdatesCommand`
- `RevitOpenGuard.ToggleCommand`

## Validation and maintenance

Use Build-Organized.ps1 from this folder. Build does not install or load the tool into a running Autodesk application. New installers were tested only in isolated temporary folders: payload SHA256 checks, manifest targets, reinstall, uninstall and preservation of unrelated user files. Host behavior and geometry have not been retested in this organization task. Existing vendor/owner installers are preserved as delivered.

The Installers folder in the parent catalogue contains the reviewed deliveries. Preserve the original README and source notes for operating instructions.

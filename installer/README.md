# Revit Open Guard installer

Build: `.\installer\Build.ps1`. Optional `-OutputDirectory`; use `-SkipBuild` only after all six Release payloads were built.

Output: `installer/dist/Revit-Open-Guard-2022-2027-Setup.exe`.

The graphical setup preselects detected Revit installations and permits selecting any supported year from 2022 through 2027. Installation is per-user without administrator access. Revit can remain open. Each payload targets its own Revit API year; Autodesk DLLs are not redistributed.

All selected payloads, dependency descriptors and the central uninstaller are staged and verified before atomically publishing Application manifests as the final files. Assemblies use content-specific folders to avoid replacing a DLL loaded by Revit. Already loaded add-ins switch DLL versions on the next session.

Manifest: `%APPDATA%/Autodesk/Revit/Addins/<year>/RevitOpenGuard.addin`.
Assembly: `%APPDATA%/Autodesk/Revit/Addins/<year>/RevitOpenGuard/<content-hash>/RevitOpenGuard.dll`.
Uninstaller: `%LOCALAPPDATA%/MoustafaMagdi/RevitOpenGuard/Setup/RevitOpenGuardSetup.exe`.

Windows Installed apps: **Revit Open Guard (2022-2027)**. Uninstall removes all supported manifests and known payload files, preserving unrelated files, settings, downloaded rules and logs. Close Revit for uninstall. The running setup EXE is retained.

The isolated `/selftest` checks six year-specific payloads, manifest-last ordering, locked-DLL reinstall/update, failure rollback, uninstall and unrelated-file preservation. It never registers an installed-app entry or installs into real Revit folders. Runtime behavior requires testing in the actual hosts.

The earlier 2024-only installed-app registration is migrated to the common entry. Setup is unsigned. Error catalogs carry their separate developer signature.

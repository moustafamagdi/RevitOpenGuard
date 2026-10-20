# Revit Open Guard

**Handle known blockers while opening your Revit model.**

A Windows add-in with a simple **ON / OFF** button and an error catalog that can update independently of the add-in.

## Releases

**Version 0.2.2 · Revit 2022–2027 · Per-user installation · No administrator required**

[Release information](https://github.com/moustafamagdi/RevitOpenGuard/releases)

Installers are distributed only as assets attached to this repository’s GitHub Releases. Release information includes the supported version and asset checksums.

## Install and start using it

1. Run the installer obtained from the GitHub release.
2. Select your Revit versions and click **Install**. Detected installations are preselected. **Revit can stay open during installation.**
3. Open Revit. If you are updating an add-in that is already loaded, restart Revit to activate the new DLL. Allow the add-in to load if Revit asks.
4. Go to **MM Tools → Model Opening → Open Guard**.
5. The first installation starts **OFF**. Click once to turn it **ON**, then open your RVT model normally. Click again to turn it OFF. The choice is remembered after restarting Revit.

> **What ON allows:** Open Guard can remove invalid annotation references and delete invalid dimensions when a known recovery rule matches and Revit permits the action. These changes affect the model in memory and may be saved when you later Save or Sync. Open Guard does not initiate Save or Sync itself. Unrecognized cases keep Revit's normal prompt.

<details>
<summary><strong>تعليمات سريعة بالعربية</strong></summary>

- استخدم ملف التثبيت الموجود ضمن أصول الإصدار على GitHub Releases، وشغّله واختر إصدارات Revit المطلوبة ثم **Install**. التثبيت مسموح وRevit مفتوح ولا يحتاج Administrator.
- لو بتحدّث نسخة قديمة محمّلة بالفعل، أعد تشغيل Revit بعد التثبيت.
- من **MM Tools → Model Opening → Open Guard** فعّل الأداة؛ أول تثبيت يبدأ **OFF**، ثم افتح الموديل بالطريقة المعتادة.
- وضع **ON** يسمح بإزالة مراجع أو حذف أبعاد متضررة حسب القواعد المعروفة وعندما يسمح Revit. التعديلات تكون داخل الموديل ويمكن حفظها لاحقًا عند Save/Sync؛ الأداة لا تحفظ أو تعمل Sync بنفسها.
- الأداة تفحص تحديث قائمة الأخطاء عند كل تشغيل Revit. عند ظهور التنبيه اضغط **Update error catalog** لتطبيقه، أو **Close** للتأجيل.

</details>

## Keep the error catalog up to date

The add-in checks in the background **every time Revit starts**, and checks daily during long sessions. It continues working with its saved catalog when offline.

When newer rules are available, a Revit dialog appears with:

- **Open Guard - Error catalog update** as the title.
- The new catalog version and a description of the changes.
- **Update error catalog** to apply the update, or **Close** to keep the current rules.

The check does **not** apply updates automatically. New rules take effect for subsequent model openings. You can also use **MM Tools → Model Opening → Error Catalog Updates → Check for updates** at any time.

Catalog updates are digitally signed and verified by the add-in. Add-in/DLL updates are separate and are published only as GitHub Releases assets. The Windows installer itself is currently unsigned.

## What it handles

| Known opening problem | Recovery, when permitted by Revit |
| --- | --- |
| Invalid dimension references | Remove the broken reference or delete the affected dimensions, according to the matching rule |
| Invalid radial or perpendicular dimensions | Delete the affected dimensions |
| Recognized CAD import and coordinate warnings | Select the configured recovery/acknowledgment |
| Recognized missing references and load/display notices | Apply the matching catalog action |

The current catalog includes a narrow Revit 2024 English rule for **“One or more dimension references are or have become invalid.”**, using **Delete Dimension(s)** when Revit allows it. Download the current catalog after installation to receive rules added after the installer was built.

Open Guard acts **during RVT model opening**. Dialogs outside that scope, unsupported native windows, unknown failures and unrecoverable files may still require user action. It does not repair ACC login, permission, licensing or connectivity problems. ACC model-opening behavior has not yet been validated as a complete workflow.

## Compatibility and validation

| Revit versions | Release coverage |
| --- | --- |
| 2022, 2023, 2024 | Separate .NET Framework 4.8 builds |
| 2025, 2026 | Separate .NET 8 builds |
| 2027 | .NET 10 build |

Requires 64-bit Windows and the corresponding full Autodesk Revit installation. All six builds compile and pass installer packaging checks. Automatic catalog-update notification was confirmed by the user in **Revit 2024**. Full model-recovery validation across all six hosts, including the .NET 10 migration updates for Revit 2025/2026, remains incomplete. See the [validation checklist](docs/VALIDATION.md) for the tested scope and remaining checks.

## Uninstall

Close Revit, then remove **Revit Open Guard (2022-2027)** through **Windows Settings → Apps → Installed apps**. Your preferences, downloaded catalog and logs are preserved.

## Settings, troubleshooting and development

Settings and logs are stored under `%APPDATA%\MoustafaMagdi\RevitOpenGuard`. Logs remain local and can include model paths; review them before sharing.

For a blocker, report the Revit year, a screenshot, and relevant log lines. For a missing update notification, include the local catalog version shown in **Error Catalog Updates**. Unknown messages should be investigated before adding a recovery rule.

- [Build from source and technical details](docs/DEVELOPMENT.md)
- [Installer details](installer/README.md)
- [Publish signed error-catalog updates](docs/CATALOG_UPDATES.md)
- [Handling catalog and provenance](docs/HANDLING_CATALOG.md)
- [Validation checklist](docs/VALIDATION.md)

Developed by **Mostafa Magdy**. This project is independent of Autodesk.

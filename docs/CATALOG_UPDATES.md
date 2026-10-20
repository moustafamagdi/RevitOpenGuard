# Publishing error catalog updates

Source: `catalog/catalog.json`. Signed envelope: `catalog/catalog.signed.json`.

Live endpoint: https://raw.githubusercontent.com/moustafamagdi/RevitOpenGuard/catalog-updates/catalog.signed.json

The independent `catalog-updates` branch contains only feed data. Publishing does not commit, overwrite or push unrelated changes in the source checkout.

## Developer workflow

1. Reproduce the error and inspect the local Open Guard log. Confirm its dialog/failure ID, available resolution, Revit version and language. Test the intended action on a suitable model.
2. Edit `catalog/catalog.json`. Increase `version` (for example 1.0.0 to 1.0.1), describe the change in `notes`, and edit the rules. Keep `schemaVersion` at 1 and `minToolVersion` at 0.2.0 for this engine.
3. Run `.\scripts\Publish-Catalog.ps1` to sign locally. The supplied private key must match the embedded public key.
4. Run `.\scripts\Test-Catalog.ps1`. Model-specific rules also need a real Revit check. The test executable supports `--verify <signed-file>` to validate a file without applying it.
5. Run `.\scripts\Publish-Catalog.ps1 -Publish`. This validates the signed catalog using the client validator, requires a version increase relative to the current feed, then pushes only catalog data to `catalog-updates` from a temporary independent checkout. It uses your configured Git identity and authentication. It does not force-push; concurrent updates are rejected for retry.
6. With add-in 0.2.1, users are checked on every Revit startup, with daily rechecks during long sessions; 0.2.0 uses the earlier daily startup gate. Manual checks remain available through **Error Catalog Updates > Check for updates > Update**. Rules using supported actions need no new DLL or restart; a new snapshot applies to subsequent model openings. Checks offer the update in a Revit TaskDialog; the user must select **Update error catalog** to apply it.

Withdraw a bad rule by publishing a higher version that corrects/removes it. Clients do not accept a lower version. GitHub raw caching can delay visibility briefly.

## Signing key

The original private signing key is stored locally at `artifacts/keys/catalog-private.xml`.

**Back it up securely. Never commit it, upload it to GitHub, include it in an installer, or share it with users.** The artifacts directory is ignored. `catalog/public-key.xml` is public and embedded in compatible DLLs. Losing the private key requires distributing a new add-in with a replacement trusted public key. A fresh checkout does not contain the private key; use `-PrivateKeyPath` with signing/testing scripts to select its secure backup.

The envelope contains Base64 payload bytes and an RSA PKCS#1 v1.5 SHA-256 signature over the exact payload bytes. Downloads use HTTPS with a 20-second timeout and a 1 MiB limit. The current feed is publicly reachable and does not require user credentials.

## Rule format

Every rule requires a unique `id`, `kind`, `action`, `minRevit` and `maxRevit`. All specified match constraints must match: `allText` requires every phrase; `anyText` requires at least one. Matching normalizes whitespace, case and curly apostrophes. Put exact-ID rules before broad text fallbacks. Add language-specific phrases as needed, or use language-independent failure identifiers.

```json
{
  "id": "Dimension.LinearConstraintNotParallel",
  "kind": "Failure",
  "failureId": "BuiltIn:LinearConstraintNotParallel",
  "action": "DetachElements",
  "minRevit": 2022,
  "maxRevit": 2027
}
```

`failureId` accepts an observed GUID or one of these aliases: `BuiltIn:LinearConstraintNotParallel`, `BuiltIn:RadialDimensionCannotProjectToArc`, `BuiltIn:DimensionPerpendicularToView`. Failure actions: `DetachElements`, `DeleteElements`. Revit must still permit the resolution; repeat-attempt limits remain in force.

Dialog rules use `kind: "Dialog"`, an observed `dialogId` and/or text conditions, and `dialogKind`: `TaskDialog`, `MessageBox` or `Any`. Actions: `Ok`, `Yes`, `Close`, `CommandLink1`; the last two require TaskDialog. MessageBox button layouts are checked before applying a result. No scripts, executable paths, Save, Sync, document-close actions or arbitrary result integers are supported. Unbounded matchers are rejected.

The JSON replaces the whole catalog snapshot, rather than appending patches. Preserve rules unless intentionally withdrawing them. New API behavior or new actions beyond the allowlist require a new add-in release.

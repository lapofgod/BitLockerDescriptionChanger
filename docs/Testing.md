# Testing

## Automated unit tests

The MSTest project covers:

- printable Unicode and the 127 UTF-16-code-unit boundary;
- null, empty, whitespace, control, malformed-surrogate, over-length, and unchanged input;
- boot-sector offsets, bounds, malformed entries, invalid signatures, and inconsistent metadata copies;
- Description, UUID, creation time, encryption method, geometry, and protector invariants;
- backup ZIP manifests, hashes, tamper detection, non-overwrite behavior, and protected ACLs;
- backup-to-volume identity matching and manual Description restoration;
- transaction ordering, pre-commit failure, post-commit rollback, rollback failure, and changed-protector preflight.

Run:

```powershell
dotnet test .\BitLockerDescriptionChanger.sln --configuration Release
```

## Disposable BitLocker VHD integration lab

`tools\Run-IntegrationLab.ps1` requires an administrator PowerShell session. It refuses to use an existing drive letter and writes only to a newly created fixed 256 MiB VHD whose NTFS label must be `BDC_INTEGRATION`. The VHD is dismounted and deleted by default.

Build Release first, then run:

```powershell
.\tools\Run-IntegrationLab.ps1 -DriveLetter R
```

Use `-DotNetPath` when `dotnet.exe` is not on `PATH`. `-KeepVhd` retains the disposable VHD and its backups for manual inspection.

The lab performs:

- read-only discovery of the real operating-system volume;
- ASCII, Unicode, and 127-code-unit changes;
- independent verification of all three raw copies;
- creation-time, UUID, encryption, geometry, offsets, and protector invariance checks;
- invalid-input and unusable-backup-destination checks;
- injected pre-commit, post-commit, and rollback failures;
- arbitrary DLL identity without fixed-hash gating;
- manual Description restore with a new pre-restore backup; and
- lock plus recovery-password unlock followed by another raw/private read-back.

The generated recovery password is kept only in the PowerShell process memory. It is not passed to the test executable, written to backup manifests, or logged.

Never repurpose the integration executable for a real volume. It additionally requires `BDC_INTEGRATION_TEST=1`, rejects operating-system volumes, and checks the exact disposable filesystem label.

# Security and sensitive data

## Private API risk

`FveOpenVolumeW`, `FveGetDescriptionW`, `FveSetDescriptionW`, `FveCommitChanges`, `FveDiscardChanges`, and `FveCloseVolume` are undocumented exports. The application resolves them dynamically from the absolute System32 `fveapi.dll` path. No DLL build is treated as trusted or guaranteed compatible.

There is no fixed version or SHA-256 allowlist. Export presence and successful read-only ABI checks do not prove write compatibility. The current DLL hash is retained only for diagnostics, and the typed experimental-risk warning is required before every change or manual restore.

## Backup sensitivity

A `.bdcbackup` archive contains the physical volume boot sector, three complete FVE metadata blocks, volume identifiers, key protector identifiers and types, and the original Description. It does **not** contain a recovery password, passphrase, PIN, VMK, FVEK, external-key contents, or decrypted user data.

The file is still security-sensitive. It receives a protected Windows DACL granting full control only to the creating user, the local Administrators group, and Local System. Copying it to a filesystem that does not preserve Windows ACLs can remove that protection. Store it like other disk-recovery material.

## Logging

Application logs contain status text, volume identifiers, backup paths, and HRESULT diagnostics. They do not query or record recovery passwords. Newlines are sanitized before writing to the daily log under `%LOCALAPPDATA%\BitLockerDescriptionChanger\Logs`.

The integration lab keeps its generated recovery password only in process memory for the lock/unlock check. PowerShell recovery-password warnings are suppressed so transcripts do not contain that secret.

## Reporting a problem

If a Description change or rollback cannot be verified:

1. Stop making changes to that volume.
2. Preserve the displayed `.bdcbackup` file and application log.
3. Do not attempt to write the raw backup blocks with a general-purpose sector editor.
4. Record the Windows build, `fveapi.dll` version and SHA-256, volume type, and exact diagnostic.
5. Follow [Recovery.md](docs/Recovery.md).

The published binary is unsigned. There is no code-signing certificate in this repository and no claim of publisher identity is made.

# Recovery guidance

## Normal rollback

If post-commit verification finds an unexpected Description or a changed invariant, the application reopens the volume, restores the original Description through `FveSetDescriptionW`, commits, and repeats the complete verification. A verified rollback is reported as **Change rolled back**. Keep the backup and diagnostic even when rollback succeeds.

## Critical rollback failure

If the rollback call or its verification fails, the application:

- disables every further write in the current process;
- leaves the `.bdcbackup` archive in place;
- keeps a persistent, copyable critical diagnostic with the backup path; and
- performs no raw-sector restoration.

Do not reopen the application to continue experimenting on the affected volume.

## Manual Description restore

For a healthy, eligible volume, select it and use **Restore from Backup...**. The application verifies the ZIP hashes and internal raw metadata, confirms that the backup belongs to the selected FVE volume, and creates a new backup of the current state. It then restores only the archived original Description through `FveSetDescriptionW` and performs the same post-write invariant checks.

Manual Description restore is not a raw recovery tool and is disabled when the current volume fails normal write preflight.

## Recommended response

1. Keep the computer powered and avoid BitLocker configuration changes if the volume is still accessible.
2. Copy important data to another protected disk.
3. Confirm that an off-device recovery password or other recovery method is available.
4. Preserve the `.bdcbackup` file and `%LOCALAPPDATA%\BitLockerDescriptionChanger\Logs`.
5. Record the Windows build and the DLL version/hash shown by the application.
6. Use supported Windows BitLocker tools to verify access. For a damaged data volume, consult Microsoft guidance for `repair-bde` or a qualified recovery specialist.

Do not use `dd`, a hex editor, or a generic sector-copy command to restore the archived blocks unless a specialist has validated the exact volume geometry and all intervening BitLocker changes. The archive may be stale as soon as protectors or metadata change.

This application never stores a recovery password. A `.bdcbackup` file is not a substitute for one.

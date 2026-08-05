# Private API and compatibility

## Resolved exports

The application dynamically loads `%SystemRoot%\System32\fveapi.dll` and resolves:

- `FveOpenVolumeW`
- `FveGetDescriptionW`
- `FveSetDescriptionW`
- `FveCommitChanges`
- `FveDiscardChanges`
- `FveCloseVolume`

Read operations open a volume with flag `0`. Description writes open it with flag `1`, set the value, and commit. A set failure before commit requests `FveDiscardChanges`; every returned HRESULT and the close result are checked and included in diagnostics.

These signatures and flags are based on reverse engineering and disposable-volume testing, not a Microsoft API contract. Microsoft can change or remove them in any Windows update.

## Compatibility state

`Experimental` means that the required exports exist. `Unavailable` means the library could not be loaded or an export is missing. There is no verified state and no fixed SHA-256 allowlist.

Export presence is only the first gate. A volume must also pass a read-only ABI test: `FveGetDescriptionW` must return exactly the same UTF-16 Description parsed independently from each of the three raw FVE metadata copies. A successful read is evidence that the getter and parser agree for that volume; it is not proof that the setter ABI is unchanged. The live DLL version and SHA-256 are diagnostic evidence only.

## Description and creation time

The Description is an FVE metadata entry with entry type `7` and value type `2`. It is unencrypted and replicated in all three metadata copies.

The FVE creation time is a separate FILETIME in the metadata header. `FveSetDescriptionW` does not change it, and the application treats any creation-time change as a verification failure. Version 1 does not attempt to modify the creation time and does not expose an API for doing so.

## Raw parser scope

The parser accepts the standard `-FVE-FS-` boot signature, reads three metadata offsets from the boot sector, and requires three 64 KiB blocks with consistent headers and invariants. BitLocker To Go, unknown signatures, malformed sizes, duplicate or out-of-range offsets, and inconsistent copies remain read-only.

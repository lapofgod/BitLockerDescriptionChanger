# `.bdcbackup` format

A backup is a ZIP-compatible archive with the `.bdcbackup` extension. Archives are created with a unique name and are never overwritten.

## Entries

| Entry | Contents |
| --- | --- |
| `volume-boot-sector.bin` | 4096 bytes read from the physical start of the selected volume extent |
| `fve-metadata-1.bin` | First complete 65,536-byte FVE metadata block |
| `fve-metadata-2.bin` | Second complete 65,536-byte FVE metadata block |
| `fve-metadata-3.bin` | Third complete 65,536-byte FVE metadata block |
| `manifest.json` | Identity, geometry, invariants, protector identifiers/types, DLL identity, and file hashes |

The manifest schema version is `1`. It records:

- Application version and UTC creation time
- Volume device ID, drive letter, filesystem label, and original Description
- BitLocker volume UUID and FVE creation time
- Encryption method, metadata version, FVE volume size, boot signature, and all metadata offsets
- Physical disk number, partition offset, and partition length
- Key protector identifiers and numeric types
- `fveapi.dll` path, version, and SHA-256
- Uncompressed length and SHA-256 for each raw binary entry

Recovery passwords and key material are deliberately absent.

## Creation and verification

The application creates a unique temporary file, immediately applies a protected DACL, writes the ZIP, moves it to the final non-existing name, reapplies the DACL, and reopens the archive. Every expected raw entry must have the manifest length and SHA-256, and critical manifest fields must round-trip before a metadata write is allowed.

The DACL grants full control to the creating user, local Administrators, and Local System. ACL protection can be lost if the file is copied to FAT, exFAT, a ZIP, cloud storage, or another destination that does not preserve NTFS security descriptors.

## No raw restore command

The binary blocks are evidence and last-resort recovery material. The GUI intentionally cannot write them back. Blindly restoring FVE metadata can conflict with newer protectors, counters, or geometry and make recovery harder.

**Restore from Backup...** reparses and verifies the archive, matches it to the selected volume, makes a fresh backup of the current state, and restores only `manifest.json`'s `originalDescription` through the same private API. No archived boot sector or metadata block is written to disk.

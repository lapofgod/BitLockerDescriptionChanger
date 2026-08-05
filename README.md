# BitLocker Description Changer

BitLocker Description Changer is a utility that changes the unencrypted FVE Description stored in BitLocker metadata using Windows private APIs.

> [!WARNING]
> **EXPERIMENTAL:** the application calls undocumented exports from `fveapi.dll`. Microsoft does not publish an ABI compatibility contract for these functions. No compatibility or data-safety guarantee is provided. You accept all consequences of use. Mandatory raw backups, conservative volume eligibility checks, read-back verification, and automatic Description rollback only reduce observable risk.

## Why this tool?

BitLocker volumes have a normally-hidden description fields which is fixed during encryption setup. It is normally set to `<Computer name used for creation> <Partition label at creation> <Creation date in computer's locale>`. This label is independent of that of the containing filesystem, and is usually hidden from Windows GUI and command-line interface, but Linux reads it for partition label prior to encryption.

Windows GUI, `manage-bde` and Linux tools do not offer a public, stable API for altering this field. Manually overwriting partition data is possible but fragile due to the fact that:

- There are three redundant copies of metadata blocks in a single volume.
- There are CRC checksums for these metadatas.

However, `fveapi.dll` exposes a private API named `FveSetDescriptionW` which can alter this field and update all three references and checksums. This tool thus utilizes this private API to provide functionality.

## License

This tool is licensed under the Unlicense, because it is 99% AI slop, with the rest 1% being this README.

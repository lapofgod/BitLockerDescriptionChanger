using BitLockerDescriptionChanger.Models;

namespace BitLockerDescriptionChanger.Services;

public static class BackupRestoreValidator
{
    public static string? Validate(
        BitLockerVolumeInfo volume,
        BackupManifest manifest,
        MetadataSnapshot? currentSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(volume);
        ArgumentNullException.ThrowIfNull(manifest);

        if (manifest.SchemaVersion != 1)
        {
            return $"Backup schema version {manifest.SchemaVersion} is not supported.";
        }

        var snapshot = currentSnapshot ?? volume.Snapshot;
        if (snapshot is null || !snapshot.CopiesAreStructurallyConsistent)
        {
            return "The selected volume does not have three consistent current metadata copies.";
        }

        if (!string.Equals(volume.DeviceId, manifest.VolumeDeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return "The backup volume device ID does not match the selected volume.";
        }

        if (snapshot.Primary.VolumeId != manifest.BitLockerVolumeId ||
            snapshot.Primary.CreationTimeUtc != manifest.BitLockerCreationTimeUtc ||
            snapshot.Primary.EncryptionMethod != manifest.EncryptionMethod ||
            snapshot.Primary.MetadataVersion != manifest.MetadataVersion ||
            snapshot.Primary.VolumeSize != manifest.BitLockerVolumeSize ||
            snapshot.PartitionLength != manifest.PartitionLength ||
            !string.Equals(snapshot.BootSignature, manifest.BootSignature, StringComparison.Ordinal) ||
            !snapshot.Primary.MetadataOffsets.SequenceEqual(manifest.MetadataOffsets))
        {
            return "The backup BitLocker identity or metadata geometry does not match the selected volume.";
        }

        return DescriptionValidator.ValidateRestore(manifest.OriginalDescription, volume.Description);
    }
}

using BitLockerDescriptionChanger.Models;

namespace BitLockerDescriptionChanger.Services;

public static class MetadataInvariantVerifier
{
    public static string? Verify(
        MetadataSnapshot before,
        MetadataSnapshot after,
        string expectedDescription,
        string nativeDescription,
        IReadOnlyList<KeyProtectorInfo> beforeProtectors,
        VolumeSecurityState afterSecurityState)
    {
        if (!after.CopiesAreStructurallyConsistent)
        {
            return "The three post-write metadata copies are structurally inconsistent.";
        }

        if (!string.Equals(after.Primary.Description, expectedDescription, StringComparison.Ordinal) ||
            !string.Equals(nativeDescription, expectedDescription, StringComparison.Ordinal))
        {
            return "The raw metadata and private API did not both return the expected Description.";
        }

        if (after.DiskNumber != before.DiskNumber ||
            after.PartitionOffset != before.PartitionOffset ||
            after.PartitionLength != before.PartitionLength)
        {
            return "The volume geometry changed during the transaction.";
        }

        if (after.Primary.VolumeId != before.Primary.VolumeId ||
            after.Primary.CreationTimeUtc != before.Primary.CreationTimeUtc ||
            after.Primary.EncryptionMethod != before.Primary.EncryptionMethod ||
            after.Primary.MetadataVersion != before.Primary.MetadataVersion ||
            after.Primary.VolumeSize != before.Primary.VolumeSize ||
            !after.Primary.MetadataOffsets.SequenceEqual(before.Primary.MetadataOffsets))
        {
            return "A protected BitLocker metadata invariant changed during the Description transaction.";
        }

        if (!afterSecurityState.IsSafeForWrite)
        {
            return "BitLocker lock, conversion, or protection status changed during the transaction.";
        }

        var beforeSet = beforeProtectors
            .Select(protector => $"{protector.Id.ToUpperInvariant()}|{protector.Type}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var afterSet = afterSecurityState.KeyProtectors
            .Select(protector => $"{protector.Id.ToUpperInvariant()}|{protector.Type}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return beforeSet.SequenceEqual(afterSet)
            ? null
            : "The BitLocker key protector identifiers or types changed during the transaction.";
    }
}

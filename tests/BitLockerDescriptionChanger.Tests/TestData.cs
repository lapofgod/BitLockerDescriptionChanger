using System.Buffers.Binary;
using System.Text;
using BitLockerDescriptionChanger.Models;
using BitLockerDescriptionChanger.Services;

namespace BitLockerDescriptionChanger.Tests;

internal static class TestData
{
    internal static readonly Guid VolumeId = new("84cae7dd-3414-4af8-975b-bb2075c7a0f3");
    internal static readonly DateTime CreationTimeUtc = new(2026, 7, 14, 12, 34, 56, DateTimeKind.Utc);
    internal static readonly long[] MetadataOffsets = [1_048_576, 2_097_152, 3_145_728];

    internal static byte[] CreateBootSector(IReadOnlyList<long>? metadataOffsets = null)
    {
        var offsets = metadataOffsets ?? MetadataOffsets;
        var boot = new byte[BitLockerMetadataParser.BootSectorBackupLength];
        Encoding.ASCII.GetBytes("-FVE-FS-").CopyTo(boot, 3);
        for (var index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(boot.AsSpan(176 + (index * 8), 8), checked((ulong)offsets[index]));
        }

        return boot;
    }

    internal static byte[] CreateMetadataBlock(
        string description,
        Guid? volumeId = null,
        DateTime? creationTimeUtc = null,
        ushort encryptionMethod = 6,
        uint metadataVersion = 2,
        ulong volumeSize = 268_435_456,
        IReadOnlyList<long>? metadataOffsets = null)
    {
        var offsets = metadataOffsets ?? MetadataOffsets;
        var data = new byte[BitLockerMetadataParser.MetadataBlockLength];
        Encoding.ASCII.GetBytes("-FVE-FS-").CopyTo(data, 0);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(16, 8), volumeSize);
        for (var index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(32 + (index * 8), 8), checked((ulong)offsets[index]));
        }

        const int metadataHeaderOffset = 64;
        const int entriesOffset = 112;
        var descriptionBytes = Encoding.Unicode.GetBytes(description + "\0");
        var entrySize = checked((ushort)(8 + descriptionBytes.Length));
        var metadataSize = checked((uint)(entriesOffset + entrySize - metadataHeaderOffset));
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(metadataHeaderOffset, 4), metadataSize);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(metadataHeaderOffset + 4, 4), metadataVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(metadataHeaderOffset + 8, 4), 48);
        (volumeId ?? VolumeId).TryWriteBytes(data.AsSpan(metadataHeaderOffset + 16, 16));
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(metadataHeaderOffset + 36, 2), encryptionMethod);
        BinaryPrimitives.WriteInt64LittleEndian(
            data.AsSpan(metadataHeaderOffset + 40, 8),
            (creationTimeUtc ?? CreationTimeUtc).ToFileTimeUtc());

        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(entriesOffset, 2), entrySize);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(entriesOffset + 2, 2), 7);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(entriesOffset + 4, 2), 2);
        descriptionBytes.CopyTo(data, entriesOffset + 8);
        return data;
    }

    internal static MetadataSnapshot CreateSnapshot(
        string description,
        Guid? volumeId = null,
        DateTime? creationTimeUtc = null,
        ushort encryptionMethod = 6,
        uint metadataVersion = 2,
        ulong volumeSize = 268_435_456,
        IReadOnlyList<long>? metadataOffsets = null)
    {
        var offsets = metadataOffsets ?? MetadataOffsets;
        var copies = offsets.Select((offset, index) => BitLockerMetadataParser.ParseMetadataCopy(
            index + 1,
            offset,
            CreateMetadataBlock(description, volumeId, creationTimeUtc, encryptionMethod, metadataVersion, volumeSize, offsets))).ToArray();
        return new MetadataSnapshot(7, 1_048_576, 268_435_456, CreateBootSector(offsets), "-FVE-FS-", copies);
    }

    internal static IReadOnlyList<KeyProtectorInfo> Protectors =>
        [new("{4028324A-19A5-4C50-9866-D7444544E1A8}", 3)];

    internal static VolumeSecurityState SafeSecurityState(IReadOnlyList<KeyProtectorInfo>? protectors = null) =>
        new(0, 1, 1, 6, 1, protectors ?? Protectors);

    internal static BitLockerVolumeInfo CreateVolume(string description = "Original")
    {
        var snapshot = CreateSnapshot(description);
        return new BitLockerVolumeInfo
        {
            DeviceId = @"\\?\Volume{01234567-89ab-cdef-0123-456789abcdef}\",
            DriveLetter = "R:",
            FileSystemLabel = "BDC_TEST",
            Description = description,
            VolumeType = 1,
            EncryptionMethod = 6,
            LockStatus = 0,
            ConversionStatus = 1,
            ProtectionStatus = 1,
            KeyProtectors = Protectors,
            Extent = new DiskExtent(snapshot.DiskNumber, snapshot.PartitionOffset, snapshot.PartitionLength),
            Snapshot = snapshot
        };
    }

    internal static FveApiIdentity ExperimentalIdentity => new(
        @"C:\Windows\System32\fveapi.dll",
        "10.0.99999.1",
        new string('A', 64),
        true,
        FveCompatibilityLevel.Experimental,
        "Test identity");

    internal static BackupManifest CreateManifest(string originalDescription = "Original")
    {
        var snapshot = CreateSnapshot(originalDescription);
        var volume = CreateVolume(originalDescription);
        return new BackupManifest(
            1,
            "1.0.0",
            DateTime.UtcNow,
            volume.DeviceId,
            volume.DriveLetter,
            volume.FileSystemLabel,
            originalDescription,
            snapshot.Primary.VolumeId,
            snapshot.Primary.CreationTimeUtc,
            snapshot.Primary.EncryptionMethod,
            snapshot.Primary.MetadataVersion,
            snapshot.Primary.VolumeSize,
            snapshot.DiskNumber,
            snapshot.PartitionOffset,
            snapshot.PartitionLength,
            snapshot.BootSignature,
            snapshot.Primary.MetadataOffsets,
            volume.KeyProtectors,
            ExperimentalIdentity.FilePath,
            ExperimentalIdentity.FileVersion,
            ExperimentalIdentity.Sha256,
            []);
    }
}

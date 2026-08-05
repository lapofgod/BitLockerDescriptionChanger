namespace BitLockerDescriptionChanger.Models;

public sealed record BackupFileEntry(string EntryName, long Length, string Sha256);

public sealed record BackupManifest(
    int SchemaVersion,
    string ApplicationVersion,
    DateTime CreatedUtc,
    string VolumeDeviceId,
    string DriveLetter,
    string FileSystemLabel,
    string OriginalDescription,
    Guid BitLockerVolumeId,
    DateTime BitLockerCreationTimeUtc,
    ushort EncryptionMethod,
    uint MetadataVersion,
    ulong BitLockerVolumeSize,
    int DiskNumber,
    long PartitionOffset,
    long PartitionLength,
    string BootSignature,
    IReadOnlyList<long> MetadataOffsets,
    IReadOnlyList<KeyProtectorInfo> KeyProtectors,
    string FveApiPath,
    string FveApiVersion,
    string FveApiSha256,
    IReadOnlyList<BackupFileEntry> Files);

public sealed record BackupResult(string FilePath, BackupManifest Manifest);

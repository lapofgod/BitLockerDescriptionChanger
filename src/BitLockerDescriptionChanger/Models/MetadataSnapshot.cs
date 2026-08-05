namespace BitLockerDescriptionChanger.Models;

public sealed record MetadataCopy(
    int Index,
    long RelativeOffset,
    byte[] RawData,
    Guid VolumeId,
    DateTime CreationTimeUtc,
    string Description,
    ushort EncryptionMethod,
    uint MetadataVersion,
    ulong VolumeSize,
    IReadOnlyList<long> MetadataOffsets);

public sealed record MetadataSnapshot(
    int DiskNumber,
    long PartitionOffset,
    long PartitionLength,
    byte[] BootSector,
    string BootSignature,
    IReadOnlyList<MetadataCopy> Copies)
{
    public MetadataCopy Primary => Copies[0];

    public bool CopiesAreStructurallyConsistent => Copies.Count == 3 && Copies.All(copy =>
        copy.VolumeId == Primary.VolumeId &&
        copy.CreationTimeUtc == Primary.CreationTimeUtc &&
        string.Equals(copy.Description, Primary.Description, StringComparison.Ordinal) &&
        copy.EncryptionMethod == Primary.EncryptionMethod &&
        copy.MetadataVersion == Primary.MetadataVersion &&
        copy.VolumeSize == Primary.VolumeSize &&
        copy.MetadataOffsets.SequenceEqual(Primary.MetadataOffsets));
}

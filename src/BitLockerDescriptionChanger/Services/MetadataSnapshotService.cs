using BitLockerDescriptionChanger.Models;

namespace BitLockerDescriptionChanger.Services;

public sealed class MetadataSnapshotService(IPhysicalDiskReader diskReader) : IMetadataSnapshotService
{
    private readonly IPhysicalDiskReader _diskReader = diskReader;

    public MetadataSnapshot Capture(string nativeVolumePath, DiskExtent extent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeVolumePath);
        var bootSector = _diskReader.Read(
            extent.DiskNumber,
            extent.StartingOffset,
            BitLockerMetadataParser.BootSectorBackupLength);
        var metadataOffsets = BitLockerMetadataParser.ParseMetadataOffsets(bootSector);
        var copies = new List<MetadataCopy>(3);

        for (var index = 0; index < metadataOffsets.Count; index++)
        {
            var relativeOffset = metadataOffsets[index];
            if (relativeOffset < 0 || relativeOffset > extent.ExtentLength - BitLockerMetadataParser.MetadataBlockLength)
            {
                throw new BitLockerMetadataFormatException($"Metadata copy {index + 1} falls outside the volume extent.");
            }

            var raw = _diskReader.Read(
                extent.DiskNumber,
                checked(extent.StartingOffset + relativeOffset),
                BitLockerMetadataParser.MetadataBlockLength);
            copies.Add(BitLockerMetadataParser.ParseMetadataCopy(index + 1, relativeOffset, raw));
        }

        var snapshot = new MetadataSnapshot(
            extent.DiskNumber,
            extent.StartingOffset,
            extent.ExtentLength,
            bootSector,
            BitLockerMetadataParser.ReadBootSignature(bootSector),
            copies);
        if (!snapshot.CopiesAreStructurallyConsistent)
        {
            throw new BitLockerMetadataFormatException("The three BitLocker metadata copies are structurally inconsistent.");
        }

        return snapshot;
    }
}

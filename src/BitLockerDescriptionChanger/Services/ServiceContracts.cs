using BitLockerDescriptionChanger.Models;

namespace BitLockerDescriptionChanger.Services;

public interface IFveApi : IDisposable
{
    FveApiIdentity Identity { get; }

    string ReadDescription(string nativeVolumePath);

    NativeWriteResult WriteDescription(string nativeVolumePath, string description);
}

public interface IMetadataSnapshotService
{
    MetadataSnapshot Capture(string nativeVolumePath, DiskExtent extent);
}

public interface IPhysicalDiskReader
{
    byte[] Read(int diskNumber, long absoluteOffset, int length);
}

public interface IBackupService
{
    BackupResult CreateAndVerify(
        string backupDirectory,
        BitLockerVolumeInfo volume,
        MetadataSnapshot snapshot,
        FveApiIdentity identity);
}

public interface IVolumeDiscoveryService
{
    IReadOnlyList<BitLockerVolumeInfo> DiscoverVolumes();
}

public interface IVolumeSecurityStateReader
{
    VolumeSecurityState Read(string deviceId);
}

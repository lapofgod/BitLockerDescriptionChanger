using BitLockerDescriptionChanger.Models;

namespace BitLockerDescriptionChanger.Services;

public sealed class VolumeDiscoveryService(
    WmiBitLockerReader wmiReader,
    DiskExtentReader extentReader,
    IMetadataSnapshotService snapshotService,
    IFveApi fveApi,
    AppLogger logger) : IVolumeDiscoveryService
{
    private readonly WmiBitLockerReader _wmiReader = wmiReader;
    private readonly DiskExtentReader _extentReader = extentReader;
    private readonly IMetadataSnapshotService _snapshotService = snapshotService;
    private readonly IFveApi _fveApi = fveApi;
    private readonly AppLogger _logger = logger;

    public IReadOnlyList<BitLockerVolumeInfo> DiscoverVolumes()
    {
        var volumes = new List<BitLockerVolumeInfo>();
        foreach (var candidate in _wmiReader.ReadAll())
        {
            var volume = new BitLockerVolumeInfo
            {
                DeviceId = candidate.DeviceId,
                DriveLetter = candidate.DriveLetter,
                FileSystemLabel = ReadFileSystemLabel(candidate.DriveLetter),
                Description = string.Empty,
                VolumeType = candidate.State.VolumeType,
                EncryptionMethod = candidate.State.EncryptionMethod,
                LockStatus = candidate.State.LockStatus,
                ConversionStatus = candidate.State.ConversionStatus,
                ProtectionStatus = candidate.State.ProtectionStatus,
                KeyProtectors = candidate.State.KeyProtectors
            };

            try
            {
                volume.Extent = _extentReader.GetSingleExtent(volume.NativeVolumePath);
                volume.Snapshot = _snapshotService.Capture(volume.NativeVolumePath, volume.Extent);
                var nativeDescription = _fveApi.ReadDescription(volume.NativeVolumePath);
                if (!string.Equals(nativeDescription, volume.Snapshot.Primary.Description, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The private API Description does not match the raw metadata Description.");
                }

                volume.Description = nativeDescription;
            }
            catch (Exception exception) when (exception is not AccessViolationException)
            {
                volume.Description = volume.Snapshot?.Primary.Description ?? "Unavailable";
                volume.DiscoveryError = exception.Message;
                _logger.Warning($"Volume discovery preflight failed for {volume.DeviceId}: {exception.Message}");
            }

            volumes.Add(volume);
        }

        return volumes
            .OrderBy(volume => volume.VolumeType == 0 ? 0 : 1)
            .ThenBy(volume => volume.DriveLetter, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ReadFileSystemLabel(string driveLetter)
    {
        if (string.IsNullOrWhiteSpace(driveLetter))
        {
            return string.Empty;
        }

        try
        {
            return new DriveInfo(driveLetter).VolumeLabel;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}

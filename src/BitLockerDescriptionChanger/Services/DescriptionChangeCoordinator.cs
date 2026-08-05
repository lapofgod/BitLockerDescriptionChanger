using BitLockerDescriptionChanger.Models;

namespace BitLockerDescriptionChanger.Services;

public sealed class DescriptionChangeCoordinator(
    IFveApi fveApi,
    IMetadataSnapshotService snapshotService,
    IBackupService backupService,
    IVolumeSecurityStateReader securityStateReader,
    AppLogger logger)
{
    private readonly IFveApi _fveApi = fveApi;
    private readonly IMetadataSnapshotService _snapshotService = snapshotService;
    private readonly IBackupService _backupService = backupService;
    private readonly IVolumeSecurityStateReader _securityStateReader = securityStateReader;
    private readonly AppLogger _logger = logger;

    public DescriptionChangeResult ChangeDescription(
        BitLockerVolumeInfo volume,
        string newDescription,
        string backupDirectory) => ChangeDescriptionCore(volume, newDescription, backupDirectory, null);

    public DescriptionChangeResult RestoreDescription(
        BitLockerVolumeInfo volume,
        BackupManifest restoreManifest,
        string backupDirectory)
    {
        ArgumentNullException.ThrowIfNull(restoreManifest);
        return ChangeDescriptionCore(volume, restoreManifest.OriginalDescription, backupDirectory, restoreManifest);
    }

    private DescriptionChangeResult ChangeDescriptionCore(
        BitLockerVolumeInfo volume,
        string newDescription,
        string backupDirectory,
        BackupManifest? restoreManifest)
    {
        ArgumentNullException.ThrowIfNull(volume);
        var validationError = restoreManifest is null
            ? DescriptionValidator.Validate(newDescription, volume.Description)
            : BackupRestoreValidator.Validate(volume, restoreManifest);
        if (validationError is not null)
        {
            return new DescriptionChangeResult(DescriptionChangeStatus.FailedBeforeCommit, validationError, null, null, null);
        }

        var blockReason = volume.GetWriteBlockReason(_fveApi.Identity);
        if (blockReason is not null)
        {
            return new DescriptionChangeResult(DescriptionChangeStatus.FailedBeforeCommit, blockReason, null, null, null);
        }

        var extent = volume.Extent ?? throw new InvalidOperationException("The selected volume has no disk extent.");
        BackupResult? backup = null;
        NativeWriteResult? write = null;
        try
        {
            var securityBefore = _securityStateReader.Read(volume.DeviceId);
            if (!securityBefore.IsSafeForWrite)
            {
                return new DescriptionChangeResult(
                    DescriptionChangeStatus.FailedBeforeCommit,
                    "BitLocker status changed after discovery; refresh the volume list and try again.",
                    null,
                    null,
                    null);
            }

            var discoveryStateError = CompareDiscoveryState(volume, securityBefore);
            if (discoveryStateError is not null)
            {
                return new DescriptionChangeResult(
                    DescriptionChangeStatus.FailedBeforeCommit,
                    discoveryStateError,
                    null,
                    null,
                    null);
            }

            var before = _snapshotService.Capture(volume.NativeVolumePath, extent);
            if (restoreManifest is not null)
            {
                var restoreError = BackupRestoreValidator.Validate(volume, restoreManifest, before);
                if (restoreError is not null)
                {
                    return new DescriptionChangeResult(
                        DescriptionChangeStatus.FailedBeforeCommit,
                        restoreError,
                        null,
                        null,
                        null);
                }
            }

            var nativeBefore = _fveApi.ReadDescription(volume.NativeVolumePath);
            if (!string.Equals(before.Primary.Description, nativeBefore, StringComparison.Ordinal) ||
                !string.Equals(before.Primary.Description, volume.Description, StringComparison.Ordinal))
            {
                return new DescriptionChangeResult(
                    DescriptionChangeStatus.FailedBeforeCommit,
                    "The Description changed after discovery; refresh the volume list and try again.",
                    null,
                    null,
                    null);
            }

            backup = _backupService.CreateAndVerify(backupDirectory, volume, before, _fveApi.Identity);
            _logger.Information($"Verified a pre-change backup for {volume.DeviceId} at {backup.FilePath}.");

            write = _fveApi.WriteDescription(volume.NativeVolumePath, newDescription);
            var postWrite = TryVerify(volume, extent, before, securityBefore.KeyProtectors, newDescription);
            if (write.Succeeded && postWrite is null)
            {
                volume.Description = newDescription;
                _logger.Information($"Description transaction succeeded for {volume.DeviceId}.");
                return new DescriptionChangeResult(
                    DescriptionChangeStatus.Succeeded,
                    "The Description was changed and all post-write checks passed.",
                    backup.FilePath,
                    write,
                    null);
            }

            if (!write.CommitAttempted && postWrite is not null)
            {
                var unchangedError = TryVerify(volume, extent, before, securityBefore.KeyProtectors, before.Primary.Description);
                if (unchangedError is null)
                {
                    return new DescriptionChangeResult(
                        DescriptionChangeStatus.FailedBeforeCommit,
                        $"The write failed before commit and the original state is intact. {write.Diagnostic}",
                        backup.FilePath,
                        write,
                        null);
                }
            }

            var rollback = _fveApi.WriteDescription(volume.NativeVolumePath, before.Primary.Description);
            var rollbackError = TryVerify(volume, extent, before, securityBefore.KeyProtectors, before.Primary.Description);
            if (rollback.Succeeded && rollbackError is null)
            {
                volume.Description = before.Primary.Description;
                _logger.Warning($"Description verification failed for {volume.DeviceId}; the original Description was restored.");
                return new DescriptionChangeResult(
                    DescriptionChangeStatus.RolledBack,
                    $"The requested change did not pass verification and was rolled back. {postWrite ?? write.Diagnostic}",
                    backup.FilePath,
                    write,
                    rollback);
            }

            _logger.Error($"CRITICAL: automatic rollback failed for {volume.DeviceId}. Backup: {backup.FilePath}.");
            return new DescriptionChangeResult(
                DescriptionChangeStatus.CriticalRollbackFailure,
                $"CRITICAL: post-write verification failed and automatic rollback could not be verified. " +
                $"Do not perform more BitLocker changes. Preserve the backup at {backup.FilePath}. " +
                $"Verification: {postWrite ?? "unknown"}; rollback: {rollbackError ?? rollback.Diagnostic}",
                backup.FilePath,
                write,
                rollback);
        }
        catch (Exception exception) when (exception is not AccessViolationException)
        {
            _logger.Error($"Description transaction failed for {volume.DeviceId}: {exception.Message}");
            return new DescriptionChangeResult(
                write?.CommitAttempted == true ? DescriptionChangeStatus.CriticalRollbackFailure : DescriptionChangeStatus.FailedBeforeCommit,
                exception.Message,
                backup?.FilePath,
                write,
                null);
        }
    }

    private string? TryVerify(
        BitLockerVolumeInfo volume,
        DiskExtent extent,
        MetadataSnapshot before,
        IReadOnlyList<KeyProtectorInfo> beforeProtectors,
        string expectedDescription)
    {
        try
        {
            var after = _snapshotService.Capture(volume.NativeVolumePath, extent);
            var nativeAfter = _fveApi.ReadDescription(volume.NativeVolumePath);
            var securityAfter = _securityStateReader.Read(volume.DeviceId);
            return MetadataInvariantVerifier.Verify(
                before,
                after,
                expectedDescription,
                nativeAfter,
                beforeProtectors,
                securityAfter);
        }
        catch (Exception exception) when (exception is not AccessViolationException)
        {
            return exception.Message;
        }
    }

    private static string? CompareDiscoveryState(BitLockerVolumeInfo volume, VolumeSecurityState current)
    {
        if (volume.LockStatus != current.LockStatus ||
            volume.ConversionStatus != current.ConversionStatus ||
            volume.ProtectionStatus != current.ProtectionStatus ||
            volume.EncryptionMethod != current.EncryptionMethod ||
            volume.VolumeType != current.VolumeType)
        {
            return "BitLocker status changed after discovery; refresh the volume list and try again.";
        }

        var discoveredProtectors = volume.KeyProtectors
            .Select(protector => $"{protector.Id.ToUpperInvariant()}|{protector.Type}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var currentProtectors = current.KeyProtectors
            .Select(protector => $"{protector.Id.ToUpperInvariant()}|{protector.Type}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return discoveredProtectors.SequenceEqual(currentProtectors)
            ? null
            : "BitLocker key protectors changed after discovery; refresh the volume list and try again.";
    }
}

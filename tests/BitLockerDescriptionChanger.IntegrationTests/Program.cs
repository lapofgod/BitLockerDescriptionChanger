using BitLockerDescriptionChanger.Models;
using BitLockerDescriptionChanger.Services;

namespace BitLockerDescriptionChanger.IntegrationTests;

internal static class Program
{
    private const string IntegrationGuardVariable = "BDC_INTEGRATION_TEST";
    private const string RequiredFileSystemLabel = "BDC_INTEGRATION";

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && string.Equals(args[0], "read-only-system-discovery", StringComparison.OrdinalIgnoreCase))
            {
                RunReadOnlySystemDiscovery();
                return 0;
            }

            RequireIntegrationGuard();
            var options = ParseOptions(args);
            return options.Command switch
            {
                "exercise" => RunExercise(options),
                "verify" => RunVerification(options),
                _ => throw new ArgumentException("Command must be 'exercise', 'verify', or 'read-only-system-discovery'.")
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"INTEGRATION TEST FAILED: {exception}");
            return 1;
        }
    }

    private static void RunReadOnlySystemDiscovery()
    {
        using var context = IntegrationContext.Create();
        var systemVolumes = context.Discovery.DiscoverVolumes().Where(volume => volume.VolumeType == 0).ToArray();
        if (systemVolumes.Length == 0)
        {
            throw new InvalidOperationException("WMI did not return an operating-system BitLocker volume.");
        }

        foreach (var volume in systemVolumes)
        {
            Console.WriteLine(
                $"READ-ONLY SYSTEM DISCOVERY: {volume.DisplayDrive}; Description length={volume.Description.Length}; " +
                $"metadata={(volume.Snapshot is null ? "unavailable" : "three copies parsed")}; " +
                $"diagnostic={volume.DiscoveryError ?? "none"}");
        }
    }

    private static int RunExercise(Options options)
    {
        Directory.CreateDirectory(options.BackupDirectory);
        using var context = IntegrationContext.Create();
        var volume = FindDisposableVolume(context, options.Drive);
        var originalDescription = volume.Description;
        var originalSnapshot = volume.Snapshot ?? throw new InvalidOperationException("The disposable volume has no metadata snapshot.");
        var originalProtectors = volume.KeyProtectors.ToArray();

        Console.WriteLine($"fveapi.dll: {context.Identity.FileVersion} {context.Identity.Sha256} ({context.Identity.CompatibilityLevel})");
        VerifySnapshot(originalSnapshot, originalDescription);

        var restoreSource = RunSuccessfulChange(context, volume, "BDC ASCII DESCRIPTION", options.BackupDirectory);
        _ = RunSuccessfulChange(context, volume, "BDC Unicode Caf\u00E9 \U0001F510", options.BackupDirectory);
        _ = RunSuccessfulChange(context, volume, new string('M', DescriptionValidator.MaximumUtf16CodeUnits), options.BackupDirectory);
        VerifyManualRestore(context, volume, restoreSource, options.BackupDirectory);

        VerifyRejectedInputs(volume.Description);
        VerifyBackupFailureStopsWrite(context, volume, options.BackupDirectory);
        VerifyPreCommitFailure(context, volume, options.BackupDirectory);
        VerifyAutomaticRollback(context, volume, options.BackupDirectory);
        VerifyCriticalRollbackFailure(context, volume, options.BackupDirectory);
        VerifyArbitraryDllIdentityCompatibility(context, volume, options.BackupDirectory);
        _ = RunSuccessfulChange(context, volume, options.FinalDescription, options.BackupDirectory);

        var finalSnapshot = context.Snapshots.Capture(volume.NativeVolumePath, RequiredExtent(volume));
        VerifySnapshot(finalSnapshot, options.FinalDescription);
        VerifyProtectedInvariants(originalSnapshot, finalSnapshot, originalProtectors, volume.KeyProtectors);
        Console.WriteLine($"EXERCISE PASSED: final Description is '{options.FinalDescription}'.");
        return 0;
    }

    private static int RunVerification(Options options)
    {
        using var context = IntegrationContext.Create();
        var volume = FindDisposableVolume(context, options.Drive);
        var snapshot = context.Snapshots.Capture(volume.NativeVolumePath, RequiredExtent(volume));
        VerifySnapshot(snapshot, options.FinalDescription);
        if (!string.Equals(context.FveApi.ReadDescription(volume.NativeVolumePath), options.FinalDescription, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The private getter did not return the expected post-unlock Description.");
        }

        Console.WriteLine("POST-UNLOCK VERIFICATION PASSED.");
        return 0;
    }

    private static string RunSuccessfulChange(
        IntegrationContext context,
        BitLockerVolumeInfo volume,
        string description,
        string backupDirectory)
    {
        var before = context.Snapshots.Capture(volume.NativeVolumePath, RequiredExtent(volume));
        var protectors = volume.KeyProtectors.ToArray();
        var result = context.CreateCoordinator(context.FveApi, context.Snapshots)
            .ChangeDescription(volume, description, backupDirectory);
        if (result.Status != DescriptionChangeStatus.Succeeded || result.BackupPath is null)
        {
            throw new InvalidOperationException($"A real Description change failed: {result.Status}; {result.Diagnostic}");
        }

        _ = BackupService.VerifyArchive(result.BackupPath);
        var after = context.Snapshots.Capture(volume.NativeVolumePath, RequiredExtent(volume));
        VerifySnapshot(after, description);
        VerifyProtectedInvariants(before, after, protectors, volume.KeyProtectors);
        Console.WriteLine($"CHANGE PASSED: {description.Length} UTF-16 code units; backup={result.BackupPath}");
        return result.BackupPath;
    }

    private static void VerifyManualRestore(
        IntegrationContext context,
        BitLockerVolumeInfo volume,
        string restoreSource,
        string backupDirectory)
    {
        var manifest = BackupService.VerifyArchive(restoreSource);
        var result = context.CreateCoordinator(context.FveApi, context.Snapshots)
            .RestoreDescription(volume, manifest, backupDirectory);
        if (result.Status != DescriptionChangeStatus.Succeeded || result.BackupPath is null)
        {
            throw new InvalidOperationException($"Manual Description restore failed: {result.Status}; {result.Diagnostic}");
        }

        _ = BackupService.VerifyArchive(result.BackupPath);
        var snapshot = context.Snapshots.Capture(volume.NativeVolumePath, RequiredExtent(volume));
        VerifySnapshot(snapshot, manifest.OriginalDescription);
        Console.WriteLine($"MANUAL DESCRIPTION RESTORE PASSED: pre-restore backup={result.BackupPath}");
    }

    private static void VerifyRejectedInputs(string originalDescription)
    {
        var rejected = new string?[]
        {
            null,
            string.Empty,
            "   ",
            "line\nbreak",
            "embedded\0null",
            new('X', DescriptionValidator.MaximumUtf16CodeUnits + 1),
            originalDescription
        };
        if (rejected.Any(value => DescriptionValidator.Validate(value, originalDescription) is null))
        {
            throw new InvalidOperationException("At least one invalid Description was accepted.");
        }

        Console.WriteLine("REJECTED-INPUT TESTS PASSED.");
    }

    private static void VerifyBackupFailureStopsWrite(
        IntegrationContext context,
        BitLockerVolumeInfo volume,
        string backupDirectory)
    {
        var original = context.FveApi.ReadDescription(volume.NativeVolumePath);
        var invalidDirectory = Path.Combine(backupDirectory, "not-a-directory");
        File.WriteAllText(invalidDirectory, "This file intentionally blocks Directory.CreateDirectory.");
        var result = context.CreateCoordinator(context.FveApi, context.Snapshots)
            .ChangeDescription(volume, "MUST NOT BE WRITTEN", invalidDirectory);
        if (result.Status != DescriptionChangeStatus.FailedBeforeCommit ||
            !string.Equals(context.FveApi.ReadDescription(volume.NativeVolumePath), original, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A backup-destination failure did not stop the write.");
        }

        Console.WriteLine("BACKUP-FAILURE TEST PASSED.");
    }

    private static void VerifyPreCommitFailure(
        IntegrationContext context,
        BitLockerVolumeInfo volume,
        string backupDirectory)
    {
        var original = context.FveApi.ReadDescription(volume.NativeVolumePath);
        using var api = new FailBeforeCommitApi(context.FveApi);
        var result = context.CreateCoordinator(api, context.Snapshots)
            .ChangeDescription(volume, "INJECTED PRE-COMMIT FAILURE", backupDirectory);
        if (result.Status != DescriptionChangeStatus.FailedBeforeCommit ||
            !string.Equals(context.FveApi.ReadDescription(volume.NativeVolumePath), original, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The injected pre-commit failure changed the Description.");
        }

        Console.WriteLine("PRE-COMMIT FAILURE INJECTION PASSED.");
    }

    private static void VerifyAutomaticRollback(
        IntegrationContext context,
        BitLockerVolumeInfo volume,
        string backupDirectory)
    {
        var original = context.FveApi.ReadDescription(volume.NativeVolumePath);
        var snapshots = new InvariantFaultSnapshotService(context.Snapshots, faultCaptureNumber: 2);
        var result = context.CreateCoordinator(context.FveApi, snapshots)
            .ChangeDescription(volume, "INJECTED POST-COMMIT FAILURE", backupDirectory);
        if (result.Status != DescriptionChangeStatus.RolledBack ||
            !string.Equals(context.FveApi.ReadDescription(volume.NativeVolumePath), original, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Automatic rollback injection failed: {result.Status}; {result.Diagnostic}");
        }

        Console.WriteLine("POST-COMMIT FAILURE AND AUTOMATIC ROLLBACK PASSED.");
    }

    private static void VerifyCriticalRollbackFailure(
        IntegrationContext context,
        BitLockerVolumeInfo volume,
        string backupDirectory)
    {
        var original = context.FveApi.ReadDescription(volume.NativeVolumePath);
        using var api = new FailRollbackApi(context.FveApi);
        var snapshots = new InvariantFaultSnapshotService(context.Snapshots, faultCaptureNumber: 2);
        var result = context.CreateCoordinator(api, snapshots)
            .ChangeDescription(volume, "INJECTED ROLLBACK FAILURE", backupDirectory);
        if (result.Status != DescriptionChangeStatus.CriticalRollbackFailure || result.BackupPath is null)
        {
            throw new InvalidOperationException("The injected rollback failure did not produce a persistent critical result.");
        }

        var cleanup = context.FveApi.WriteDescription(volume.NativeVolumePath, original);
        if (!cleanup.Succeeded || !string.Equals(context.FveApi.ReadDescription(volume.NativeVolumePath), original, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Disposable-VHD cleanup could not restore the pre-injection Description.");
        }

        volume.Description = original;
        Console.WriteLine($"ROLLBACK-FAILURE INJECTION PASSED: backup retained at {result.BackupPath}");
    }

    private static void VerifyArbitraryDllIdentityCompatibility(
        IntegrationContext context,
        BitLockerVolumeInfo volume,
        string backupDirectory)
    {
        var simulatedIdentity = context.Identity with
        {
            FileVersion = "0.0.0.0-simulated-unknown",
            CompatibilityLevel = FveCompatibilityLevel.Experimental,
            Diagnostic = "Simulated arbitrary DLL identity for integration testing."
        };
        using var api = new IdentityOverrideApi(context.FveApi, simulatedIdentity);
        var result = context.CreateCoordinator(api, context.Snapshots)
            .ChangeDescription(volume, "ARBITRARY DLL IDENTITY", backupDirectory);
        if (result.Status != DescriptionChangeStatus.Succeeded)
        {
            throw new InvalidOperationException($"The arbitrary DLL identity path failed: {result.Diagnostic}");
        }

        Console.WriteLine("ARBITRARY DLL IDENTITY COMPATIBILITY PATH PASSED.");
    }

    private static void VerifySnapshot(MetadataSnapshot snapshot, string description)
    {
        if (!snapshot.CopiesAreStructurallyConsistent || snapshot.Copies.Count != 3 ||
            snapshot.Copies.Any(copy => !string.Equals(copy.Description, description, StringComparison.Ordinal)))
        {
            throw new InvalidDataException("The three raw metadata copies do not contain the expected Description.");
        }
    }

    private static void VerifyProtectedInvariants(
        MetadataSnapshot before,
        MetadataSnapshot after,
        IReadOnlyList<KeyProtectorInfo> beforeProtectors,
        IReadOnlyList<KeyProtectorInfo> afterProtectors)
    {
        if (before.Primary.VolumeId != after.Primary.VolumeId ||
            before.Primary.CreationTimeUtc != after.Primary.CreationTimeUtc ||
            before.Primary.EncryptionMethod != after.Primary.EncryptionMethod ||
            before.Primary.MetadataVersion != after.Primary.MetadataVersion ||
            before.Primary.VolumeSize != after.Primary.VolumeSize ||
            !before.Primary.MetadataOffsets.SequenceEqual(after.Primary.MetadataOffsets) ||
            before.DiskNumber != after.DiskNumber ||
            before.PartitionOffset != after.PartitionOffset ||
            before.PartitionLength != after.PartitionLength)
        {
            throw new InvalidDataException("A protected raw-metadata or geometry invariant changed.");
        }

        var left = beforeProtectors.Select(NormalizeProtector).OrderBy(value => value, StringComparer.Ordinal);
        var right = afterProtectors.Select(NormalizeProtector).OrderBy(value => value, StringComparer.Ordinal);
        if (!left.SequenceEqual(right))
        {
            throw new InvalidDataException("Key protector identities changed.");
        }
    }

    private static string NormalizeProtector(KeyProtectorInfo protector) =>
        $"{protector.Id.ToUpperInvariant()}|{protector.Type}";

    private static BitLockerVolumeInfo FindDisposableVolume(IntegrationContext context, string drive)
    {
        var normalizedDrive = drive.TrimEnd('\\').TrimEnd(Path.DirectorySeparatorChar);
        var volume = context.Discovery.DiscoverVolumes().SingleOrDefault(candidate =>
            string.Equals(candidate.DriveLetter, normalizedDrive, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"BitLocker volume {normalizedDrive} was not discovered.");
        if (volume.VolumeType == 0 || !string.Equals(volume.FileSystemLabel, RequiredFileSystemLabel, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing to write: {normalizedDrive} is not a non-system volume labeled {RequiredFileSystemLabel}.");
        }

        var blockReason = volume.GetWriteBlockReason(context.Identity);
        if (blockReason is not null)
        {
            throw new InvalidOperationException($"The disposable volume failed write preflight: {blockReason}");
        }

        return volume;
    }

    private static DiskExtent RequiredExtent(BitLockerVolumeInfo volume) =>
        volume.Extent ?? throw new InvalidOperationException("The disposable volume has no supported disk extent.");

    private static void RequireIntegrationGuard()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(IntegrationGuardVariable), "1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Set {IntegrationGuardVariable}=1 to acknowledge that the disposable-volume integration test performs BitLocker writes.");
        }
    }

    private static Options ParseOptions(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException("An integration-test command is required.");
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("Options must use '--name value' pairs.");
            }

            values.Add(args[index][2..], args[index + 1]);
        }

        return new Options(
            args[0].ToLowerInvariant(),
            GetRequired(values, "drive"),
            values.GetValueOrDefault("backup-directory") ?? Path.Combine(Path.GetTempPath(), "BDC-Integration-Backups"),
            GetRequired(values, "final-description"));
    }

    private static string GetRequired(Dictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"--{name} is required.");

    private sealed record Options(string Command, string Drive, string BackupDirectory, string FinalDescription);

    private sealed class IntegrationContext : IDisposable
    {
        private IntegrationContext(
            FveApiIdentity identity,
            IFveApi fveApi,
            WmiBitLockerReader wmi,
            MetadataSnapshotService snapshots,
            VolumeDiscoveryService discovery,
            AppLogger logger)
        {
            Identity = identity;
            FveApi = fveApi;
            Wmi = wmi;
            Snapshots = snapshots;
            Discovery = discovery;
            Logger = logger;
        }

        internal FveApiIdentity Identity { get; }
        internal IFveApi FveApi { get; }
        internal WmiBitLockerReader Wmi { get; }
        internal MetadataSnapshotService Snapshots { get; }
        internal VolumeDiscoveryService Discovery { get; }
        internal AppLogger Logger { get; }

        internal static IntegrationContext Create()
        {
            var identity = FveApiIdentityService.Inspect();
            if (!identity.RequiredExportsAvailable)
            {
                throw new InvalidOperationException(identity.Diagnostic);
            }

            var fveApi = new FveNativeApi(identity);
            var wmi = new WmiBitLockerReader();
            var snapshots = new MetadataSnapshotService(new PhysicalDiskReader());
            var logger = new AppLogger(Path.Combine(Path.GetTempPath(), "BDC-Integration", "integration.log"));
            var discovery = new VolumeDiscoveryService(wmi, new DiskExtentReader(), snapshots, fveApi, logger);
            return new IntegrationContext(identity, fveApi, wmi, snapshots, discovery, logger);
        }

        internal DescriptionChangeCoordinator CreateCoordinator(IFveApi api, IMetadataSnapshotService snapshots) =>
            new(api, snapshots, new BackupService(), Wmi, Logger);

        public void Dispose() => FveApi.Dispose();
    }

    private sealed class FailBeforeCommitApi(IFveApi inner) : DelegatingFveApi(inner)
    {
        public override NativeWriteResult WriteDescription(string nativeVolumePath, string description) => new(
            false,
            0,
            unchecked((int)0x80070057),
            0,
            0,
            0,
            false,
            "Injected failure before commit.");
    }

    private sealed class FailRollbackApi(IFveApi inner) : DelegatingFveApi(inner)
    {
        private int _writeCount;

        public override NativeWriteResult WriteDescription(string nativeVolumePath, string description)
        {
            _writeCount++;
            return _writeCount == 1
                ? Inner.WriteDescription(nativeVolumePath, description)
                : new NativeWriteResult(
                    false,
                    0,
                    0,
                    unchecked((int)0x80004005),
                    0,
                    0,
                    true,
                    "Injected rollback failure.");
        }
    }

    private sealed class IdentityOverrideApi(IFveApi inner, FveApiIdentity identity) : DelegatingFveApi(inner)
    {
        public override FveApiIdentity Identity => identity;
    }

    private abstract class DelegatingFveApi(IFveApi inner) : IFveApi
    {
        protected IFveApi Inner { get; } = inner;

        public virtual FveApiIdentity Identity => Inner.Identity;

        public string ReadDescription(string nativeVolumePath) => Inner.ReadDescription(nativeVolumePath);

        public virtual NativeWriteResult WriteDescription(string nativeVolumePath, string description) =>
            Inner.WriteDescription(nativeVolumePath, description);

        public void Dispose()
        {
            // The integration context owns the underlying API lifetime.
        }
    }

    private sealed class InvariantFaultSnapshotService(
        IMetadataSnapshotService inner,
        int faultCaptureNumber) : IMetadataSnapshotService
    {
        private int _captureCount;

        public MetadataSnapshot Capture(string nativeVolumePath, DiskExtent extent)
        {
            var snapshot = inner.Capture(nativeVolumePath, extent);
            _captureCount++;
            if (_captureCount != faultCaptureNumber)
            {
                return snapshot;
            }

            var changedCopies = snapshot.Copies
                .Select(copy => copy with { CreationTimeUtc = copy.CreationTimeUtc.AddSeconds(1) })
                .ToArray();
            return snapshot with { Copies = changedCopies };
        }
    }
}

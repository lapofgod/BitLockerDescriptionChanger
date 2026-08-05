using BitLockerDescriptionChanger.Models;
using BitLockerDescriptionChanger.Services;

namespace BitLockerDescriptionChanger.Tests;

[TestClass]
public sealed class DescriptionChangeCoordinatorTests
{
    private static readonly string[] ExpectedSuccessEvents =
        ["snapshot", "read", "backup", "write:Updated", "snapshot", "read"];
    private string _testDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "BitLockerDescriptionChanger.CoordinatorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [TestMethod]
    public void SuccessfulTransactionBacksUpBeforeWritingAndVerifiesBothReaders()
    {
        var events = new List<string>();
        var api = new FakeFveApi("Original", events, [SuccessResult()]);
        var snapshots = new FakeSnapshotService(events, TestData.CreateSnapshot("Original"), TestData.CreateSnapshot("Updated"));
        var backup = new FakeBackupService(events, Path.Combine(_testDirectory, "verified.bdcbackup"));
        var coordinator = CreateCoordinator(api, snapshots, backup,
            new FakeSecurityReader(TestData.SafeSecurityState(), TestData.SafeSecurityState()));

        var result = coordinator.ChangeDescription(TestData.CreateVolume(), "Updated", _testDirectory);

        Assert.AreEqual(DescriptionChangeStatus.Succeeded, result.Status);
        CollectionAssert.AreEqual(
            ExpectedSuccessEvents,
            events.Where(value => !value.StartsWith("security", StringComparison.Ordinal)).ToArray());
        Assert.IsLessThan(events.IndexOf("write:Updated"), events.IndexOf("backup"));
    }

    [TestMethod]
    public void PreCommitFailureLeavesOriginalStateAndDoesNotRollbackWrite()
    {
        var events = new List<string>();
        var failed = new NativeWriteResult(false, 0, unchecked((int)0x80070057), 0, 0, 0, false, "Injected set failure");
        var api = new FakeFveApi("Original", events, [failed]);
        var coordinator = CreateCoordinator(
            api,
            new FakeSnapshotService(
                events,
                TestData.CreateSnapshot("Original"),
                TestData.CreateSnapshot("Original"),
                TestData.CreateSnapshot("Original")),
            new FakeBackupService(events, Path.Combine(_testDirectory, "verified.bdcbackup")),
            new FakeSecurityReader(
                TestData.SafeSecurityState(),
                TestData.SafeSecurityState(),
                TestData.SafeSecurityState()));

        var result = coordinator.ChangeDescription(TestData.CreateVolume(), "Updated", _testDirectory);

        Assert.AreEqual(DescriptionChangeStatus.FailedBeforeCommit, result.Status);
        Assert.AreEqual(1, api.WriteCount);
        Assert.AreEqual("Original", api.Description);
    }

    [TestMethod]
    public void PostCommitVerificationFailureRollsBackAndVerifiesOriginal()
    {
        var events = new List<string>();
        var api = new FakeFveApi("Original", events, [SuccessResult(), SuccessResult()]);
        var snapshots = new FakeSnapshotService(
            events,
            TestData.CreateSnapshot("Original"),
            TestData.CreateSnapshot("Updated", creationTimeUtc: TestData.CreationTimeUtc.AddSeconds(1)),
            TestData.CreateSnapshot("Original"));
        var coordinator = CreateCoordinator(
            api,
            snapshots,
            new FakeBackupService(events, Path.Combine(_testDirectory, "verified.bdcbackup")),
            new FakeSecurityReader(TestData.SafeSecurityState(), TestData.SafeSecurityState(), TestData.SafeSecurityState()));

        var result = coordinator.ChangeDescription(TestData.CreateVolume(), "Updated", _testDirectory);

        Assert.AreEqual(DescriptionChangeStatus.RolledBack, result.Status);
        Assert.AreEqual(2, api.WriteCount);
        Assert.AreEqual("Original", api.Description);
        CollectionAssert.Contains(events, "write:Original");
    }

    [TestMethod]
    public void RollbackFailureReturnsCriticalStatusAndPreservesBackupPath()
    {
        var events = new List<string>();
        var rollbackFailure = new NativeWriteResult(false, 0, 0, unchecked((int)0x80004005), 0, 0, true, "Injected rollback failure");
        var api = new FakeFveApi("Original", events, [SuccessResult(), rollbackFailure]);
        var backupPath = Path.Combine(_testDirectory, "verified.bdcbackup");
        var coordinator = CreateCoordinator(
            api,
            new FakeSnapshotService(
                events,
                TestData.CreateSnapshot("Original"),
                TestData.CreateSnapshot("Unexpected"),
                TestData.CreateSnapshot("Unexpected")),
            new FakeBackupService(events, backupPath),
            new FakeSecurityReader(TestData.SafeSecurityState(), TestData.SafeSecurityState(), TestData.SafeSecurityState()));

        var result = coordinator.ChangeDescription(TestData.CreateVolume(), "Updated", _testDirectory);

        Assert.AreEqual(DescriptionChangeStatus.CriticalRollbackFailure, result.Status);
        Assert.AreEqual(backupPath, result.BackupPath);
        StringAssert.Contains(result.Diagnostic, backupPath);
    }

    [TestMethod]
    public void ChangedProtectorStateStopsBeforeBackupOrWrite()
    {
        var events = new List<string>();
        var changedState = TestData.SafeSecurityState([new KeyProtectorInfo("{CHANGED}", 3)]);
        var api = new FakeFveApi("Original", events, [SuccessResult()]);
        var backup = new FakeBackupService(events, Path.Combine(_testDirectory, "unused.bdcbackup"));
        var coordinator = CreateCoordinator(
            api,
            new FakeSnapshotService(events, TestData.CreateSnapshot("Original")),
            backup,
            new FakeSecurityReader(changedState));

        var result = coordinator.ChangeDescription(TestData.CreateVolume(), "Updated", _testDirectory);

        Assert.AreEqual(DescriptionChangeStatus.FailedBeforeCommit, result.Status);
        Assert.AreEqual(0, api.WriteCount);
        Assert.AreEqual(0, backup.CallCount);
    }

    [TestMethod]
    public void ManualRestoreBacksUpCurrentStateBeforeRestoringArchivedDescription()
    {
        var events = new List<string>();
        var api = new FakeFveApi("Current", events, [SuccessResult()]);
        var backup = new FakeBackupService(events, Path.Combine(_testDirectory, "current-state.bdcbackup"));
        var coordinator = CreateCoordinator(
            api,
            new FakeSnapshotService(events, TestData.CreateSnapshot("Current"), TestData.CreateSnapshot("Original")),
            backup,
            new FakeSecurityReader(TestData.SafeSecurityState(), TestData.SafeSecurityState()));

        var result = coordinator.RestoreDescription(
            TestData.CreateVolume("Current"),
            TestData.CreateManifest("Original"),
            _testDirectory);

        Assert.AreEqual(DescriptionChangeStatus.Succeeded, result.Status);
        Assert.AreEqual("Original", api.Description);
        Assert.AreEqual(1, backup.CallCount);
        Assert.IsLessThan(events.IndexOf("write:Original"), events.IndexOf("backup"));
    }

    private DescriptionChangeCoordinator CreateCoordinator(
        IFveApi api,
        IMetadataSnapshotService snapshots,
        IBackupService backup,
        IVolumeSecurityStateReader security) => new(
            api,
            snapshots,
            backup,
            security,
            new AppLogger(Path.Combine(_testDirectory, "test.log")));

    private static NativeWriteResult SuccessResult() => new(true, 0, 0, 0, 0, 0, true, "Success");

    private sealed class FakeFveApi(
        string description,
        List<string> events,
        IReadOnlyList<NativeWriteResult> writeResults) : IFveApi
    {
        private int _writeIndex;

        public FveApiIdentity Identity => TestData.ExperimentalIdentity;

        public string Description { get; private set; } = description;

        public int WriteCount => _writeIndex;

        public string ReadDescription(string nativeVolumePath)
        {
            events.Add("read");
            return Description;
        }

        public NativeWriteResult WriteDescription(string nativeVolumePath, string newDescription)
        {
            events.Add($"write:{newDescription}");
            var result = writeResults[_writeIndex++];
            if (result.Succeeded)
            {
                Description = newDescription;
            }

            return result;
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeSnapshotService(List<string> events, params MetadataSnapshot[] snapshots) : IMetadataSnapshotService
    {
        private int _index;

        public MetadataSnapshot Capture(string nativeVolumePath, DiskExtent extent)
        {
            events.Add("snapshot");
            return snapshots[_index++];
        }
    }

    private sealed class FakeBackupService(List<string> events, string path) : IBackupService
    {
        public int CallCount { get; private set; }

        public BackupResult CreateAndVerify(
            string backupDirectory,
            BitLockerVolumeInfo volume,
            MetadataSnapshot snapshot,
            FveApiIdentity identity)
        {
            events.Add("backup");
            CallCount++;
            return new BackupResult(path, new BackupManifest(
                1, "1.0.0", DateTime.UtcNow, volume.DeviceId, volume.DriveLetter, volume.FileSystemLabel,
                snapshot.Primary.Description, snapshot.Primary.VolumeId, snapshot.Primary.CreationTimeUtc,
                snapshot.Primary.EncryptionMethod, snapshot.Primary.MetadataVersion, snapshot.Primary.VolumeSize, snapshot.DiskNumber,
                snapshot.PartitionOffset, snapshot.PartitionLength, snapshot.BootSignature,
                snapshot.Primary.MetadataOffsets, volume.KeyProtectors, identity.FilePath, identity.FileVersion,
                identity.Sha256, []));
        }
    }

    private sealed class FakeSecurityReader(params VolumeSecurityState[] states) : IVolumeSecurityStateReader
    {
        private int _index;

        public VolumeSecurityState Read(string deviceId) => states[_index++];
    }
}

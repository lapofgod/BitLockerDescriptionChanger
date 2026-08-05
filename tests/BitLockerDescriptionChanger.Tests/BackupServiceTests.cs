using System.IO.Compression;
using System.Security.AccessControl;
using System.Security.Cryptography;
using BitLockerDescriptionChanger.Services;

namespace BitLockerDescriptionChanger.Tests;

[TestClass]
public sealed class BackupServiceTests
{
    private static readonly string[] ExpectedArchiveEntries =
        ["volume-boot-sector.bin", "fve-metadata-1.bin", "fve-metadata-2.bin", "fve-metadata-3.bin", "manifest.json"];
    private string _testDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "BitLockerDescriptionChanger.Tests", Guid.NewGuid().ToString("N"));
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
    public void CreatesAclProtectedHashVerifiedArchiveWithoutRecoverySecrets()
    {
        var result = new BackupService().CreateAndVerify(
            _testDirectory,
            TestData.CreateVolume(),
            TestData.CreateSnapshot("Original"),
            TestData.ExperimentalIdentity);

        Assert.IsTrue(File.Exists(result.FilePath));
        StringAssert.EndsWith(result.FilePath, ".bdcbackup", StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(new FileInfo(result.FilePath).GetAccessControl().AreAccessRulesProtected);

        var manifest = BackupService.VerifyArchive(result.FilePath);
        Assert.AreEqual("Original", manifest.OriginalDescription);
        Assert.HasCount(4, manifest.Files);
        Assert.IsTrue(manifest.Files.All(file => file.Sha256.Length == SHA256.HashSizeInBytes * 2));

        using var archive = ZipFile.OpenRead(result.FilePath);
        CollectionAssert.AreEquivalent(
            ExpectedArchiveEntries,
            archive.Entries.Select(entry => entry.FullName).ToArray());
        using var manifestStream = archive.GetEntry("manifest.json")!.Open();
        using var reader = new StreamReader(manifestStream);
        var json = reader.ReadToEnd();
        Assert.DoesNotContain("RecoveryPassword", json, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void DetectsTamperedBackupEntry()
    {
        var result = new BackupService().CreateAndVerify(
            _testDirectory,
            TestData.CreateVolume(),
            TestData.CreateSnapshot("Original"),
            TestData.ExperimentalIdentity);
        using (var archive = ZipFile.Open(result.FilePath, ZipArchiveMode.Update))
        {
            var entry = archive.GetEntry("fve-metadata-2.bin")!;
            entry.Delete();
            var replacement = archive.CreateEntry("fve-metadata-2.bin", CompressionLevel.NoCompression);
            using var stream = replacement.Open();
            stream.WriteByte(0xFF);
        }

        Assert.ThrowsExactly<InvalidDataException>(() => BackupService.VerifyArchive(result.FilePath));
    }

    [TestMethod]
    public void DoesNotOverwriteExistingBackup()
    {
        var service = new BackupService();
        var first = service.CreateAndVerify(_testDirectory, TestData.CreateVolume(), TestData.CreateSnapshot("Original"), TestData.ExperimentalIdentity);
        var second = service.CreateAndVerify(_testDirectory, TestData.CreateVolume(), TestData.CreateSnapshot("Original"), TestData.ExperimentalIdentity);

        Assert.AreNotEqual(first.FilePath, second.FilePath);
        Assert.IsTrue(File.Exists(first.FilePath));
        Assert.IsTrue(File.Exists(second.FilePath));
    }

    [TestMethod]
    public void RejectsUnexpectedArchiveEntries()
    {
        var result = new BackupService().CreateAndVerify(
            _testDirectory,
            TestData.CreateVolume(),
            TestData.CreateSnapshot("Original"),
            TestData.ExperimentalIdentity);
        using (var archive = ZipFile.Open(result.FilePath, ZipArchiveMode.Update))
        {
            var entry = archive.CreateEntry("unexpected.bin");
            using var stream = entry.Open();
            stream.WriteByte(1);
        }

        Assert.ThrowsExactly<InvalidDataException>(() => BackupService.VerifyArchive(result.FilePath));
    }
}

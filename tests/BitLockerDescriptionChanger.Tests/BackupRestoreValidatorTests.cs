using BitLockerDescriptionChanger.Services;

namespace BitLockerDescriptionChanger.Tests;

[TestClass]
public sealed class BackupRestoreValidatorTests
{
    [TestMethod]
    public void AcceptsMatchingVolumeAndDifferentArchivedDescription()
    {
        var volume = TestData.CreateVolume("Current");
        var manifest = TestData.CreateManifest("Original");

        Assert.IsNull(BackupRestoreValidator.Validate(volume, manifest));
    }

    [TestMethod]
    public void RejectsWrongDeviceIdentityOrMetadataFingerprint()
    {
        var volume = TestData.CreateVolume("Current");
        var wrongDevice = TestData.CreateManifest() with { VolumeDeviceId = @"\\?\Volume{wrong}\" };
        var wrongSize = TestData.CreateManifest() with { BitLockerVolumeSize = 123 };

        Assert.IsNotNull(BackupRestoreValidator.Validate(volume, wrongDevice));
        Assert.IsNotNull(BackupRestoreValidator.Validate(volume, wrongSize));
    }

    [TestMethod]
    public void RejectsDescriptionThatIsAlreadyActive()
    {
        var volume = TestData.CreateVolume("Current");
        var manifest = TestData.CreateManifest("Current");

        Assert.IsNotNull(BackupRestoreValidator.Validate(volume, manifest));
    }
}

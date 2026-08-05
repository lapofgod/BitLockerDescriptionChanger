using BitLockerDescriptionChanger.Models;
using BitLockerDescriptionChanger.Services;

namespace BitLockerDescriptionChanger.Tests;

[TestClass]
public sealed class MetadataInvariantVerifierTests
{
    [TestMethod]
    public void AcceptsDescriptionOnlyChange()
    {
        var result = MetadataInvariantVerifier.Verify(
            TestData.CreateSnapshot("Original"),
            TestData.CreateSnapshot("Updated"),
            "Updated",
            "Updated",
            TestData.Protectors,
            TestData.SafeSecurityState());

        Assert.IsNull(result);
    }

    [TestMethod]
    public void RejectsPrivateGetterMismatchAndInvariantChanges()
    {
        var before = TestData.CreateSnapshot("Original");
        Assert.IsNotNull(MetadataInvariantVerifier.Verify(
            before,
            TestData.CreateSnapshot("Updated"),
            "Updated",
            "Other",
            TestData.Protectors,
            TestData.SafeSecurityState()));

        Assert.IsNotNull(MetadataInvariantVerifier.Verify(
            before,
            TestData.CreateSnapshot("Updated", creationTimeUtc: TestData.CreationTimeUtc.AddSeconds(1)),
            "Updated",
            "Updated",
            TestData.Protectors,
            TestData.SafeSecurityState()));
    }

    [TestMethod]
    public void RejectsGeometryAndProtectorChanges()
    {
        var before = TestData.CreateSnapshot("Original");
        var geometryChanged = TestData.CreateSnapshot("Updated") with { PartitionLength = before.PartitionLength - 1 };
        Assert.IsNotNull(MetadataInvariantVerifier.Verify(
            before,
            geometryChanged,
            "Updated",
            "Updated",
            TestData.Protectors,
            TestData.SafeSecurityState()));

        var changedProtectors = new[] { new KeyProtectorInfo("{DIFFERENT}", 3) };
        Assert.IsNotNull(MetadataInvariantVerifier.Verify(
            before,
            TestData.CreateSnapshot("Updated"),
            "Updated",
            "Updated",
            TestData.Protectors,
            TestData.SafeSecurityState(changedProtectors)));
    }
}

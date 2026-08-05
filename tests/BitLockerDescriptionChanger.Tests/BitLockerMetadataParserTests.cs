using System.Buffers.Binary;
using System.Text;
using BitLockerDescriptionChanger.Models;
using BitLockerDescriptionChanger.Services;

namespace BitLockerDescriptionChanger.Tests;

[TestClass]
public sealed class BitLockerMetadataParserTests
{
    [TestMethod]
    public void ParsesBootOffsetsAndUnicodeDescription()
    {
        var offsets = BitLockerMetadataParser.ParseMetadataOffsets(TestData.CreateBootSector());
        CollectionAssert.AreEqual(TestData.MetadataOffsets, offsets.ToArray());

        var copy = BitLockerMetadataParser.ParseMetadataCopy(
            1,
            offsets[0],
            TestData.CreateMetadataBlock("Caf\u00E9 archive \U0001F510"));
        Assert.AreEqual("Caf\u00E9 archive \U0001F510", copy.Description);
        Assert.AreEqual(TestData.VolumeId, copy.VolumeId);
        Assert.AreEqual(TestData.CreationTimeUtc, copy.CreationTimeUtc);
        Assert.AreEqual((ushort)6, copy.EncryptionMethod);
    }

    [TestMethod]
    public void RejectsDuplicateAndOutOfRangeMetadataOffsets()
    {
        var duplicate = TestData.CreateBootSector([4096, 4096, 8192]);
        Assert.ThrowsExactly<BitLockerMetadataFormatException>(() => BitLockerMetadataParser.ParseMetadataOffsets(duplicate));

        var outOfRange = TestData.CreateBootSector();
        BinaryPrimitives.WriteUInt64LittleEndian(outOfRange.AsSpan(176, 8), ulong.MaxValue);
        Assert.ThrowsExactly<BitLockerMetadataFormatException>(() => BitLockerMetadataParser.ParseMetadataOffsets(outOfRange));
    }

    [TestMethod]
    public void RejectsMalformedEntriesAndInvalidHeaders()
    {
        var malformed = TestData.CreateMetadataBlock("Original");
        BinaryPrimitives.WriteUInt16LittleEndian(malformed.AsSpan(112, 2), 7);
        Assert.ThrowsExactly<BitLockerMetadataFormatException>(() =>
            BitLockerMetadataParser.ParseMetadataCopy(1, TestData.MetadataOffsets[0], malformed));

        var oddLength = TestData.CreateMetadataBlock("Original");
        BinaryPrimitives.WriteUInt16LittleEndian(oddLength.AsSpan(112, 2), 9);
        BinaryPrimitives.WriteUInt32LittleEndian(oddLength.AsSpan(64, 4), 57);
        Assert.ThrowsExactly<BitLockerMetadataFormatException>(() =>
            BitLockerMetadataParser.ParseMetadataCopy(1, TestData.MetadataOffsets[0], oddLength));

        var invalidSignature = TestData.CreateMetadataBlock("Original");
        Encoding.ASCII.GetBytes("BAD-FS!!").CopyTo(invalidSignature, 0);
        Assert.ThrowsExactly<BitLockerMetadataFormatException>(() =>
            BitLockerMetadataParser.ParseMetadataCopy(1, TestData.MetadataOffsets[0], invalidSignature));
    }

    [TestMethod]
    public void DetectsInconsistentCopies()
    {
        var consistent = TestData.CreateSnapshot("Original");
        var inconsistentCopies = consistent.Copies.ToArray();
        inconsistentCopies[2] = BitLockerMetadataParser.ParseMetadataCopy(
            3,
            TestData.MetadataOffsets[2],
            TestData.CreateMetadataBlock("Different"));
        var inconsistent = consistent with { Copies = inconsistentCopies };

        Assert.IsTrue(consistent.CopiesAreStructurallyConsistent);
        Assert.IsFalse(inconsistent.CopiesAreStructurallyConsistent);
    }

    [TestMethod]
    public void SnapshotServiceRejectsMetadataOutsideExtentBeforeReadingIt()
    {
        var reader = new MemoryDiskReader(TestData.CreateBootSector([999_999, 1_999_999, 2_999_999]));
        var service = new MetadataSnapshotService(reader);

        Assert.ThrowsExactly<BitLockerMetadataFormatException>(() =>
            service.Capture(@"\\?\Volume{test}", new DiskExtent(1, 0, 131_072)));
        Assert.AreEqual(1, reader.ReadCount);
    }

    private sealed class MemoryDiskReader(byte[] bootSector) : IPhysicalDiskReader
    {
        public int ReadCount { get; private set; }

        public byte[] Read(int diskNumber, long absoluteOffset, int length)
        {
            ReadCount++;
            return bootSector;
        }
    }
}

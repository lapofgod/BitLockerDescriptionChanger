using System.Buffers.Binary;
using System.Text;
using BitLockerDescriptionChanger.Models;

namespace BitLockerDescriptionChanger.Services;

public sealed class BitLockerMetadataFormatException : IOException
{
    public BitLockerMetadataFormatException(string message) : base(message)
    {
    }
}

public static class BitLockerMetadataParser
{
    public const int BootSectorBackupLength = 4096;
    public const int MetadataBlockLength = 65536;
    private const int MetadataHeaderOffset = 64;
    private const int MetadataEntriesOffset = 112;

    public static IReadOnlyList<long> ParseMetadataOffsets(ReadOnlySpan<byte> bootSector)
    {
        if (bootSector.Length < 512)
        {
            throw new BitLockerMetadataFormatException("The BitLocker boot sector is shorter than 512 bytes.");
        }

        var signature = Encoding.ASCII.GetString(bootSector.Slice(3, 8));
        if (!string.Equals(signature, "-FVE-FS-", StringComparison.Ordinal))
        {
            var toGoSignature = Encoding.ASCII.GetString(bootSector.Slice(3, 8));
            if (string.Equals(toGoSignature, "MSWIN4.1", StringComparison.Ordinal))
            {
                throw new BitLockerMetadataFormatException("BitLocker To Go boot-sector geometry is visible but intentionally read-only in version 1.");
            }

            throw new BitLockerMetadataFormatException($"Unsupported boot signature '{SanitizeSignature(signature)}'.");
        }

        var offsets = new long[3];
        for (var index = 0; index < offsets.Length; index++)
        {
            var value = BinaryPrimitives.ReadUInt64LittleEndian(bootSector.Slice(176 + (index * 8), 8));
            if (value == 0 || value > long.MaxValue)
            {
                throw new BitLockerMetadataFormatException($"Metadata copy {index + 1} has an invalid offset.");
            }

            offsets[index] = (long)value;
        }

        if (offsets.Distinct().Count() != 3)
        {
            throw new BitLockerMetadataFormatException("The three metadata offsets are not unique.");
        }

        return offsets;
    }

    public static MetadataCopy ParseMetadataCopy(int index, long relativeOffset, byte[] rawData)
    {
        ArgumentNullException.ThrowIfNull(rawData);
        if (rawData.Length != MetadataBlockLength)
        {
            throw new BitLockerMetadataFormatException($"Metadata copy {index} is not {MetadataBlockLength} bytes long.");
        }

        var data = rawData.AsSpan();
        var signature = Encoding.ASCII.GetString(data[..8]);
        if (!string.Equals(signature, "-FVE-FS-", StringComparison.Ordinal))
        {
            throw new BitLockerMetadataFormatException($"Metadata copy {index} has an invalid signature.");
        }

        var metadataSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(MetadataHeaderOffset, 4));
        var metadataVersion = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(MetadataHeaderOffset + 4, 4));
        var headerSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(MetadataHeaderOffset + 8, 4));
        if (headerSize != 48 || metadataSize < 48 || MetadataHeaderOffset + metadataSize > rawData.Length)
        {
            throw new BitLockerMetadataFormatException($"Metadata copy {index} has invalid header sizes.");
        }

        var volumeId = new Guid(data.Slice(MetadataHeaderOffset + 16, 16));
        var encryptionMethod = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(MetadataHeaderOffset + 36, 2));
        var fileTime = BinaryPrimitives.ReadInt64LittleEndian(data.Slice(MetadataHeaderOffset + 40, 8));
        DateTime creationTime;
        try
        {
            creationTime = DateTime.FromFileTimeUtc(fileTime);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new BitLockerMetadataFormatException($"Metadata copy {index} has an invalid creation FILETIME: {exception.Message}");
        }

        var volumeSize = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(16, 8));
        var offsets = new long[3];
        for (var offsetIndex = 0; offsetIndex < offsets.Length; offsetIndex++)
        {
            var offset = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(32 + (offsetIndex * 8), 8));
            if (offset > long.MaxValue)
            {
                throw new BitLockerMetadataFormatException($"Metadata copy {index} contains an out-of-range metadata offset.");
            }

            offsets[offsetIndex] = (long)offset;
        }

        var description = FindDescription(data, checked(MetadataHeaderOffset + (int)metadataSize), index);
        return new MetadataCopy(
            index,
            relativeOffset,
            rawData,
            volumeId,
            creationTime,
            description,
            encryptionMethod,
            metadataVersion,
            volumeSize,
            offsets);
    }

    public static string ReadBootSignature(ReadOnlySpan<byte> bootSector) =>
        bootSector.Length >= 11 ? Encoding.ASCII.GetString(bootSector.Slice(3, 8)) : string.Empty;

    private static string FindDescription(ReadOnlySpan<byte> data, int end, int copyIndex)
    {
        var position = MetadataEntriesOffset;
        while (position + 8 <= end)
        {
            var entrySize = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(position, 2));
            if (entrySize == 0)
            {
                break;
            }

            if (entrySize < 8 || position + entrySize > end)
            {
                throw new BitLockerMetadataFormatException($"Metadata copy {copyIndex} contains a malformed entry at offset {position}.");
            }

            var entryType = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(position + 2, 2));
            var valueType = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(position + 4, 2));
            if (entryType == 7 && valueType == 2)
            {
                var byteLength = entrySize - 8;
                if ((byteLength & 1) != 0)
                {
                    throw new BitLockerMetadataFormatException($"Metadata copy {copyIndex} contains an odd-length UTF-16 description.");
                }

                return Encoding.Unicode.GetString(data.Slice(position + 8, byteLength)).TrimEnd('\0');
            }

            position += entrySize;
        }

        throw new BitLockerMetadataFormatException($"Metadata copy {copyIndex} does not contain a Description entry.");
    }

    private static string SanitizeSignature(string signature) =>
        new(signature.Select(character => char.IsControl(character) ? '?' : character).ToArray());
}

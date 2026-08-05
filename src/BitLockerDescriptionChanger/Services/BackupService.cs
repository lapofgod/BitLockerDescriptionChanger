using System.IO.Compression;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text.Json;
using BitLockerDescriptionChanger.Models;

namespace BitLockerDescriptionChanger.Services;

public sealed class BackupService : IBackupService
{
    private const long MaximumManifestLength = 1_048_576;
    private static readonly IReadOnlyDictionary<string, long> RequiredRawEntries =
        new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["volume-boot-sector.bin"] = BitLockerMetadataParser.BootSectorBackupLength,
            ["fve-metadata-1.bin"] = BitLockerMetadataParser.MetadataBlockLength,
            ["fve-metadata-2.bin"] = BitLockerMetadataParser.MetadataBlockLength,
            ["fve-metadata-3.bin"] = BitLockerMetadataParser.MetadataBlockLength
        };
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string DefaultBackupDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "BitLockerDescriptionChanger",
        "Backups");

    public BackupResult CreateAndVerify(
        string backupDirectory,
        BitLockerVolumeInfo volume,
        MetadataSnapshot snapshot,
        FveApiIdentity identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectory);
        Directory.CreateDirectory(backupDirectory);

        var timestamp = DateTime.UtcNow;
        var safeDrive = string.IsNullOrWhiteSpace(volume.DriveLetter)
            ? "NO_DRIVE"
            : volume.DriveLetter.Replace(":", string.Empty, StringComparison.Ordinal);
        var fileName = $"{safeDrive}_{snapshot.Primary.VolumeId:N}_{timestamp:yyyyMMddTHHmmssfffffffZ}.bdcbackup";
        var finalPath = Path.Combine(backupDirectory, fileName);
        var temporaryPath = Path.Combine(backupDirectory, $".{Guid.NewGuid():N}.tmp");
        var finalCreated = false;

        try
        {
            var files = new List<(string Name, byte[] Data)>
            {
                ("volume-boot-sector.bin", snapshot.BootSector)
            };
            files.AddRange(snapshot.Copies.Select(copy => ($"fve-metadata-{copy.Index}.bin", copy.RawData)));
            var fileEntries = files.Select(file => new BackupFileEntry(
                file.Name,
                file.Data.LongLength,
                Convert.ToHexString(SHA256.HashData(file.Data)))).ToArray();

            var manifest = new BackupManifest(
                1,
                Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
                timestamp,
                volume.DeviceId,
                volume.DriveLetter,
                volume.FileSystemLabel,
                snapshot.Primary.Description,
                snapshot.Primary.VolumeId,
                snapshot.Primary.CreationTimeUtc,
                snapshot.Primary.EncryptionMethod,
                snapshot.Primary.MetadataVersion,
                snapshot.Primary.VolumeSize,
                snapshot.DiskNumber,
                snapshot.PartitionOffset,
                snapshot.PartitionLength,
                snapshot.BootSignature,
                snapshot.Primary.MetadataOffsets,
                volume.KeyProtectors,
                identity.FilePath,
                identity.FileVersion,
                identity.Sha256,
                fileEntries);

            using (File.Open(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
            }

            ApplyRestrictiveAcl(temporaryPath);
            using (var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Update))
            {
                foreach (var file in files)
                {
                    var entry = archive.CreateEntry(file.Name, CompressionLevel.NoCompression);
                    using var stream = entry.Open();
                    stream.Write(file.Data);
                }

                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using var manifestStream = manifestEntry.Open();
                JsonSerializer.Serialize(manifestStream, manifest, JsonOptions);
            }

            File.Move(temporaryPath, finalPath, false);
            finalCreated = true;
            ApplyRestrictiveAcl(finalPath);
            VerifyArchive(finalPath, manifest);
            return new BackupResult(finalPath, manifest);
        }
        catch
        {
            TryDelete(temporaryPath);
            if (finalCreated)
            {
                TryDelete(finalPath);
            }

            throw;
        }
    }

    public static BackupManifest VerifyArchive(string backupPath)
    {
        using var archive = ZipFile.OpenRead(backupPath);
        var manifestEntry = archive.GetEntry("manifest.json")
            ?? throw new InvalidDataException("The backup archive does not contain manifest.json.");
        if (manifestEntry.Length <= 0 || manifestEntry.Length > MaximumManifestLength)
        {
            throw new InvalidDataException("The backup manifest has an invalid length.");
        }
        BackupManifest manifest;
        using (var stream = manifestEntry.Open())
        {
            manifest = JsonSerializer.Deserialize<BackupManifest>(stream, JsonOptions)
                ?? throw new InvalidDataException("The backup manifest is invalid.");
        }

        VerifyArchiveEntries(archive, manifest);
        VerifyArchivedMetadata(archive, manifest);
        return manifest;
    }

    private static void VerifyArchive(string backupPath, BackupManifest expectedManifest)
    {
        var actualManifest = VerifyArchive(backupPath);
        if (actualManifest.SchemaVersion != expectedManifest.SchemaVersion ||
            actualManifest.BitLockerVolumeId != expectedManifest.BitLockerVolumeId ||
            !string.Equals(actualManifest.OriginalDescription, expectedManifest.OriginalDescription, StringComparison.Ordinal) ||
            !actualManifest.Files.SequenceEqual(expectedManifest.Files))
        {
            throw new InvalidDataException("The backup manifest failed its read-back comparison.");
        }
    }

    private static void VerifyArchiveEntries(ZipArchive archive, BackupManifest manifest)
    {
        var archiveNames = archive.Entries.Select(entry => entry.FullName).ToArray();
        var requiredNames = RequiredRawEntries.Keys.Append("manifest.json").ToArray();
        if (archiveNames.Length != requiredNames.Length ||
            archiveNames.Distinct(StringComparer.Ordinal).Count() != archiveNames.Length ||
            !archiveNames.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(requiredNames.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidDataException("The backup archive contains missing, duplicate, or unexpected entries.");
        }

        if (manifest.Files.Count != RequiredRawEntries.Count ||
            manifest.Files.Select(file => file.EntryName).Distinct(StringComparer.Ordinal).Count() != manifest.Files.Count)
        {
            throw new InvalidDataException("The backup manifest file list is incomplete or contains duplicates.");
        }

        foreach (var expected in manifest.Files)
        {
            if (!RequiredRawEntries.TryGetValue(expected.EntryName, out var requiredLength) || expected.Length != requiredLength)
            {
                throw new InvalidDataException($"The backup manifest contains an invalid entry or length for {expected.EntryName}.");
            }

            var entry = archive.GetEntry(expected.EntryName)
                ?? throw new InvalidDataException($"The backup archive is missing {expected.EntryName}.");
            using var stream = entry.Open();
            var hash = Convert.ToHexString(SHA256.HashData(stream));
            if (entry.Length != expected.Length || !string.Equals(hash, expected.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"The backup entry {expected.EntryName} failed length or SHA-256 verification.");
            }
        }
    }

    private static void VerifyArchivedMetadata(ZipArchive archive, BackupManifest manifest)
    {
        if (manifest.MetadataOffsets.Count != 3)
        {
            throw new InvalidDataException("The backup manifest does not contain exactly three metadata offsets.");
        }

        var bootEntry = archive.GetEntry("volume-boot-sector.bin")
            ?? throw new InvalidDataException("The backup archive is missing volume-boot-sector.bin.");
        byte[] bootSector;
        using (var stream = bootEntry.Open())
        using (var memory = new MemoryStream())
        {
            stream.CopyTo(memory);
            bootSector = memory.ToArray();
        }

        var bootOffsets = BitLockerMetadataParser.ParseMetadataOffsets(bootSector);
        if (!bootOffsets.SequenceEqual(manifest.MetadataOffsets) ||
            !string.Equals(BitLockerMetadataParser.ReadBootSignature(bootSector), manifest.BootSignature, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The archived boot sector does not match the backup manifest.");
        }

        var copies = new List<MetadataCopy>(3);
        for (var index = 0; index < 3; index++)
        {
            var entryName = $"fve-metadata-{index + 1}.bin";
            var entry = archive.GetEntry(entryName)
                ?? throw new InvalidDataException($"The backup archive is missing {entryName}.");
            byte[] data;
            using (var stream = entry.Open())
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                data = memory.ToArray();
            }

            copies.Add(BitLockerMetadataParser.ParseMetadataCopy(index + 1, manifest.MetadataOffsets[index], data));
        }

        var snapshot = new MetadataSnapshot(
            manifest.DiskNumber,
            manifest.PartitionOffset,
            manifest.PartitionLength,
            bootSector,
            manifest.BootSignature,
            copies);
        if (!snapshot.CopiesAreStructurallyConsistent ||
            snapshot.Primary.VolumeId != manifest.BitLockerVolumeId ||
            snapshot.Primary.CreationTimeUtc != manifest.BitLockerCreationTimeUtc ||
            !string.Equals(snapshot.Primary.Description, manifest.OriginalDescription, StringComparison.Ordinal) ||
            snapshot.Primary.EncryptionMethod != manifest.EncryptionMethod ||
            snapshot.Primary.MetadataVersion != manifest.MetadataVersion ||
            snapshot.Primary.VolumeSize != manifest.BitLockerVolumeSize ||
            !snapshot.Primary.MetadataOffsets.SequenceEqual(manifest.MetadataOffsets))
        {
            throw new InvalidDataException("The archived FVE metadata does not match the backup manifest.");
        }
    }

    private static void ApplyRestrictiveAcl(string filePath)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new InvalidOperationException("The current Windows user SID is unavailable.");
        var security = new FileSecurity();
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        FileSystemAclExtensions.SetAccessControl(new FileInfo(filePath), security);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Preserve the original failure; a leftover temporary file contains only the current backup attempt.
        }
    }
}

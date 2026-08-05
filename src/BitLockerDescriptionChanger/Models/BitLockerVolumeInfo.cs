namespace BitLockerDescriptionChanger.Models;

public sealed class BitLockerVolumeInfo
{
    public required string DeviceId { get; init; }

    public required string DriveLetter { get; init; }

    public required string FileSystemLabel { get; init; }

    public required string Description { get; set; }

    public required uint VolumeType { get; init; }

    public required uint EncryptionMethod { get; init; }

    public required uint LockStatus { get; init; }

    public required uint ConversionStatus { get; init; }

    public required uint ProtectionStatus { get; init; }

    public required IReadOnlyList<KeyProtectorInfo> KeyProtectors { get; init; }

    public DiskExtent? Extent { get; set; }

    public MetadataSnapshot? Snapshot { get; set; }

    public string? DiscoveryError { get; set; }

    public string NativeVolumePath => DeviceId.TrimEnd('\\');

    public bool HasRecoveryCapableProtector => KeyProtectors.Any(protector => protector.IsRecoveryCapable);

    public string DisplayDrive => string.IsNullOrWhiteSpace(DriveLetter) ? "(No drive letter)" : DriveLetter;

    public string VolumeTypeName => VolumeType switch
    {
        0 => "Operating system",
        1 => "Fixed data",
        2 => "Removable data",
        _ => $"Unknown ({VolumeType})"
    };

    public string EncryptionMethodName => EncryptionMethod switch
    {
        0 => "None",
        1 => "AES 128 with diffuser",
        2 => "AES 256 with diffuser",
        3 => "AES 128",
        4 => "AES 256",
        5 => "Hardware encryption",
        6 => "XTS-AES 128",
        7 => "XTS-AES 256",
        _ => $"Unknown ({EncryptionMethod})"
    };

    public string LockStatusName => LockStatus switch
    {
        0 => "Unlocked",
        1 => "Locked",
        _ => "Unknown"
    };

    public string ConversionStatusName => ConversionStatus switch
    {
        0 => "Fully decrypted",
        1 => "Fully encrypted",
        2 => "Encrypting",
        3 => "Decrypting",
        4 => "Encryption paused",
        5 => "Decryption paused",
        _ => $"Unknown ({ConversionStatus})"
    };

    public string ProtectionStatusName => ProtectionStatus switch
    {
        0 => "Off",
        1 => "On",
        2 => "Unknown",
        _ => $"Unknown ({ProtectionStatus})"
    };

    public string? GetWriteBlockReason(FveApiIdentity identity)
    {
        if (!identity.CanAttemptWrite)
        {
            return identity.Diagnostic;
        }

        if (!string.IsNullOrWhiteSpace(DiscoveryError))
        {
            return DiscoveryError;
        }

        if (LockStatus != 0)
        {
            return "The volume must be unlocked.";
        }

        if (ConversionStatus != 1)
        {
            return "The volume must be fully encrypted and not converting.";
        }

        if (ProtectionStatus != 1)
        {
            return "BitLocker protection must be enabled and not suspended.";
        }

        if (Extent is null)
        {
            return "The volume does not have exactly one supported disk extent.";
        }

        if (Snapshot is null || !Snapshot.CopiesAreStructurallyConsistent)
        {
            return "The three BitLocker metadata copies are not readable and structurally consistent.";
        }

        if (!HasRecoveryCapableProtector)
        {
            return "No recovery-capable key protector was detected.";
        }

        return null;
    }
}

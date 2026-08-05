namespace BitLockerDescriptionChanger.Models;

public sealed record VolumeSecurityState(
    uint LockStatus,
    uint ConversionStatus,
    uint ProtectionStatus,
    uint EncryptionMethod,
    uint VolumeType,
    IReadOnlyList<KeyProtectorInfo> KeyProtectors)
{
    public bool IsSafeForWrite => LockStatus == 0 && ConversionStatus == 1 && ProtectionStatus == 1;
}

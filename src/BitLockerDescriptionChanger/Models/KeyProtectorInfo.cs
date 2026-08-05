namespace BitLockerDescriptionChanger.Models;

public sealed record KeyProtectorInfo(string Id, uint Type)
{
    public string TypeName => Type switch
    {
        1 => "TPM",
        2 => "External key",
        3 => "Numerical password",
        4 => "TPM and PIN",
        5 => "TPM and startup key",
        6 => "TPM, PIN, and startup key",
        7 => "Public key",
        8 => "Passphrase",
        9 => "TPM certificate",
        10 => "CNG or SID protector",
        _ => "Unknown"
    };

    public bool IsRecoveryCapable => Type is 2 or 3 or 7 or 8;
}

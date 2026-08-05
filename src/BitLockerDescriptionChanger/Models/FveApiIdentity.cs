namespace BitLockerDescriptionChanger.Models;

public enum FveCompatibilityLevel
{
    Unavailable,
    Experimental
}

public sealed record FveApiIdentity(
    string FilePath,
    string FileVersion,
    string Sha256,
    bool RequiredExportsAvailable,
    FveCompatibilityLevel CompatibilityLevel,
    string Diagnostic)
{
    public bool CanAttemptWrite => RequiredExportsAvailable && CompatibilityLevel != FveCompatibilityLevel.Unavailable;

    public string DisplayName => CompatibilityLevel switch
    {
        FveCompatibilityLevel.Experimental => $"Experimental ({FileVersion})",
        _ => "Unavailable"
    };
}

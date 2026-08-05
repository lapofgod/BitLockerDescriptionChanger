namespace BitLockerDescriptionChanger.Models;

public enum DescriptionChangeStatus
{
    Succeeded,
    FailedBeforeCommit,
    RolledBack,
    CriticalRollbackFailure
}

public sealed record NativeWriteResult(
    bool Succeeded,
    int OpenHResult,
    int SetHResult,
    int CommitHResult,
    int DiscardHResult,
    int CloseResult,
    bool CommitAttempted,
    string Diagnostic);

public sealed record DescriptionChangeResult(
    DescriptionChangeStatus Status,
    string Diagnostic,
    string? BackupPath,
    NativeWriteResult? WriteResult,
    NativeWriteResult? RollbackResult)
{
    public bool Succeeded => Status == DescriptionChangeStatus.Succeeded;
}

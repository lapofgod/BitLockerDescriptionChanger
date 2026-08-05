using BitLockerDescriptionChanger.Models;

namespace BitLockerDescriptionChanger.Services;

public sealed class UnavailableFveApi(FveApiIdentity identity) : IFveApi
{
    public FveApiIdentity Identity { get; } = identity;

    public string ReadDescription(string nativeVolumePath) => throw new InvalidOperationException(Identity.Diagnostic);

    public NativeWriteResult WriteDescription(string nativeVolumePath, string description) =>
        new(false, unchecked((int)0x80004005), 0, 0, 0, 0, false, Identity.Diagnostic);

    public void Dispose()
    {
    }
}

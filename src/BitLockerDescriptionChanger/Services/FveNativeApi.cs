using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using BitLockerDescriptionChanger.Models;

namespace BitLockerDescriptionChanger.Services;

public sealed class FveNativeApi : IFveApi
{
    private const int InitialDescriptionCapacity = 512;
    private readonly nint _module;
    private readonly FveOpenVolumeDelegate _openVolume;
    private readonly FveGetDescriptionDelegate _getDescription;
    private readonly FveSetDescriptionDelegate _setDescription;
    private readonly FveCommitChangesDelegate _commitChanges;
    private readonly FveDiscardChangesDelegate _discardChanges;
    private readonly FveCloseVolumeDelegate _closeVolume;
    private bool _disposed;

    public FveNativeApi(FveApiIdentity identity)
    {
        Identity = identity;
        if (!identity.RequiredExportsAvailable)
        {
            throw new InvalidOperationException(identity.Diagnostic);
        }

        _module = NativeLibrary.Load(identity.FilePath);
        _openVolume = LoadDelegate<FveOpenVolumeDelegate>("FveOpenVolumeW");
        _getDescription = LoadDelegate<FveGetDescriptionDelegate>("FveGetDescriptionW");
        _setDescription = LoadDelegate<FveSetDescriptionDelegate>("FveSetDescriptionW");
        _commitChanges = LoadDelegate<FveCommitChangesDelegate>("FveCommitChanges");
        _discardChanges = LoadDelegate<FveDiscardChangesDelegate>("FveDiscardChanges");
        _closeVolume = LoadDelegate<FveCloseVolumeDelegate>("FveCloseVolume");
    }

    public FveApiIdentity Identity { get; }

    public string ReadDescription(string nativeVolumePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        nint handle = 0;
        var openResult = _openVolume(nativeVolumePath, 0, out handle);
        ThrowForHResult(openResult, "FveOpenVolumeW");

        string? description = null;
        Exception? readFailure = null;
        try
        {
            description = ReadOpenedDescription(handle);
        }
        catch (Exception exception)
        {
            readFailure = exception;
        }

        var closeResult = handle == 0 ? 0 : _closeVolume(handle);
        if (readFailure is not null)
        {
            ExceptionDispatchInfo.Capture(readFailure).Throw();
        }

        if (closeResult < 0)
        {
            throw new InvalidOperationException(FormatHResult("FveCloseVolume", closeResult));
        }

        return description ?? string.Empty;
    }

    public NativeWriteResult WriteDescription(string nativeVolumePath, string description)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var openResult = unchecked((int)0x80004005);
        var setResult = unchecked((int)0x80004005);
        var commitResult = unchecked((int)0x80004005);
        var discardResult = 0;
        var closeResult = 0;
        var commitAttempted = false;
        var succeeded = false;
        var message = "The private API transaction did not complete.";
        nint handle = 0;

        try
        {
            openResult = _openVolume(nativeVolumePath, 1, out handle);
            if (openResult < 0 || handle == 0)
            {
                message = "FveOpenVolumeW failed.";
            }
            else
            {
                setResult = _setDescription(handle, description);
                if (setResult < 0)
                {
                    discardResult = _discardChanges(handle);
                    message = "FveSetDescriptionW failed before commit.";
                }
                else
                {
                    commitAttempted = true;
                    commitResult = _commitChanges(handle);
                    if (commitResult < 0)
                    {
                        discardResult = _discardChanges(handle);
                        message = "FveCommitChanges failed.";
                    }
                    else
                    {
                        succeeded = true;
                        message = "The private API transaction committed successfully.";
                    }
                }
            }
        }
        catch (Exception exception) when (exception is not AccessViolationException)
        {
            if (handle != 0 && !commitAttempted)
            {
                discardResult = _discardChanges(handle);
            }

            succeeded = false;
            message = exception.Message;
        }
        finally
        {
            if (handle != 0)
            {
                closeResult = _closeVolume(handle);
            }
        }

        if (closeResult < 0)
        {
            succeeded = false;
            message = "FveCloseVolume failed after the transaction.";
        }

        return CreateResult(succeeded, openResult, setResult, commitResult, discardResult, closeResult, commitAttempted, message);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        NativeLibrary.Free(_module);
        _disposed = true;
    }

    private T LoadDelegate<T>(string exportName) where T : Delegate
    {
        var address = NativeLibrary.GetExport(_module, exportName);
        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    private string ReadOpenedDescription(nint handle)
    {
        var capacity = InitialDescriptionCapacity;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            nint buffer = Marshal.AllocHGlobal(checked(capacity * sizeof(char)));
            try
            {
                var zero = new byte[checked(capacity * sizeof(char))];
                Marshal.Copy(zero, 0, buffer, zero.Length);
                var getResult = _getDescription(handle, buffer, (nuint)capacity, out var required);
                if (getResult >= 0)
                {
                    return Marshal.PtrToStringUni(buffer) ?? string.Empty;
                }

                if (unchecked((uint)getResult) != 0x8007007A || required <= (nuint)capacity)
                {
                    ThrowForHResult(getResult, "FveGetDescriptionW");
                }

                capacity = checked((int)required + 1);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        throw new InvalidOperationException("FveGetDescriptionW repeatedly reported an insufficient buffer.");
    }

    private static NativeWriteResult CreateResult(
        bool succeeded,
        int openResult,
        int setResult,
        int commitResult,
        int discardResult,
        int closeResult,
        bool commitAttempted,
        string message) => new(
            succeeded,
            openResult,
            setResult,
            commitResult,
            discardResult,
            closeResult,
            commitAttempted,
            $"{message} Open={FormatCode(openResult)}, Set={FormatCode(setResult)}, Commit={FormatCode(commitResult)}, " +
            $"Discard={FormatCode(discardResult)}, Close={FormatCode(closeResult)}");

    private static void ThrowForHResult(int result, string operation)
    {
        if (result >= 0)
        {
            return;
        }

        throw new Win32Exception(result, FormatHResult(operation, result));
    }

    private static string FormatHResult(string operation, int result) => $"{operation} failed with HRESULT {FormatCode(result)}.";

    private static string FormatCode(int result) => $"0x{unchecked((uint)result):X8}";

    [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
    private delegate int FveOpenVolumeDelegate(
        [MarshalAs(UnmanagedType.LPWStr)] string volumeName,
        uint flags,
        out nint volumeHandle);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int FveGetDescriptionDelegate(nint volumeHandle, nint buffer, nuint bufferSize, out nuint requiredSize);

    [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
    private delegate int FveSetDescriptionDelegate(
        nint volumeHandle,
        [MarshalAs(UnmanagedType.LPWStr)] string description);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int FveCommitChangesDelegate(nint volumeHandle);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int FveDiscardChangesDelegate(nint volumeHandle);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int FveCloseVolumeDelegate(nint volumeHandle);
}

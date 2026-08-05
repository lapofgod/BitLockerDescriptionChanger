using BitLockerDescriptionChanger.UI;

namespace BitLockerDescriptionChanger;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        if (!Environment.Is64BitProcess)
        {
            MessageBox.Show(
                "BitLocker Description Changer requires a 64-bit Windows process.",
                "Unsupported architecture",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, eventArgs) => ShowFatalError(eventArgs.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            ShowFatalError(eventArgs.ExceptionObject as Exception ?? new InvalidOperationException("An unknown fatal error occurred."));

        using var form = new MainForm();
        Application.Run(form);
    }

    private static void ShowFatalError(Exception exception) => MessageBox.Show(
        $"A fatal error occurred. No further write will be attempted in this process.\n\n{exception.Message}",
        "BitLocker Description Changer",
        MessageBoxButtons.OK,
        MessageBoxIcon.Stop);
}

namespace BitLockerDescriptionChanger.UI;

public sealed partial class RiskAcceptanceDialog : Form
{
    private const int ConfirmationDelaySeconds = 10;
    private int _remainingSeconds;

    public RiskAcceptanceDialog()
    {
        InitializeComponent();
        ResetCountdown();
    }

    public RiskAcceptanceDialog(string version, string sha256)
        : this()
    {
        apiIdentityLabel.Text = $"Version: {version}\r\nSHA-256: {sha256}";
    }

    private void RiskAcceptanceDialog_Shown(object? sender, EventArgs eventArgs)
    {
        ResetCountdown();
        countdownTimer.Start();
    }

    private void CountdownTimer_Tick(object? sender, EventArgs eventArgs)
    {
        _remainingSeconds--;
        if (_remainingSeconds <= 0)
        {
            countdownTimer.Stop();
            continueButton.Text = "Continue";
            continueButton.Enabled = true;
            return;
        }

        UpdateContinueButtonText();
    }

    private void RiskAcceptanceDialog_FormClosed(object? sender, FormClosedEventArgs eventArgs) =>
        countdownTimer.Stop();

    private void ResetCountdown()
    {
        countdownTimer.Stop();
        _remainingSeconds = ConfirmationDelaySeconds;
        continueButton.Enabled = false;
        UpdateContinueButtonText();
    }

    private void UpdateContinueButtonText() =>
        continueButton.Text = $"Continue ({_remainingSeconds})";
}

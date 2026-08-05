#nullable enable

namespace BitLockerDescriptionChanger.UI;

partial class RiskAcceptanceDialog
{
    private System.ComponentModel.IContainer? components;
    private TableLayoutPanel mainLayout = null!;
    private Label titleLabel = null!;
    private Label apiIdentityLabel = null!;
    private Label explanationLabel = null!;
    private FlowLayoutPanel buttonLayout = null!;
    private Button continueButton = null!;
    private Button cancelButton = null!;
    private System.Windows.Forms.Timer countdownTimer = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        mainLayout = new TableLayoutPanel();
        titleLabel = new Label();
        apiIdentityLabel = new Label();
        explanationLabel = new Label();
        buttonLayout = new FlowLayoutPanel();
        continueButton = new Button();
        cancelButton = new Button();
        countdownTimer = new System.Windows.Forms.Timer(components);
        mainLayout.SuspendLayout();
        buttonLayout.SuspendLayout();
        SuspendLayout();

        mainLayout.ColumnCount = 1;
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainLayout.Controls.Add(titleLabel, 0, 0);
        mainLayout.Controls.Add(apiIdentityLabel, 0, 1);
        mainLayout.Controls.Add(explanationLabel, 0, 2);
        mainLayout.Controls.Add(buttonLayout, 0, 3);
        mainLayout.Dock = DockStyle.Fill;
        mainLayout.Padding = new Padding(18);
        mainLayout.RowCount = 4;
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));

        titleLabel.AutoEllipsis = true;
        titleLabel.Dock = DockStyle.Fill;
        titleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        titleLabel.ForeColor = Color.DarkRed;
        titleLabel.Margin = new Padding(0);
        titleLabel.Text = "This feature is experimental and uses an undocumented BitLocker private API.";
        titleLabel.TextAlign = ContentAlignment.MiddleLeft;

        apiIdentityLabel.AutoEllipsis = true;
        apiIdentityLabel.Dock = DockStyle.Fill;
        apiIdentityLabel.Margin = new Padding(0);
        apiIdentityLabel.Text = "Version: supplied at runtime\r\nSHA-256: supplied at runtime";
        apiIdentityLabel.TextAlign = ContentAlignment.MiddleLeft;

        explanationLabel.Dock = DockStyle.Fill;
        explanationLabel.Margin = new Padding(0, 4, 0, 4);
        explanationLabel.Text = "Backups and automatic Description rollback reduce observable risk, but no compatibility or data-safety guarantee is provided. You accept all consequences of use. Review this warning for 10 seconds before continuing.";
        explanationLabel.TextAlign = ContentAlignment.MiddleLeft;

        buttonLayout.AutoSize = true;
        buttonLayout.Controls.Add(continueButton);
        buttonLayout.Controls.Add(cancelButton);
        buttonLayout.Dock = DockStyle.Fill;
        buttonLayout.FlowDirection = FlowDirection.RightToLeft;
        buttonLayout.Margin = new Padding(0, 6, 0, 0);
        buttonLayout.WrapContents = false;

        continueButton.DialogResult = DialogResult.OK;
        continueButton.Enabled = false;
        continueButton.Margin = new Padding(8, 0, 0, 0);
        continueButton.Size = new Size(112, 30);
        continueButton.Text = "Continue (10)";
        continueButton.UseVisualStyleBackColor = true;

        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Margin = new Padding(0);
        cancelButton.Size = new Size(90, 30);
        cancelButton.Text = "Cancel";
        cancelButton.UseVisualStyleBackColor = true;

        countdownTimer.Interval = 1000;
        countdownTimer.Tick += CountdownTimer_Tick;

        AcceptButton = continueButton;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        CancelButton = cancelButton;
        ClientSize = new Size(640, 280);
        Controls.Add(mainLayout);
        Font = new Font("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "RiskAcceptanceDialog";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Experimental BitLocker private API";
        FormClosed += RiskAcceptanceDialog_FormClosed;
        Shown += RiskAcceptanceDialog_Shown;
        mainLayout.ResumeLayout(false);
        mainLayout.PerformLayout();
        buttonLayout.ResumeLayout(false);
        ResumeLayout(false);
    }
}

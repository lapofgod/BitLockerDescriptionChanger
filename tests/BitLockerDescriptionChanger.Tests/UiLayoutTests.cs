using System.Reflection;
using System.Windows.Forms;
using BitLockerDescriptionChanger.UI;

namespace BitLockerDescriptionChanger.Tests;

[TestClass]
public sealed class UiLayoutTests
{
    [STATestMethod]
    public void MainFormUsesFixedAlignedAndUnclippedLayout()
    {
        using var form = new MainForm();
        form.CreateControl();
        PerformLayoutRecursively(form);

        var metadataValue = GetField<TextBox>(form, "currentDescriptionTextBox");
        var metadataGroup = GetField<GroupBox>(form, "metadataGroupBox");
        var metadataLayout = GetField<TableLayoutPanel>(form, "metadataLayout");
        var metadataVersion = GetField<TextBox>(form, "metadataVersionTextBox");
        var newDescriptionLabel = GetField<Label>(form, "newDescriptionLabel");
        var newDescription = GetField<TextBox>(form, "newDescriptionTextBox");
        var descriptionCounter = GetField<Label>(form, "descriptionCounterLabel");
        var changeButton = GetField<Button>(form, "changeButton");
        var volumeComboBox = GetField<ComboBox>(form, "volumeComboBox");
        var refreshButton = GetField<Button>(form, "refreshButton");
        var restoreButton = GetField<Button>(form, "restoreButton");
        var openBackupButton = GetField<Button>(form, "openBackupButton");
        var warningLabel = GetField<Label>(form, "experimentalWarningLabel");

        Assert.AreEqual(new Size(900, 510), form.ClientSize);
        Assert.AreEqual(FormBorderStyle.FixedSingle, form.FormBorderStyle);
        Assert.IsFalse(form.MaximizeBox);
        Assert.IsFalse(form.AutoScroll);
        Assert.AreEqual(refreshButton.Height, restoreButton.Height, "Toolbar button heights differ.");
        Assert.AreEqual(refreshButton.Height, openBackupButton.Height, "Toolbar button heights differ.");
        Assert.AreEqual(refreshButton.Height, changeButton.Height, "Action button heights differ between sections.");
        Assert.AreEqual(refreshButton.Padding, restoreButton.Padding, "Toolbar button padding differs.");
        Assert.AreEqual(refreshButton.Padding, openBackupButton.Padding, "Toolbar button padding differs.");
        Assert.AreEqual(refreshButton.Padding, changeButton.Padding, "Action button padding differs between sections.");
        Assert.AreEqual(
            GetFormX(metadataValue),
            GetFormX(newDescription),
            "Description fields are not aligned.");
        var editRowCenter = GetFormY(newDescription) + (newDescription.Height / 2);
        Assert.AreEqual(editRowCenter, GetFormY(newDescriptionLabel) + (newDescriptionLabel.Height / 2), 1, "New Description label is vertically misaligned.");
        Assert.AreEqual(editRowCenter, GetFormY(changeButton) + (changeButton.Height / 2), 1, "Change button is vertically misaligned.");
        Assert.IsGreaterThan(GetFormY(newDescription), GetFormY(descriptionCounter), "Description counter should be on the readiness row.");
        Assert.IsGreaterThan(30, descriptionCounter.Width, "Description counter collapsed horizontally.");
        Assert.AreEqual(ContentAlignment.MiddleLeft, warningLabel.TextAlign, "Experimental warning should be left aligned.");
        Assert.IsGreaterThan(220, metadataGroup.Height, "The FVE metadata section collapsed vertically.");
        Assert.IsLessThanOrEqualTo(metadataLayout.ClientSize.Height, metadataVersion.Bottom, "Metadata version is clipped by the metadata layout.");
        Assert.IsLessThanOrEqualTo(metadataGroup.DisplayRectangle.Bottom, metadataLayout.Bottom, "Metadata layout is clipped by the group box.");
        Assert.IsGreaterThan(300, metadataValue.Width, "The FVE metadata values became too narrow.");
        Assert.IsGreaterThan(15, metadataValue.Height, "The FVE metadata value controls collapsed vertically.");
        Assert.IsGreaterThan(100, volumeComboBox.Width, "The volume selector became too narrow.");
        AssertControlInsideClientArea(form, refreshButton);
        AssertControlInsideClientArea(form, restoreButton);
        AssertControlInsideClientArea(form, openBackupButton);
        AssertControlInsideClientArea(form, descriptionCounter);
        AssertControlInsideClientArea(form, warningLabel);
        Assert.IsFalse(form.HorizontalScroll.Visible);
        Assert.IsFalse(form.VerticalScroll.Visible);
    }

    [STATestMethod]
    public void RiskAcceptanceDialogHasVisibleDesignerControls()
    {
        using var dialog = new RiskAcceptanceDialog("test-version", new string('A', 64));
        dialog.CreateControl();
        PerformLayoutRecursively(dialog);

        var continueButton = GetField<Button>(dialog, "continueButton");
        var cancelButton = GetField<Button>(dialog, "cancelButton");

        Assert.IsFalse(continueButton.Enabled);
        Assert.AreEqual("Continue (10)", continueButton.Text);
        AssertControlInsideClientArea(dialog, continueButton);
        AssertControlInsideClientArea(dialog, cancelButton);

        var tickMethod = typeof(RiskAcceptanceDialog).GetMethod(
            "CountdownTimer_Tick",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("Countdown tick handler was not found.");
        for (var second = 0; second < 9; second++)
        {
            tickMethod.Invoke(dialog, [null, EventArgs.Empty]);
        }

        Assert.IsFalse(continueButton.Enabled);
        Assert.AreEqual("Continue (1)", continueButton.Text);
        tickMethod.Invoke(dialog, [null, EventArgs.Empty]);
        Assert.IsTrue(continueButton.Enabled);
        Assert.AreEqual("Continue", continueButton.Text);
    }

    private static T GetField<T>(object instance, string name)
        where T : class
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(instance) as T
               ?? throw new AssertFailedException($"Designer field '{name}' was not found.");
    }

    private static int GetFormX(Control control)
    {
        var x = control.Left;
        for (var parent = control.Parent; parent is not null and not Form; parent = parent.Parent)
        {
            x += parent.Left;
        }

        return x;
    }

    private static int GetFormY(Control control)
    {
        var y = control.Top;
        for (var parent = control.Parent; parent is not null and not Form; parent = parent.Parent)
        {
            y += parent.Top;
        }

        return y;
    }

    private static void AssertControlInsideClientArea(Form form, Control control)
    {
        var x = GetFormX(control);
        var y = GetFormY(control);

        Assert.IsGreaterThanOrEqualTo(0, x);
        Assert.IsGreaterThanOrEqualTo(0, y);
        Assert.IsLessThanOrEqualTo(form.ClientSize.Width, x + control.Width);
        Assert.IsLessThanOrEqualTo(form.ClientSize.Height, y + control.Height);
    }

    private static void PerformLayoutRecursively(Control control)
    {
        control.PerformLayout();
        foreach (Control child in control.Controls)
        {
            PerformLayoutRecursively(child);
        }
    }
}

using System.Windows.Forms;

namespace ClampDown.UI;

internal static class UiPrompt
{
    public static string? PromptText(IWin32Window owner, string title, string label, string initialValue)
    {
        using var dialog = new Form
        {
            Text = title,
            Width = 520,
            Height = 170,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false
        };

        var labelControl = new Label { Text = label, Left = 12, Top = 14, AutoSize = true };
        var input = new TextBox { Left = 12, Top = 40, Width = 480, Text = initialValue };
        var ok = new Button { Text = "OK", Left = 332, Width = 75, Top = 82, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Left = 417, Width = 75, Top = 82, DialogResult = DialogResult.Cancel };

        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;

        dialog.Controls.Add(labelControl);
        dialog.Controls.Add(input);
        dialog.Controls.Add(ok);
        dialog.Controls.Add(cancel);

        return dialog.ShowDialog(owner) == DialogResult.OK ? input.Text : null;
    }

    public static bool PromptTypedConfirmation(IWin32Window owner, string title, string message, string typedValue)
    {
        var input = PromptText(owner, title, message + $"\r\n\r\nType {typedValue} to confirm:", "");
        return string.Equals(input?.Trim(), typedValue, StringComparison.OrdinalIgnoreCase);
    }
}


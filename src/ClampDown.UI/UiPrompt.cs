using System.Windows.Forms;

namespace ClampDown.UI;

internal static class UiPrompt
{
    public static string? PromptText(IWin32Window owner, string title, string label, string initialValue)
    {
        // Get the UI services to access theme manager
        var services = (owner as Form)?.Tag as UiServices;
        
        using var dialog = new Form
        {
            Text = title,
            Width = 520,
            Height = 180,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false
        };

        var labelControl = new Label { Text = label, Left = 20, Top = 20, AutoSize = true };
        var input = new TextBox { Left = 20, Top = 50, Width = 465, Text = initialValue, Font = new Font("Segoe UI", 10) };
        
        var ok = new Button { Text = "OK", Left = 320, Width = 80, Height = 32, Top = 95, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Left = 405, Width = 80, Height = 32, Top = 95, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel };

        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;

        dialog.Controls.Add(labelControl);
        dialog.Controls.Add(input);
        dialog.Controls.Add(ok);
        dialog.Controls.Add(cancel);

        // Try to apply theme if services are available
        if (owner is MainForm mainForm && mainForm.GetType().GetField("_services", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(mainForm) is UiServices foundServices)
        {
            foundServices.ThemeManager.ApplyToForm(dialog);
        }

        return dialog.ShowDialog(owner) == DialogResult.OK ? input.Text : null;
    }

    public static bool PromptTypedConfirmation(IWin32Window owner, string title, string message, string typedValue)
    {
        var input = PromptText(owner, title, message + $"\r\n\r\nType {typedValue} to confirm:", "");
        return string.Equals(input?.Trim(), typedValue, StringComparison.OrdinalIgnoreCase);
    }
}


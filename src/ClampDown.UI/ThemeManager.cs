using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClampDown.UI;

public sealed class ThemeManager
{
    private Theme _currentTheme = Theme.Dark;

    public Theme CurrentTheme => _currentTheme;

    public event EventHandler? ThemeChanged;

    public void SetTheme(Theme theme)
    {
        _currentTheme = theme;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyToForm(Form form)
    {
        form.BackColor = _currentTheme.Background;
        form.ForeColor = _currentTheme.PrimaryText;
        ApplyToControlRecursive(form);
    }

    public void ApplyToControl(Control control)
    {
        ApplyToControlRecursive(control);
    }

    private void ApplyToControlRecursive(Control control)
    {
        switch (control)
        {
            case Form form:
                form.BackColor = _currentTheme.Background;
                form.ForeColor = _currentTheme.PrimaryText;
                break;

            case TabControl tabControl:
                tabControl.BackColor = _currentTheme.Surface;
                tabControl.ForeColor = _currentTheme.PrimaryText;
                foreach (TabPage page in tabControl.TabPages)
                {
                    page.BackColor = _currentTheme.Background;
                    page.ForeColor = _currentTheme.PrimaryText;
                }
                break;

            case Button button:
                button.BackColor = _currentTheme.ButtonFace;
                button.ForeColor = _currentTheme.ButtonText;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = _currentTheme.Border;
                button.FlatAppearance.BorderSize = 1;
                break;

            case TextBox textBox:
                textBox.BackColor = _currentTheme.Surface;
                textBox.ForeColor = _currentTheme.PrimaryText;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                break;

            case ComboBox comboBox:
                comboBox.BackColor = _currentTheme.Surface;
                comboBox.ForeColor = _currentTheme.PrimaryText;
                comboBox.FlatStyle = FlatStyle.Flat;
                break;

            case CheckBox checkBox:
                checkBox.ForeColor = _currentTheme.PrimaryText;
                break;

            case Label label:
                label.ForeColor = label.Font.Bold ? _currentTheme.PrimaryText : _currentTheme.SecondaryText;
                break;

            case DataGridView grid:
                ApplyToDataGridView(grid);
                break;

            case TableLayoutPanel tablePanel:
                tablePanel.BackColor = _currentTheme.Background;
                tablePanel.ForeColor = _currentTheme.PrimaryText;
                break;

            case FlowLayoutPanel flowPanel:
                flowPanel.BackColor = _currentTheme.Background;
                flowPanel.ForeColor = _currentTheme.PrimaryText;
                break;

            case Panel panel:
                panel.BackColor = _currentTheme.Background;
                panel.ForeColor = _currentTheme.PrimaryText;
                break;
        }

        foreach (Control child in control.Controls)
        {
            ApplyToControlRecursive(child);
        }
    }

    private void ApplyToDataGridView(DataGridView grid)
    {
        grid.BackgroundColor = _currentTheme.Background;
        grid.GridColor = _currentTheme.GridLines;
        grid.DefaultCellStyle.BackColor = _currentTheme.Surface;
        grid.DefaultCellStyle.ForeColor = _currentTheme.PrimaryText;
        grid.DefaultCellStyle.SelectionBackColor = _currentTheme.GridSelection;
        grid.DefaultCellStyle.SelectionForeColor = _currentTheme.PrimaryText;
        grid.ColumnHeadersDefaultCellStyle.BackColor = _currentTheme.GridHeader;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = _currentTheme.PrimaryText;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = _currentTheme.GridHeader;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = _currentTheme.PrimaryText;
        grid.AlternatingRowsDefaultCellStyle.BackColor = _currentTheme.SurfaceAlt;
        grid.EnableHeadersVisualStyles = false;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
    }
}

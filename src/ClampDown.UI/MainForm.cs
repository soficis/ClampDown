using System.ComponentModel;
using System.Windows.Forms;
using ClampDown.UI.Tabs;

namespace ClampDown.UI;

public sealed class MainForm : Form
{
    private readonly UiServices _services;
    private readonly Panel _sidebar;
    private readonly Panel _contentContainer;
    private readonly List<SidebarButton> _sidebarButtons = new();
    private readonly List<Control> _tabs = new();

    public MainForm(UiServices services)
    {
        _services = services;

        Text = "ClampDown";
        Width = 1200;
        Height = 800;
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        Icon = AppIconLoader.Load();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = services.ThemeManager.CurrentTheme.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        _sidebar = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 40, 0, 0),
            Tag = "Sidebar",
            BackColor = services.ThemeManager.CurrentTheme.Gutter
        };
        root.Controls.Add(_sidebar, 0, 0);

        _contentContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(30),
            BackColor = services.ThemeManager.CurrentTheme.Background
        };
        root.Controls.Add(_contentContainer, 1, 0);

        AddSidebarItem("Overview", 0);
        AddSidebarItem("Files", 1);
        AddSidebarItem("Drives", 2);
        AddSidebarItem("Activity Log", 3);
        AddSidebarItem("Settings", 4);

        _tabs.Add(new OverviewTab(_services, SwitchToTab) { Dock = DockStyle.Fill });
        _tabs.Add(new FilesTab(_services) { Dock = DockStyle.Fill });
        _tabs.Add(new DrivesTab(_services) { Dock = DockStyle.Fill });
        _tabs.Add(new LogsTab(_services) { Dock = DockStyle.Fill });
        _tabs.Add(new SettingsTab(_services) { Dock = DockStyle.Fill });

        SwitchToTab(0);

        _services.ThemeManager.ApplyToForm(this);
        _services.ThemeManager.ThemeChanged += (_, _) => _services.ThemeManager.ApplyToForm(this);
    }

    private void AddSidebarItem(string text, int index)
    {
        var btn = new SidebarButton(_services.ThemeManager)
        {
            Text = "  " + text,
            Dock = DockStyle.Top,
            Height = 44,
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 10.5f)
        };
        btn.Click += (_, _) => SwitchToTab(index);
        _sidebarButtons.Add(btn);
        _sidebar.Controls.Add(btn);
        _sidebar.Controls.SetChildIndex(btn, 0); // Reverse order for Top docking
    }

    public void SwitchToTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;

        _contentContainer.Controls.Clear();
        _contentContainer.Controls.Add(_tabs[index]);

        for (int i = 0; i < _sidebarButtons.Count; i++)
        {
            _sidebarButtons[i].IsSelected = i == (_sidebarButtons.Count - 1 - index);
        }
    }
}

public sealed class SidebarButton : Button
{
    private readonly ThemeManager _themeManager;
    private bool _isSelected;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; Invalidate(); }
    }

    public SidebarButton(ThemeManager themeManager)
    {
        _themeManager = themeManager;
        DoubleBuffered = true;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseDownBackColor = Color.Transparent;
        FlatAppearance.MouseOverBackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var theme = _themeManager.CurrentTheme;
        var rect = ClientRectangle;

        using var brush = new SolidBrush(_isSelected ? theme.Surface : theme.Gutter);
        e.Graphics.FillRectangle(brush, rect);

        if (_isSelected)
        {
            using var accentBrush = new SolidBrush(theme.Primary);
            e.Graphics.FillRectangle(accentBrush, 0, 10, 4, rect.Height - 20);
        }

        TextRenderer.DrawText(e.Graphics, Text, Font, rect, _isSelected ? theme.PrimaryText : theme.SecondaryText, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }
}

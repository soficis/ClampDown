using System.Drawing;
using System.Windows.Forms;

namespace ClampDown.UI.Tabs;

public sealed class OverviewTab : UserControl
{
    private readonly UiServices _services;
    private readonly Action<int> _navigateToTab;

    public OverviewTab(UiServices services, Action<int> navigateToTab)
    {
        _services = services;
        _navigateToTab = navigateToTab;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        // Header
        var header = new Panel { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 20) };
        var title = new Label
        {
            Text = "ClampDown",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 24, FontStyle.Bold),
            Dock = DockStyle.Top
        };
        var subtitle = new Label
        {
            Text = "Unlock files and safely eject removable drives",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 11),
            Dock = DockStyle.Top,
            Padding = new Padding(0, 8, 0, 0)
        };
        header.Controls.Add(subtitle);
        header.Controls.Add(title);
        root.Controls.Add(header);

        // Cards Grid
        var cardsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(0)
        };
        cardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cardsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        cardsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        cardsPanel.Controls.Add(CreateNavigationCard(
            "📁 Files",
            "Analyze file locks and unlock files",
            "View which processes are locking files and take action to unlock them for operations.",
            () => _navigateToTab(1)
        ), 0, 0);

        cardsPanel.Controls.Add(CreateNavigationCard(
            "💾 Drives",
            "Manage removable drives",
            "Safely eject USB drives and external storage by automatically closing locking processes.",
            () => _navigateToTab(2)
        ), 1, 0);

        cardsPanel.Controls.Add(CreateNavigationCard(
            "📊 Activity Log",
            "View operation history",
            "Review all actions taken by ClampDown including process termination and file operations.",
            () => _navigateToTab(3)
        ), 0, 1);

        cardsPanel.Controls.Add(CreateNavigationCard(
            "⚙️ Settings",
            "Configure preferences",
            "Adjust startup settings, elevation options, and customize your ClampDown experience.",
            () => _navigateToTab(4)
        ), 1, 1);

        root.Controls.Add(cardsPanel);

        // Footer
        var footer = new Label
        {
            Text = "Click a card above to get started",
            AutoSize = true,
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(0, 20, 0, 0)
        };
        root.Controls.Add(footer);

        // Apply theme
        _services.ThemeManager.ApplyToControl(this);
        _services.ThemeManager.ThemeChanged += (_, _) =>
        {
            _services.ThemeManager.ApplyToControl(this);
            // Recreate cards with new theme
            RefreshCards();
        };
    }

    private Panel CreateNavigationCard(string title, string subtitle, string description, Action onClick)
    {
        var theme = _services.ThemeManager.CurrentTheme;
        
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(10),
            Padding = new Padding(20),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = theme.Surface,
            Cursor = Cursors.Hand
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
            ForeColor = theme.PrimaryText,
            Dock = DockStyle.Top
        };

        var subtitleLabel = new Label
        {
            Text = subtitle,
            AutoSize = true,
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
            ForeColor = theme.Primary,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 8, 0, 0)
        };

        var descLabel = new Label
        {
            Text = description,
            AutoSize = true,
            MaximumSize = new Size(400, 0),
            ForeColor = theme.SecondaryText,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 12, 0, 0)
        };

        var actionLabel = new Label
        {
            Text = "→ Open",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 9, FontStyle.Bold),
            ForeColor = theme.Primary,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 16, 0, 0)
        };

        layout.Controls.Add(titleLabel);
        layout.Controls.Add(subtitleLabel);
        layout.Controls.Add(descLabel);
        layout.Controls.Add(actionLabel);

        card.Controls.Add(layout);

        // Hover effect
        card.MouseEnter += (_, _) => card.BackColor = theme.CardHover;
        card.MouseLeave += (_, _) => card.BackColor = theme.Surface;
        card.Click += (_, _) => onClick();

        // Make all child controls clickable
        foreach (Control control in card.Controls)
        {
            MakeClickable(control, onClick, card, theme);
        }

        return card;
    }

    private void MakeClickable(Control control, Action onClick, Panel card, Theme theme)
    {
        control.Cursor = Cursors.Hand;
        control.Click += (_, _) => onClick();
        control.MouseEnter += (_, _) => card.BackColor = theme.CardHover;
        control.MouseLeave += (_, _) => card.BackColor = theme.Surface;

        foreach (Control child in control.Controls)
        {
            MakeClickable(child, onClick, card, theme);
        }
    }

    private void RefreshCards()
    {
        // Force recreate by triggering a layout refresh
        if (InvokeRequired)
        {
            BeginInvoke(RefreshCards);
            return;
        }
        PerformLayout();
    }
}


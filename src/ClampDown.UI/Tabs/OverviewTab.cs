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
            RowCount = 2,
            Padding = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        // Header Section
        var header = new Panel { Dock = DockStyle.Top, Height = 120, Padding = new Padding(10, 20, 10, 0) };
        var title = new Label
        {
            Text = "Welcome to ClampDown",
            AutoSize = true,
            Font = new Font("Segoe UI Light", 28),
            Tag = "Header",
            Location = new Point(0, 0)
        };
        var subtitle = new Label
        {
            Text = "The ultimate utility for file control and drive safety.",
            AutoSize = true,
            Font = new Font("Segoe UI", 12),
            ForeColor = services.ThemeManager.CurrentTheme.SecondaryText,
            Location = new Point(4, 52)
        };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        root.Controls.Add(header, 0, 0);

        // Flow layout for cards to feel more organic
        var cardsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 20, 0, 0),
            AutoScroll = true
        };
        
        cardsFlow.Controls.Add(CreatePremiumCard(
            "Files",
            "Identify and release locked files with surgical precision.",
            () => _navigateToTab(1)
        ));

        cardsFlow.Controls.Add(CreatePremiumCard(
            "Drives",
            "Safely eject external storage without the 'In Use' drama.",
            () => _navigateToTab(2)
        ));

        cardsFlow.Controls.Add(CreatePremiumCard(
            "Activity",
            "A transparent history of every operation performed.",
            () => _navigateToTab(3)
        ));

        root.Controls.Add(cardsFlow, 0, 1);

        _services.ThemeManager.ApplyToControl(this);
    }

    private Panel CreatePremiumCard(string title, string description, Action onClick)
    {
        var theme = _services.ThemeManager.CurrentTheme;
        var card = new Panel
        {
            Width = 320,
            Height = 180,
            Margin = new Padding(0, 0, 30, 30),
            BackColor = theme.Surface,
            Cursor = Cursors.Hand,
            Tag = "Surface"
        };

        var titleLabel = new Label
        {
            Text = title,
            Font = new Font("Segoe UI Semibold", 16),
            ForeColor = theme.PrimaryText,
            Location = new Point(20, 20),
            AutoSize = true
        };

        var descLabel = new Label
        {
            Text = description,
            Font = new Font("Segoe UI", 10),
            ForeColor = theme.SecondaryText,
            Location = new Point(20, 60),
            Size = new Size(280, 60)
        };

        var actionLabel = new Label
        {
            Text = "Get Started →",
            Font = new Font("Segoe UI Semibold", 9),
            ForeColor = theme.Primary,
            Location = new Point(20, 135),
            AutoSize = true,
            Tag = "Accent"
        };

        card.Controls.Add(titleLabel);
        card.Controls.Add(descLabel);
        card.Controls.Add(actionLabel);

        card.Click += (_, _) => onClick();
        foreach (Control c in card.Controls)
        {
            c.Click += (_, _) => onClick();
            c.Cursor = Cursors.Hand;
        }

        card.MouseEnter += (_, _) => card.BackColor = theme.CardHover;
        card.MouseLeave += (_, _) => card.BackColor = theme.Surface;

        return card;
    }
}


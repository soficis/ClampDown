using System.Drawing;

namespace ClampDown.UI;

public sealed class Theme
{
    public required string Name { get; init; }
    public required Color Background { get; init; }
    public required Color Surface { get; init; }
    public required Color SurfaceAlt { get; init; }
    public required Color Primary { get; init; }
    public required Color Accent { get; init; }
    public required Color PrimaryText { get; init; }
    public required Color SecondaryText { get; init; }
    public required Color AccentText { get; init; }
    public required Color Border { get; init; }
    public required Color ButtonFace { get; init; }
    public required Color ButtonText { get; init; }
    public required Color GridHeader { get; init; }
    public required Color GridLines { get; init; }
    public required Color GridSelection { get; init; }
    public required Color Success { get; init; }
    public required Color Warning { get; init; }
    public required Color Error { get; init; }
    public required Color CardHover { get; init; }
    public required Color Gutter { get; init; }
    public int CornerRadius { get; init; } = 8;

    public static Theme Dark => new()
    {
        Name = "Dark",
        Background = Color.FromArgb(10, 10, 10), // Deeper black
        Surface = Color.FromArgb(24, 24, 24),    // Sleek surface
        SurfaceAlt = Color.FromArgb(18, 18, 18),
        Primary = Color.FromArgb(0, 125, 255),   // San Francisco Blue
        Accent = Color.FromArgb(88, 86, 214),    // Premium Indigo
        PrimaryText = Color.FromArgb(255, 255, 255),
        SecondaryText = Color.FromArgb(174, 174, 178),
        AccentText = Color.FromArgb(255, 255, 255),
        Border = Color.FromArgb(38, 38, 41),
        ButtonFace = Color.FromArgb(44, 44, 46),
        ButtonText = Color.FromArgb(255, 255, 255),
        GridHeader = Color.FromArgb(28, 28, 30),
        GridLines = Color.FromArgb(44, 44, 46),
        GridSelection = Color.FromArgb(0, 122, 255, 100), // Transparent primary
        Success = Color.FromArgb(48, 209, 88),
        Warning = Color.FromArgb(255, 159, 10),
        Error = Color.FromArgb(255, 69, 58),
        CardHover = Color.FromArgb(32, 32, 35),
        Gutter = Color.FromArgb(18, 18, 18),
        CornerRadius = 12
    };

    public static Theme Light => new()
    {
        Name = "Light",
        Background = Color.FromArgb(242, 242, 247),
        Surface = Color.FromArgb(255, 255, 255),
        SurfaceAlt = Color.FromArgb(249, 249, 252),
        Primary = Color.FromArgb(0, 122, 255),
        Accent = Color.FromArgb(88, 86, 214),
        PrimaryText = Color.FromArgb(0, 0, 0),
        SecondaryText = Color.FromArgb(60, 60, 67),
        AccentText = Color.FromArgb(255, 255, 255),
        Border = Color.FromArgb(209, 209, 214),
        ButtonFace = Color.FromArgb(229, 229, 234),
        ButtonText = Color.FromArgb(0, 0, 0),
        GridHeader = Color.FromArgb(242, 242, 247),
        GridLines = Color.FromArgb(199, 199, 204),
        GridSelection = Color.FromArgb(0, 122, 255, 100),
        Success = Color.FromArgb(52, 199, 89),
        Warning = Color.FromArgb(255, 149, 0),
        Error = Color.FromArgb(255, 59, 48),
        CardHover = Color.FromArgb(242, 242, 247),
        Gutter = Color.FromArgb(255, 255, 255),
        CornerRadius = 12
    };
}

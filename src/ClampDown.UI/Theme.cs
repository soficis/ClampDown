using System.Drawing;

namespace ClampDown.UI;

public sealed class Theme
{
    public required string Name { get; init; }
    public required Color Background { get; init; }
    public required Color Surface { get; init; }
    public required Color SurfaceAlt { get; init; }
    public required Color Primary { get; init; }
    public required Color PrimaryText { get; init; }
    public required Color SecondaryText { get; init; }
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

    public static Theme Dark => new()
    {
        Name = "Dark",
        Background = Color.FromArgb(30, 30, 30),
        Surface = Color.FromArgb(45, 45, 48),
        SurfaceAlt = Color.FromArgb(37, 37, 38),
        Primary = Color.FromArgb(0, 122, 204),
        PrimaryText = Color.FromArgb(241, 241, 241),
        SecondaryText = Color.FromArgb(200, 200, 200),
        Border = Color.FromArgb(63, 63, 70),
        ButtonFace = Color.FromArgb(62, 62, 64),
        ButtonText = Color.FromArgb(241, 241, 241),
        GridHeader = Color.FromArgb(51, 51, 55),
        GridLines = Color.FromArgb(63, 63, 70),
        GridSelection = Color.FromArgb(51, 153, 255),
        Success = Color.FromArgb(106, 153, 85),
        Warning = Color.FromArgb(206, 145, 120),
        Error = Color.FromArgb(244, 71, 71),
        CardHover = Color.FromArgb(55, 55, 58)
    };

    public static Theme Light => new()
    {
        Name = "Light",
        Background = Color.FromArgb(255, 255, 255),
        Surface = Color.FromArgb(243, 243, 243),
        SurfaceAlt = Color.FromArgb(250, 250, 250),
        Primary = Color.FromArgb(0, 120, 212),
        PrimaryText = Color.FromArgb(50, 50, 50),
        SecondaryText = Color.FromArgb(96, 96, 96),
        Border = Color.FromArgb(204, 204, 204),
        ButtonFace = Color.FromArgb(225, 225, 225),
        ButtonText = Color.FromArgb(50, 50, 50),
        GridHeader = Color.FromArgb(230, 230, 230),
        GridLines = Color.FromArgb(217, 217, 217),
        GridSelection = Color.FromArgb(0, 120, 215),
        Success = Color.FromArgb(16, 124, 16),
        Warning = Color.FromArgb(157, 93, 0),
        Error = Color.FromArgb(196, 43, 28),
        CardHover = Color.FromArgb(235, 235, 235)
    };
}

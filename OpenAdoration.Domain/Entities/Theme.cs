using OpenAdoration.Domain.Common;

namespace OpenAdoration.Domain.Entities;

public class Theme : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    // Text style
    public string FontFamily      { get; set; } = "Arial";
    public int    FontSize        { get; set; } = 36;
    public string FontColor       { get; set; } = "#FFFFFF";
    public string TextAlignment   { get; set; } = "Center";

    // Background — layers applied in order: Color → Image → Video (each overrides the one below)
    public string BackgroundColor { get; set; } = "#000000";
    public string? BackgroundImagePath { get; set; }
    public string? BackgroundVideoPath { get; set; }

    public bool IsDefault { get; set; }
}

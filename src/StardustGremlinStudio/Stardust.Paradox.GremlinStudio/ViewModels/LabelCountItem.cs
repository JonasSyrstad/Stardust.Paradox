namespace Stardust.Paradox.GremlinStudio.ViewModels;

/// <summary>
/// Represents a label and its count for database statistics display.
/// </summary>
public sealed record LabelCountItem(string Label, long Count);

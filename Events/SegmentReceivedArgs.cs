namespace Smpp.Tools.Events;

public class SegmentReceivedArgs
{
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string SegmentId { get; set; } = string.Empty;
    public string TotalSegments { get; set; } = string.Empty;
}
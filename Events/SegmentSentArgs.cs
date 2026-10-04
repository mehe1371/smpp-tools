namespace Smpp.Tools.Events;

public class SegmentSentArgs
{
    public string? ReferenceId { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
}
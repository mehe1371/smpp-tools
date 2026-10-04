namespace Smpp.Tools.Events;

public class SmsSentArgs
{
    public string? ReferenceId { get; set; }
    public string MessageIds { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
}
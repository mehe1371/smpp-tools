namespace Smpp.Tools.Events;

public class SegmentDeliveredArgs : EventArgs
{
    public string DeliveryReceipt { get; set; } = string.Empty;
}
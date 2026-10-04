namespace Smpp.Tools.Lib;

public class DeliveryReceipt
{
    public string Id { get; set; } = string.Empty;
    public string Sub { get; set; } = string.Empty;
    public int Dlvrd { get; set; }
    public DateTime DoneDate { get; set; } = DateTime.UtcNow;
    public string Stat { get; set; } = string.Empty;
    public string Err { get; set; } = string.Empty;
}
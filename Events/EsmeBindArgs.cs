using Smpp.Tools.Protocol;

namespace Smpp.Tools.Events;

public class EsmeBindArgs : EventArgs
{
    public string SystemId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public AddressTon AddressTon { get; set; }
    public AddressNpi AddressNpi { get; set; }
    public BindTransceiverRespPdu? Response { get; set; }
}
using Smpp.Tools.Lib;
using Smpp.Tools.Protocol;

namespace Smpp.Tools.Events;

public class ClientBindArgs : EventArgs
{
    public string SystemId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public AddressTon AddressTon { get; set; }
    public AddressNpi AddressNpi { get; set; }
    public BindTransceiverRespPdu? Response { get; set; }
}

public delegate void ClientBindEventHandler(object sender, Esme client, ClientBindArgs args);
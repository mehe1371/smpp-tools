using Smpp.Tools.Protocol;

namespace Smpp.Tools.Exceptions;

public class SmppException : Exception
{
    internal PduCommandId CommandId { get; set; }
    internal PduCommandStatus Status { get; set; }
    internal SmppException(string message, PduCommandId commandId, PduCommandStatus status) : base(message)
    {
        this.CommandId = commandId;
        this.Status = status;
    }
}
namespace Smpp.Tools.Protocol;

public enum SessionState
{
    Open,
    Bound_TX,
    Bound_RX,
    Bound_TRX,
    Unbound,
    Closed
}
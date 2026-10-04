using Smpp.Tools.Lib;

namespace Smpp.Tools.Protocol;

internal class GenericNackPdu : Pdu
{
    public override PduCommandId Id => PduCommandId.generic_nack;

    public override byte[] GetBytes()
    {
        byte[] payload = [
            .. GetOctets((UInt32)Id),
            .. GetOctets((UInt32)Status),
            .. GetOctets((UInt32)SequenceNumber)
        ];
        return [.. GetOctets((UInt32)payload.Length + 4), .. payload];
    }
}
using Smpp.Tools.Lib;

namespace Smpp.Tools.Protocol;

public class UnbindRespPdu : Pdu, IParseablePdu<UnbindRespPdu>
{
    public override PduCommandId Id => PduCommandId.unbind_resp;

    public override byte[] GetBytes()
    {
        byte[] payload = [
            .. GetOctets((UInt32)Id),
            .. GetOctets((UInt32)Status),
            .. GetOctets((UInt32)SequenceNumber)
        ];
        return [.. GetOctets((UInt32)payload.Length + 4), .. payload];
    }

    public static UnbindRespPdu FromBytes(byte[] bytes)
    {
        var pdu = new UnbindRespPdu();

        var command_id = ExtractId(bytes);
        if (command_id != PduCommandId.unbind_resp)
            throw new InvalidOperationException($"Invalid PDU type received, expected {PduCommandId.unbind_resp}, received {command_id}");

        var command_length = ExtractLength(bytes);
        if (command_length != bytes.Length)
            throw new InvalidOperationException($"Invalid PDU length, expected {command_length}, received {bytes.Length}");

        pdu.Status = ExtractStatus(bytes);
        pdu.SequenceNumber = ExtractSequenceNumber(bytes);

        return pdu;
    }
}
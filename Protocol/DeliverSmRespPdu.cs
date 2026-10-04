using Smpp.Tools.Lib;

namespace Smpp.Tools.Protocol;

public class DeliverSmRespPdu : Pdu, IParseablePdu<DeliverSmRespPdu>
{
    public override PduCommandId Id => PduCommandId.deliver_sm_resp;
    public static string MessageId => string.Empty;

    public override byte[] GetBytes()
    {
        byte[] payload = [
            .. GetOctets((UInt32)Id),
            .. GetOctets((UInt32)Status),
            .. GetOctets((UInt32)SequenceNumber),
            .. GetCOctets(MessageId, DataCoding.Default),
        ];
        return [.. GetOctets((UInt32)payload.Length + 4), .. payload];
    }

    public static DeliverSmRespPdu FromBytes(byte[] bytes)
    {
        var pdu = new DeliverSmRespPdu();

        var command_id = ExtractId(bytes);
        if (command_id != PduCommandId.deliver_sm_resp)
            throw new InvalidOperationException($"Invalid PDU type received, expected {PduCommandId.deliver_sm_resp}, received {command_id}");

        var command_length = ExtractLength(bytes);
        if (command_length != bytes.Length)
            throw new InvalidOperationException($"Invalid PDU length, expected {command_length}, received {bytes.Length}");

        pdu.Status = ExtractStatus(bytes);
        pdu.SequenceNumber = ExtractSequenceNumber(bytes);

        return pdu;
    }
}
using Smpp.Tools.Lib;

namespace Smpp.Tools.Protocol;

public class EnquireLinkPdu : Pdu, IParseablePdu<EnquireLinkPdu>
{
    public override PduCommandId Id => PduCommandId.enquire_link;

    public override byte[] GetBytes()
    {
        byte[] payload = [
            .. GetOctets((UInt32)Id),
            .. GetOctets((UInt32)Status),
            .. GetOctets((UInt32)SequenceNumber)
        ];
        return [.. GetOctets((UInt32)payload.Length + 4), .. payload];
    }

    public static EnquireLinkPdu FromBytes(byte[] bytes)
    {
        var pdu = new EnquireLinkPdu();

        var command_id = ExtractId(bytes);
        if (command_id != PduCommandId.enquire_link)
            throw new InvalidOperationException($"Invalid PDU type received, expected {PduCommandId.enquire_link}, received {command_id}");

        var command_length = ExtractLength(bytes);
        if (command_length != bytes.Length)
            throw new InvalidOperationException($"Invalid PDU length, expected {command_length}, received {bytes.Length}");

        pdu.Status = ExtractStatus(bytes);
        pdu.SequenceNumber = ExtractSequenceNumber(bytes);

        return pdu;
    }
}
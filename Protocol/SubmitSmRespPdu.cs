using Smpp.Tools.Lib;

namespace Smpp.Tools.Protocol;

public class SubmitSmRespPdu : Pdu, IParseablePdu<SubmitSmRespPdu>
{
    public override PduCommandId Id => PduCommandId.submit_sm_resp;
    public string MessageId { get; set; } = string.Empty;

    public override byte[] GetBytes()
    {
        byte[] payload = [
            .. GetOctets((UInt32)Id),
            .. GetOctets((UInt32)Status),
            .. GetOctets((UInt32)SequenceNumber),
            .. GetCOctets(MessageId, DataCoding.Default)
        ];
        return [.. GetOctets((UInt32)payload.Length + 4), .. payload];
    }

    public static SubmitSmRespPdu FromBytes(byte[] bytes)
    {
        var pdu = new SubmitSmRespPdu();

        var command_id = ExtractId(bytes);
        if (command_id != PduCommandId.submit_sm_resp)
            throw new InvalidOperationException($"Invalid PDU type received, expected {PduCommandId.submit_sm_resp}, received {command_id}");

        var command_length = ExtractLength(bytes);
        if (command_length != bytes.Length)
            throw new InvalidOperationException($"Invalid PDU length, expected {command_length}, received {bytes.Length}");

        pdu.Status = ExtractStatus(bytes);
        pdu.SequenceNumber = ExtractSequenceNumber(bytes);
        pdu.MessageId = GetString([.. bytes.Skip(16).TakeWhile(x => x != 0x00)], DataCoding.Default);

        return pdu;
    }
}
using Smpp.Tools.Lib;
using System.Text;

namespace Smpp.Tools.Protocol;

public class SubmitSmPdu : Pdu, IParseablePdu<SubmitSmPdu>
{
    public override PduCommandId Id => PduCommandId.submit_sm;
    public string ServiceType { get; set; } = string.Empty;
    public AddressTon SourceAddressTon { get; set; } = AddressTon.Unknown;
    public AddressNpi SourceAddressNpi { get; set; } = AddressNpi.Unknown;
    public string SourceAddress { get; set; } = string.Empty;
    public AddressTon DestinationAddressTon { get; set; } = AddressTon.International;
    public AddressNpi DestinationAddressNpi { get; set; } = AddressNpi.ISDN;
    public string DestinationAddress { get; set; } = string.Empty;
    public byte ESMClass { get; set; }
    public byte ProtocolId { get; set; }
    public byte PriorityFlag { get; set; }
    public string ScheduleDeliveryTime { get; set; } = string.Empty;
    public string ValidityPeriod { get; set; } = string.Empty;
    public byte RegisteredDelivery { get; set; }
    public byte ReplaceIfPresent { get; set; }
    public DataCoding DataCoding { get; set; }
    public byte SmDefaultMsgId { get; set; }
    public byte SmLength => DataCoding switch
    {
        DataCoding.Default => (byte)Encoding.ASCII.GetByteCount(ShortMessage),
        DataCoding.UCS2 => (byte)Encoding.BigEndianUnicode.GetByteCount(ShortMessage),
        _ => throw new NotSupportedException($"Unsupported data coding: {DataCoding}")
    };
    public string ShortMessage { get; set; } = string.Empty;

    public override byte[] GetBytes()
    {
        byte[] payload = [
            .. GetOctets((UInt32)Id),
            .. GetOctets((UInt32)Status),
            .. GetOctets((UInt32)SequenceNumber),
            .. GetCOctets(ServiceType, DataCoding.Default),
            (byte) SourceAddressTon,
            (byte) SourceAddressNpi,
            .. GetCOctets(SourceAddress, DataCoding.Default),
            (byte) DestinationAddressTon,
            (byte) DestinationAddressNpi,
            .. GetCOctets(DestinationAddress, DataCoding.Default),
            ESMClass,
            ProtocolId,
            PriorityFlag,
            .. GetCOctets(ScheduleDeliveryTime, DataCoding.Default),
            .. GetCOctets(ValidityPeriod, DataCoding.Default),
            RegisteredDelivery,
            ReplaceIfPresent,
            (byte) DataCoding,
            SmDefaultMsgId,
            SmLength,
            .. GetOctets(ShortMessage, DataCoding)
        ];
        byte[] data = [.. GetOctets((UInt32)payload.Length + 4), .. payload];
        return data;
    }

    public static SubmitSmPdu FromBytes(byte[] bytes)
    {
        var pdu = new SubmitSmPdu();

        var command_id = ExtractId(bytes);
        if (command_id != PduCommandId.submit_sm)
            throw new InvalidOperationException($"Invalid PDU type received, expected {PduCommandId.submit_sm}, received {command_id}");

        var command_length = ExtractLength(bytes);
        if (command_length != bytes.Length)
            throw new InvalidOperationException($"Invalid PDU length, expected {command_length}, received {bytes.Length}");

        pdu.Status = ExtractStatus(bytes);
        pdu.SequenceNumber = ExtractSequenceNumber(bytes);

        int index = 16;
        pdu.ServiceType = GetString([.. bytes.Skip(index).TakeWhile(x => x != 0x00)], DataCoding.Default);
        index += pdu.ServiceType.Length + 1;

        pdu.SourceAddressTon = (AddressTon)bytes[index];
        index++;

        pdu.SourceAddressNpi = (AddressNpi)bytes[index];
        index++;

        pdu.SourceAddress = GetString([.. bytes.Skip(index).TakeWhile(x => x != 0x00)], DataCoding.Default);
        index += pdu.SourceAddress.Length + 1;

        pdu.DestinationAddressTon = (AddressTon)bytes[index];
        index++;

        pdu.DestinationAddressNpi = (AddressNpi)bytes[index];
        index++;

        pdu.DestinationAddress = GetString([.. bytes.Skip(index).TakeWhile(x => x != 0x00)], DataCoding.Default);
        index += pdu.DestinationAddress.Length + 1;

        pdu.ESMClass = bytes[index];
        index++;

        pdu.ProtocolId = bytes[index];
        index++;

        pdu.PriorityFlag = bytes[index];
        index++;

        pdu.ScheduleDeliveryTime = GetString([.. bytes.Skip(index).TakeWhile(x => x != 0x00)], DataCoding.Default);
        index += pdu.ScheduleDeliveryTime.Length + 1;

        pdu.ValidityPeriod = GetString([.. bytes.Skip(index).TakeWhile(x => x != 0x00)], DataCoding.Default);
        index += pdu.ValidityPeriod.Length + 1;

        pdu.RegisteredDelivery = bytes[index];
        index++;

        pdu.ReplaceIfPresent = bytes[index];
        index++;

        pdu.DataCoding = (DataCoding)bytes[index];
        index++;

        pdu.SmDefaultMsgId = bytes[index];
        index++;

        int smLength = bytes[index];
        index++;

        pdu.ShortMessage = GetString([.. bytes.Skip(index).Take(smLength)], pdu.DataCoding);

        return pdu;
    }
}
using Smpp.Tools.Lib;

namespace Smpp.Tools.Protocol;

public class BindTransceiverPdu : Pdu, IParseablePdu<BindTransceiverPdu>
{
    public override PduCommandId Id => PduCommandId.bind_transceiver;
    public string SystemId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string SystemType { get; set; } = string.Empty;
    public SmppVersion SmppVersion { get; set; } = SmppVersion.V3_4;
    public AddressTon AddressTon { get; set; } = AddressTon.Unknown;
    public AddressNpi AddressNpi { get; set; } = AddressNpi.Unknown;
    public string AddressRange { get; set; } = string.Empty;

    public override byte[] GetBytes()
    {
        byte[] payload = [
            .. GetOctets((UInt32)Id),
            .. GetOctets((UInt32)Status),
            .. GetOctets((UInt32)SequenceNumber),
            .. GetCOctets(SystemId, DataCoding.Default),
            .. GetCOctets(Password, DataCoding.Default),
            .. GetCOctets(SystemType, DataCoding.Default),
            (byte) SmppVersion,
            (byte) AddressTon,
            (byte) AddressNpi,
            .. GetCOctets(AddressRange, DataCoding.Default),
        ];
        return [.. GetOctets((UInt32)payload.Length + 4), .. payload];
    }

    public static BindTransceiverPdu FromBytes(byte[] bytes)
    {
        var pdu = new BindTransceiverPdu();

        var command_id = ExtractId(bytes);
        if (command_id != PduCommandId.bind_transceiver)
            throw new InvalidOperationException($"Invalid PDU type received, expected {PduCommandId.bind_transceiver}, received {command_id}");

        var command_length = ExtractLength(bytes);
        if (command_length != bytes.Length)
            throw new InvalidOperationException($"Invalid PDU length, expected {command_length}, received {bytes.Length}");

        pdu.Status = ExtractStatus(bytes);
        pdu.SequenceNumber = ExtractSequenceNumber(bytes);

        int index = 16;
        pdu.SystemId = GetString([.. bytes.Skip(index).TakeWhile(x => x != 0x00)], DataCoding.Default);
        index += pdu.SystemId.Length + 1;

        pdu.Password = GetString([.. bytes.Skip(index).TakeWhile(x => x != 0x00)], DataCoding.Default);
        index += pdu.Password.Length + 1;

        pdu.SystemType = GetString([.. bytes.Skip(index).TakeWhile(x => x != 0x00)], DataCoding.Default);
        index += pdu.SystemType.Length + 1;

        pdu.SmppVersion = (SmppVersion)bytes[index];
        index++;

        pdu.AddressTon = (AddressTon)bytes[index];
        index++;

        pdu.AddressNpi = (AddressNpi)bytes[index];
        index++;

        pdu.AddressRange = GetString([.. bytes.Skip(index).TakeWhile(x => x != 0x00)], DataCoding.Default);
        
        return pdu;
    }
}
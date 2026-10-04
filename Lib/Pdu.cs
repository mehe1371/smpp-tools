using Smpp.Tools.Protocol;
using System.Buffers.Binary;
using System.Text;

namespace Smpp.Tools.Lib;

public abstract class Pdu
{
    public PduCommandStatus Status { get; set; } = PduCommandStatus.ESME_ROK;
    internal int SequenceNumber { get; set; }
    public abstract PduCommandId Id { get; }
    public abstract byte[] GetBytes();

    private protected static byte[] GetOctets(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    private protected static byte[] GetCOctets(string value, DataCoding coding) => coding switch
    {
        DataCoding.Default => [.. Encoding.ASCII.GetBytes(value), 0x00],
        DataCoding.UCS2 => [.. Encoding.BigEndianUnicode.GetBytes(value), 0x00],
        _ => throw new NotSupportedException($"Unsupported data coding: {coding}"),
    };
    private protected static byte[] GetOctets(string value, DataCoding coding) => coding switch
    {
        DataCoding.Default => Encoding.ASCII.GetBytes(value),
        DataCoding.UCS2 => Encoding.BigEndianUnicode.GetBytes(value),
        _ => throw new NotSupportedException($"Unsupported data coding: {coding}"),
    };
    private protected static string GetString(byte[] data, DataCoding coding) => coding switch
    {
        DataCoding.Default => Encoding.ASCII.GetString(data),
        DataCoding.UCS2 => Encoding.BigEndianUnicode.GetString(data),
        _ => throw new NotSupportedException($"Unsupported data coding: {coding}")
    };



    protected internal static int ExtractLength(byte[] bytes)
    {
        return BinaryPrimitives.ReadInt32BigEndian(bytes[0..4]);
    }
    protected internal static PduCommandId ExtractId(byte[] bytes)
    {
        return (PduCommandId)BinaryPrimitives.ReadUInt32BigEndian(bytes[4..8]);
    }
    protected internal static PduCommandStatus ExtractStatus(byte[] bytes)
    {
        return (PduCommandStatus)BinaryPrimitives.ReadInt32BigEndian(bytes[8..12]);
    }
    protected internal static int ExtractSequenceNumber(byte[] bytes)
    {
        return BinaryPrimitives.ReadInt32BigEndian(bytes[12..16]);
    }
}
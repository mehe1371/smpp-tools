namespace Smpp.Tools.Protocol;

public enum AddressTon : Byte
{
    Unknown = 0b00000000,
    International = 0b00000001,
    National = 0b00000010,
    NetworkSpecific = 0b00000011,
    SubscriberNumber = 0b00000100,
    Alphanumeric = 0b00000101,
    Abbreviated = 0b00000110
}
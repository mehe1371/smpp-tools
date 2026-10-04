namespace Smpp.Tools.Protocol;

public enum AddressNpi : Byte
{
    Unknown = 0b00000000,
    ISDN = 0b00000001,
    Data = 0b00000011,
    Telex = 0b00000100,
    LandMobile = 0b00000110,
    National = 0b00001000,
    Private = 0b00001001,
    ERMES = 0b00001010,
    Internet = 0b00001110,
    WAPClientId = 0b00010010
}
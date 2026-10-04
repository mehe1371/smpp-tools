namespace Smpp.Tools.Lib;

internal interface IParseablePdu<TPdu>
    where TPdu : IParseablePdu<TPdu>
{
    static abstract TPdu FromBytes(byte[] bytes);
}
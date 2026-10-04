using Smpp.Tools.Protocol;

namespace Smpp.Tools.Events;

internal class EsmeSubmitArgs : EventArgs
{
    public AddressTon SourceAddressTon { get; set; }
    public AddressNpi SourceAddressNpi { get; set; }
    public string SourceAddress { get; set; } = string.Empty;
    public AddressTon DestinationAddressTon { get; set; }
    public AddressNpi DestinationAddressNpi { get; set; }
    public string DestinationAddress { get; set; } = string.Empty;
    public string ValidityPeriod { get; set; } = string.Empty;
    public bool RegisteredDelivery { get; set; }
    public DataCoding DataCoding { get; set; }
    public string ShortMessage { get; set; } = string.Empty;
    public SubmitSmRespPdu? Response { get; set; }
}
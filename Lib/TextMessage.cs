using Smpp.Tools.Protocol;

namespace Smpp.Tools.Lib;

public class TextMessage
{
    public string? ReferenceId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public bool RegisterDelivery { get; set; }
    public DataCoding DataCoding { get; set; }

    internal IEnumerable<SubmitSmPdu> GetSegments()
    {
        var maxLength = GetMaxSegmentLength(DataCoding);
        if (Text.Length < maxLength)
        {
            var pdu = new SubmitSmPdu
            {
                DataCoding = DataCoding,
                SourceAddress = Source,
                DestinationAddress = Destination,
                ShortMessage = Text,
                RegisteredDelivery = RegisterDelivery ? (byte)1 : (byte)0
            };
            return [pdu];
        }

        var avgLength = GetAverageSegmentLength(DataCoding);
        return Text.Chunk(avgLength)
            .Select(part => new SubmitSmPdu
            {
                DataCoding = DataCoding,
                SourceAddress = Source,
                DestinationAddress = Destination,
                ShortMessage = new string(part),
                RegisteredDelivery = RegisterDelivery ? (byte)1 : (byte)0
            });
    }

    private static int GetMaxSegmentLength(DataCoding dataCoding) => dataCoding switch
    {
        DataCoding.Default => 160,
        DataCoding.UCS2 => 70,
        _ => throw new NotImplementedException("DataCoding is not supported"),
    };
    private static int GetAverageSegmentLength(DataCoding dataCoding) => dataCoding switch
    {
        DataCoding.Default => 153,
        DataCoding.UCS2 => 67,
        _ => throw new NotImplementedException("DataCoding is not supported"),
    };
}
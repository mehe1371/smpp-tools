using Smpp.Tools.Events;
using Smpp.Tools.Protocol;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Smpp.Tools.Lib;

public class Esme
{
    public string SystemId { get; private set; } = string.Empty;
    public SessionState State => session.State;
    public string RemoteEndpoint { get; private set; }

    internal event EventHandler<EsmeBindArgs>? OnBind;
    internal event EventHandler<EsmeSubmitArgs>? OnSubmit;

    private readonly ServerSession session;
    private readonly PeriodicTimer enquireLinkTimer;

    internal Esme(TcpClient tcpClient, TimeSpan enquireLinkInterval)
    {
        session = new ServerSession(tcpClient);
        session.OnPduReceived += HandleResponse;

        var clientEndpoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
        RemoteEndpoint = $"{clientEndpoint?.Address}:{clientEndpoint?.Port}";

        enquireLinkTimer = new PeriodicTimer(enquireLinkInterval);
        _ = EnquireLinkAsync();
    }

    public async Task<DeliverSmRespPdu> SendDeliveryReceipt(string receipt)
    {
        if (session.State != SessionState.Bound_TRX)
            throw new InvalidOperationException("Session is not bound yet");

        var pdu = new DeliverSmPdu
        {
            ESMClass = 0x04,
            ShortMessage = receipt
        };
        return await session.SubmitAsync<DeliverSmRespPdu>(pdu);
    }

    public async Task<DeliverSmRespPdu> SendDeliveryReceipt(DeliveryReceipt receipt)
    {
        var receiptStr = $"id:{receipt.Id} sub:{receipt.Sub} dlvrd:{receipt.Dlvrd} submit date:{DateTime.UtcNow:yyMMddHHmm} done date:{receipt.DoneDate:yyMMddHHmm} stat:{receipt.Stat} err:{receipt.Err} text: ";

        return await SendDeliveryReceipt(receiptStr);
    }

    public async Task<bool> TryUnbindAsync()
    {
        UnbindPdu request = new();
        try
        {
            var response = await session.SubmitAsync<UnbindRespPdu>(request);
            return response.Status == PduCommandStatus.ESME_ROK;
        }
        catch
        {
            return false;
        }
    }

    public async Task Disconnect()
    {
        session.TryClose();
    }

    private async void HandleResponse(object? sender, byte[] payload)
    {
        var command_id = (PduCommandId)BinaryPrimitives.ReadUInt32BigEndian(payload[4..8]);
        switch (command_id)
        {
            case PduCommandId.enquire_link:
            {
                var request = EnquireLinkPdu.FromBytes(payload);
                var response = new EnquireLinkRespPdu
                {
                    Status = PduCommandStatus.ESME_ROK,
                    SequenceNumber = request.SequenceNumber
                };
                await session.SubmitAsync(response);

                break;
            }
            case PduCommandId.bind_transceiver:
            {
                var request = BindTransceiverPdu.FromBytes(payload);
                if (session.State == SessionState.Bound_TRX)
                {
                    var response = new BindTransceiverRespPdu
                    {
                        Status = PduCommandStatus.ESME_RALYBND,
                        SequenceNumber = request.SequenceNumber
                    };
                    await session.SubmitAsync(response);
                    break;
                }
                if (string.IsNullOrWhiteSpace(request.SystemId) || request.SystemId.Length > 15)
                {
                    var response = new BindTransceiverRespPdu
                    {
                        Status = PduCommandStatus.ESME_RINVSYSID,
                        SequenceNumber = request.SequenceNumber
                    };
                    await session.SubmitAsync(response);
                    break;
                }
                if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 8)
                {
                    var response = new BindTransceiverRespPdu
                    {
                        Status = PduCommandStatus.ESME_RINVPASWD,
                        SequenceNumber = request.SequenceNumber
                    };
                    await session.SubmitAsync(response);
                    break;
                }
                var message = new EsmeBindArgs
                {
                    SystemId = request.SystemId,
                    Password = request.Password,
                    AddressTon = request.AddressTon,
                    AddressNpi = request.AddressNpi
                };
                try
                {
                    OnBind?.Invoke(this, message);
                }
                catch
                {
                    var response = new BindTransceiverRespPdu
                    {
                        Status = PduCommandStatus.ESME_RSYSERR,
                        SequenceNumber = request.SequenceNumber
                    };
                    await session.SubmitAsync(response);
                    break;
                }
                if (message.Response != null)
                {
                    var response = message.Response;
                    if (response.Status == PduCommandStatus.ESME_ROK)
                    {
                        session.State = SessionState.Bound_TRX;
                        SystemId = request.SystemId;
                    }
                    response.SequenceNumber = request.SequenceNumber;
                    await session.SubmitAsync(response);
                }
                else
                {
                    var response = new BindTransceiverRespPdu
                    {
                        Status = PduCommandStatus.ESME_RBINDFAIL,
                        SystemId = request.SystemId
                    };
                    await session.SubmitAsync(response);
                }
                break;
            }
            case PduCommandId.submit_sm:
            {
                var request = SubmitSmPdu.FromBytes(payload);
                if (session.State != SessionState.Bound_TRX)
                {
                    var response = new SubmitSmRespPdu
                    {
                        Status = PduCommandStatus.ESME_RINVBNDSTS,
                        SequenceNumber = request.SequenceNumber
                    };
                    await session.SubmitAsync(response);
                    break;
                }
                var message = new EsmeSubmitArgs
                {
                    SourceAddressTon = request.SourceAddressTon,
                    SourceAddressNpi = request.SourceAddressNpi,
                    SourceAddress = request.SourceAddress,
                    DestinationAddressTon = request.DestinationAddressTon,
                    DestinationAddressNpi = request.DestinationAddressNpi,
                    DestinationAddress = request.DestinationAddress,
                    ValidityPeriod = request.ValidityPeriod,
                    RegisteredDelivery = request.RegisteredDelivery == 1,
                    DataCoding = request.DataCoding,
                    ShortMessage = request.ShortMessage
                };
                try
                {
                    OnSubmit?.Invoke(this, message);
                }
                catch
                {
                    var response = new SubmitSmRespPdu
                    {
                        Status = PduCommandStatus.ESME_RSYSERR,
                        SequenceNumber = request.SequenceNumber
                    };
                    await session.SubmitAsync(response);
                    break;
                }
                if (message.Response != null)
                {
                    var response = message.Response;
                    response.SequenceNumber = request.SequenceNumber;
                    await session.SubmitAsync(response);
                }
                break;
            }
            case PduCommandId.unbind:
            {
                var request = EnquireLinkPdu.FromBytes(payload);
                var response = new UnbindRespPdu
                {
                    Status = PduCommandStatus.ESME_ROK,
                    SequenceNumber = request.SequenceNumber
                };
                session.State = SessionState.Unbound;
                await session.SubmitAsync(response);

                break;
            }
            default:
            {
                var sequenceNumber = Pdu.ExtractSequenceNumber(payload);
                var response = new GenericNackPdu
                {
                    Status = PduCommandStatus.ESME_RINVCMDID,
                    SequenceNumber = sequenceNumber
                };
                await session.SubmitAsync(response);
            }
            break;
        }
    }

    private async Task EnquireLinkAsync()
    {
        while (await enquireLinkTimer.WaitForNextTickAsync())
        {
            var enquireLinkPdu = new EnquireLinkPdu();
            try
            {
                var response = await session.SubmitAsync<EnquireLinkRespPdu>(enquireLinkPdu);
                if (response.Status != PduCommandStatus.ESME_ROK)
                    session.TryClose();
            }
            catch
            {
                session.TryClose();
            }
        }
    }
}
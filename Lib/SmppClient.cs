using Smpp.Tools.Events;
using Smpp.Tools.Exceptions;
using Smpp.Tools.Protocol;
using System.Buffers.Binary;

namespace Smpp.Tools.Lib;

public class SmppClient
{
    public string Host { get; set; }
    public ushort Port { get; set; }
    public string SystemId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(3);
    public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(10);

    private readonly ClientSession session;
    private readonly SemaphoreSlim bindLock = new(1, 1);

    public event EventHandler<SegmentSentArgs>? OnSegmentSent;
    public event EventHandler<SmsSentArgs>? OnSmsSent;
    public event EventHandler<SegmentDeliveredArgs>? OnSegmentDelivered;
    public event EventHandler<SegmentReceivedArgs>? OnSegmentReceived;

    public SmppClient(string host, ushort port)
    {
        Host = host;
        Port = port;
        session = new ClientSession(host, port);
        session.OnPduReceived += HandleResponse;
    }

    public async Task ConnectAsync()
    {
        if (session.State != SessionState.Closed)
            throw new InvalidOperationException("The connection is already open");

        if (string.IsNullOrWhiteSpace(Host))
            throw new InvalidOperationException("Host name cannot be empty");

        if (string.IsNullOrWhiteSpace(SystemId))
            throw new InvalidOperationException("SystemId must be specified");

        if (SystemId.Length > 15)
            throw new InvalidOperationException("SystemId can have a maximum of 15 characters");

        if (Password.Length > 8)
            throw new InvalidOperationException("Password can have a maximum of 8 characters");

        session.CommandTimeout = CommandTimeout;
        session.ReconnectInterval = ReconnectInterval;

        await session.OpenAsync();

        await BindAsync();
    }

    public async Task DisconnectAsync()
    {
        if (session.State == SessionState.Closed)
            throw new InvalidOperationException("The client is not connected");

        await session.CloseAsync();
    }

    private async Task BindAsync()
    {
        await bindLock.WaitAsync();
        try
        {
            if (session.State == SessionState.Bound_TRX)
                return;

            var bindPdu = new BindTransceiverPdu
            {
                Status = PduCommandStatus.ESME_ROK,
                SystemId = SystemId,
                Password = Password
            };

            var response = await session.SubmitAsync<BindTransceiverRespPdu>(bindPdu);
            if (response.Status != PduCommandStatus.ESME_ROK)
                throw new SmppException("Failed to bind SMPP session", response.Id, response.Status);

            session.State = SessionState.Bound_TRX;
        }
        catch
        {
            throw;
        }
        finally
        {
            bindLock.Release();
        }
    }

    public async Task SendMessageAsync(TextMessage sms)
    {
        if (session.State != SessionState.Bound_TRX)
            await BindAsync();

        List<string> messageIds = [];
        foreach (var pdu in sms.GetSegments())
        {
            var response = await session.SubmitAsync<SubmitSmRespPdu>(pdu);
            if (response.Status != PduCommandStatus.ESME_ROK)
                throw new SmppException("Failed to submit PDU", pdu.Id, response.Status);

            messageIds.Add(response.MessageId);
            OnSegmentSent?.Invoke(this, new()
            {
                Source = sms.Source,
                Destination = sms.Destination,
                ReferenceId = sms.ReferenceId,
                MessageId = response.MessageId
            });
        }
        OnSmsSent?.Invoke(this, new()
        {
            Source = sms.Source,
            Destination = sms.Destination,
            ReferenceId = sms.ReferenceId,
            MessageIds = string.Join(',', messageIds)
        });
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
            case PduCommandId.deliver_sm:
            {
                var request = DeliverSmPdu.FromBytes(payload);
                var response = new DeliverSmRespPdu
                {
                    Status = PduCommandStatus.ESME_ROK,
                    SequenceNumber = request.SequenceNumber
                };
                await session.SubmitAsync(response);

                if ((request.ESMClass & 0x0C) == 0x04)
                    OnSegmentDelivered?.Invoke(this, new()
                    {
                        DeliveryReceipt = request.ShortMessage
                    });
                else
                    OnSegmentReceived?.Invoke(this, new()
                    {
                        Source = request.SourceAddress,
                        Destination = request.DestinationAddress,
                        Text = request.ShortMessage,
                    });

                break;
            }
            case PduCommandId.unbind:
            {
                var request = UnbindPdu.FromBytes(payload);
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

                break;
            }
        }
    }
}
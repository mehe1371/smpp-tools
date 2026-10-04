using Smpp.Tools.Events;
using Smpp.Tools.Protocol;

namespace Smpp.Tools.Lib;

public class SmscSimulator
{
    private long messageId;
    private SmppServer server;
    public SmscSimulator(ushort port)
    {
        messageId = 70000000000;
        server = new SmppServer(port);
        server.OnClientBind += OnBind;
        server.OnClientSubmit += OnSubmit;

    }

    public void Start() => server.Start();
    public async Task Stop() => await server.StopAsync();

    private void OnBind(object sender, Esme client, ClientBindArgs args)
    {
        args.Response = new BindTransceiverRespPdu
        {
            Status = PduCommandStatus.ESME_ROK,
            SystemId = args.SystemId
        };
    }

    private void OnSubmit(object sender, Esme client, ClientSubmitArgs args)
    {
        long nextMessageId;
        do
        {
            long current = Volatile.Read(ref messageId);
            nextMessageId = current + 1;
        }
        while (Interlocked.CompareExchange(
            ref messageId,
            nextMessageId,
            Volatile.Read(ref messageId)) != nextMessageId - 1);

        args.Response = new SubmitSmRespPdu
        {
            Status = PduCommandStatus.ESME_ROK,
            MessageId = nextMessageId.ToString("X")
        };
        var receipt = new DeliveryReceipt
        {
            Dlvrd = 1,
            DoneDate = DateTime.UtcNow,
            Err = "000",
            Id = nextMessageId.ToString(),
            Stat = "DELIVRD",
            Sub = "001"
        };
        Task.Run(async () =>
        {
            var delay = new Random().Next(100, 2000);
            await Task.Delay(delay);
            await client.SendDeliveryReceipt(receipt);
        });
    }
}
using Smpp.Tools.Events;
using Smpp.Tools.Protocol;
using System.Net;
using System.Net.Sockets;

namespace Smpp.Tools.Lib;

public class SmppServer
{
    public TimeSpan EnquireLinkInterval { get; set; }
    public ICollection<Esme> Clients { get; set; } = [];
    public SmppServerStatus Status { get; private set; }

    public event ClientSubmitEventHandler? OnClientSubmit;
    public event ClientBindEventHandler? OnClientBind;

    private readonly TcpListener listener;
    private readonly PeriodicTimer clientCleanupTimer;

    public SmppServer(ushort port)
    {
        listener = new(IPAddress.Any, port);

        Status = SmppServerStatus.Offline;
        EnquireLinkInterval = TimeSpan.FromSeconds(5);

        clientCleanupTimer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        _ = ClientCleanupAsync();
    }

    public void Start()
    {
        if (Status == SmppServerStatus.Running)
            throw new InvalidOperationException("The server is already started");

        if (Status == SmppServerStatus.Stopping)
            throw new InvalidOperationException("The server is about to stop and cannot be started right now");

        listener.Start();
        Status = SmppServerStatus.Running;
        _ = RunAsync();
    }

    public async Task StopAsync()
    {
        if (Status == SmppServerStatus.Offline)
            throw new InvalidOperationException("The server is already stopped");

        if (Status == SmppServerStatus.Stopping)
            return;

        Status = SmppServerStatus.Stopping;
        listener.Stop();
        foreach (var client in Clients)
        {
            if (client.State != SessionState.Bound_TRX)
                continue;

            await client.TryUnbindAsync();
            await client.Disconnect();
        }
        Clients.Clear();
        Status = SmppServerStatus.Offline;
    }

    private async Task RunAsync()
    {
        try
        {
            while (true)
            {
                var tcpClient = await listener.AcceptTcpClientAsync();
                var esme = new Esme(tcpClient, EnquireLinkInterval);
                esme.OnBind += OnEsmeBind;
                esme.OnSubmit += OnEsmeSubmit;

                Clients.Add(esme);
            }
        }
        finally
        {
            await StopAsync();
        }
    }

    private void OnEsmeBind(object? sender, EsmeBindArgs e)
    {
        if (Status == SmppServerStatus.Offline)
            throw new InvalidOperationException("Tried to bind to an offline server");

        if (Status == SmppServerStatus.Stopping)
        {
            e.Response = new BindTransceiverRespPdu
            {
                Status = PduCommandStatus.ESME_RSYSERR,
                SystemId = e.SystemId
            };
            return;
        }

        var esme = (Esme)sender!;
        var args = new ClientBindArgs
        {
            SystemId = e.SystemId,
            Password = e.Password,
            AddressNpi = e.AddressNpi,
            AddressTon = e.AddressTon
        };
        OnClientBind?.Invoke(this, esme, args);
        e.Response = args.Response;
    }

    private void OnEsmeSubmit(object? sender, EsmeSubmitArgs e)
    {
        var esme = (Esme)sender!;
        var args = new ClientSubmitArgs
        {
            DestinationAddress = e.DestinationAddress,
            DestinationAddressNpi = e.DestinationAddressNpi,
            DestinationAddressTon = e.DestinationAddressTon,
            SourceAddress = e.SourceAddress,
            SourceAddressNpi = e.SourceAddressNpi,
            SourceAddressTon = e.SourceAddressTon,
            DataCoding = e.DataCoding,
            ShortMessage = e.ShortMessage,
            RegisteredDelivery = e.RegisteredDelivery,
            ValidityPeriod = e.ValidityPeriod
        };
        OnClientSubmit?.Invoke(this, esme, args);
        e.Response = args.Response;
    }

    private async Task ClientCleanupAsync()
    {
        while (await clientCleanupTimer.WaitForNextTickAsync())
        {
            foreach (var client in Clients.Where(x => x.State is SessionState.Closed or SessionState.Unbound))
                await client.Disconnect();
        }
    }
}
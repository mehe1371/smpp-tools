using Smpp.Tools.Protocol;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Smpp.Tools.Lib;

internal class ClientSession
{
    public string Host { get; set; } = string.Empty;
    public ushort Port { get; set; }
    public TimeSpan CommandTimeout { get; set; }
    public TimeSpan ReconnectInterval { get; set; }
    public SessionState State { get; set; }

    public event EventHandler<byte[]>? OnPduReceived;

    private int currentSequenceNumber;
    private NetworkStream? stream;
    private TcpClient? tcpClient;
    private Task? task;
    private CancellationTokenSource? cancellationSource;

    private readonly ConcurrentDictionary<(int sequenceNumber, uint id), TaskCompletionSource<byte[]>> responseQueue = [];
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly SemaphoreSlim stateLock = new(1, 1);

    public ClientSession(string host, ushort port)
    {
        currentSequenceNumber = 0;
        Host = host;
        Port = port;
        State = SessionState.Closed;
    }

    public async Task OpenAsync()
    {
        await stateLock.WaitAsync();
        try
        {
            if (State == SessionState.Open)
                throw new InvalidOperationException("Already connected");

            cancellationSource = new CancellationTokenSource();
            task = RunAsync(cancellationSource.Token);
        }
        finally
        {
            stateLock.Release();
        }
    }

    public async Task CloseAsync()
    {
        await stateLock.WaitAsync();
        try
        {
            if (cancellationSource == null || task == null)
                return;

            cancellationSource.Cancel();
            await task;
        }
        finally
        {
            cancellationSource?.Dispose();
            cancellationSource = null;
            task = null;
            tcpClient?.Dispose();
            State = SessionState.Closed;
            foreach (var waitedTask in responseQueue)
            {
                if (responseQueue.TryRemove(waitedTask.Key, out var tcs))
                    tcs.TrySetException(new IOException("Connection lost while awaiting the response PDU"));
            }
            stateLock.Release();
        }
    }

    public async Task<TResponse> SubmitAsync<TResponse>(Pdu pdu)
        where TResponse : Pdu, IParseablePdu<TResponse>
    {
        if (stream == null)
            throw new InvalidOperationException("Client is not connected");

        int nextSequenceNumber;
        do
        {
            int current = Volatile.Read(ref currentSequenceNumber);
            nextSequenceNumber = current == int.MaxValue ? 1 : current + 1;
        }
        while (Interlocked.CompareExchange(
            ref currentSequenceNumber,
            nextSequenceNumber,
            Volatile.Read(ref currentSequenceNumber)) != nextSequenceNumber - 1);

        pdu.SequenceNumber = nextSequenceNumber;

        var wait = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

        var responseId = (uint)pdu.Id | 0x80000000u;
        responseQueue.TryAdd((nextSequenceNumber, responseId), wait);

        await WriteAsync(pdu.GetBytes());
        byte[] responsePayload;
        try
        {
            responsePayload = await wait.Task.WaitAsync(CommandTimeout);
        }
        catch
        {
            responseQueue.TryRemove((nextSequenceNumber, responseId), out _);
            throw;
        }
        return TResponse.FromBytes(responsePayload);
    }

    public async Task SubmitAsync(Pdu pdu)
    {
        if (stream == null)
            throw new InvalidOperationException("Client is not connected");

        await WriteAsync(pdu.GetBytes());
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = new TcpClient();
                try
                {
                    await client.ConnectAsync(Host, Port, cancellationToken);

                    tcpClient = client;
                    stream = client.GetStream();
                    State = SessionState.Open;
                }
                catch
                {
                    client.Dispose();
                    throw;
                }
                await ReceiveLoopAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                stream = null;
                var client = Interlocked.Exchange(ref tcpClient, null);
                client?.Dispose();
                State = SessionState.Closed;
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        if (tcpClient == null || stream == null)
            return;

        byte[] buffer = new byte[65535];
        int buffered = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int bytesRead = await stream.ReadAsync(buffer.AsMemory(buffered), cancellationToken);
            if (bytesRead == 0)
                throw new IOException("Client has closed the connection");

            buffered += bytesRead;
            while (buffered >= 16)
            {
                int commandLength = Pdu.ExtractLength(buffer);
                if (commandLength < 16 || commandLength > 65535)
                {
                    await SendNackAsync(PduCommandStatus.ESME_RINVCMDLEN);
                    throw new IOException("Synchronization failure");
                }
                var commandId = Pdu.ExtractId(buffer);
                if (!Enum.IsDefined(commandId))
                {
                    await SendNackAsync(PduCommandStatus.ESME_RINVCMDID);
                    throw new IOException("Synchronization failure");
                }
                if (buffered < commandLength)
                    break;

                byte[] payload = new byte[commandLength];
                Buffer.BlockCopy(buffer, 0, payload, 0, commandLength);

                var sequenceNumber = Pdu.ExtractSequenceNumber(buffer);
                if (responseQueue.TryRemove((sequenceNumber, (uint)commandId), out var prs))
                    prs.TrySetResult(payload);
                else
                    OnPduReceived?.Invoke(this, payload);

                buffered -= commandLength;
                if (buffered > 0)
                    Buffer.BlockCopy(buffer, commandLength, buffer, 0, buffered);
            }
        }
        async Task SendNackAsync(PduCommandStatus status)
        {
            var response = new GenericNackPdu
            {
                Status = status,
                SequenceNumber = 0
            };

            await WriteAsync(response.GetBytes());
        }
    }

    private async Task WriteAsync(ReadOnlyMemory<byte> data)
    {
        if (stream == null || State == SessionState.Closed)
            throw new InvalidOperationException("Client is not connected");

        await writeLock.WaitAsync();
        try
        {
            await stream.WriteAsync(data);
        }
        finally
        {
            writeLock.Release();
        }
    }
}
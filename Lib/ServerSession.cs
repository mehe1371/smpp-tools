using Smpp.Tools.Protocol;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Smpp.Tools.Lib;

internal class ServerSession
{
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public SessionState State { get; set; }

    internal event EventHandler<byte[]>? OnPduReceived;

    private int currentSequenceNumber;
    private readonly TcpClient tcpClient;
    private readonly NetworkStream stream;
    private readonly ConcurrentDictionary<(int sequenceNumber, uint id), TaskCompletionSource<byte[]>> responseQueue = [];
    private readonly SemaphoreSlim writeLock = new(1, 1);

    public ServerSession(TcpClient _tcpClient)
    {
        tcpClient = _tcpClient;
        stream = tcpClient.GetStream();
        currentSequenceNumber = 0;
        State = SessionState.Open;

        _ = ReceiveLoopAsync();
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
            responseQueue.TryRemove((nextSequenceNumber, responseId), out var prs);
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

    public bool TryClose()
    {
        try
        {
            tcpClient.Close();
            return true;
        }
        catch { return false; }
        finally
        {
            State = SessionState.Closed;
        }
    }

    private async Task ReceiveLoopAsync()
    {
        byte[] buffer = new byte[65535];
        int buffered = 0;

        try
        {
            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer.AsMemory(buffered));
                if (bytesRead == 0)
                    return;

                buffered += bytesRead;
                while (buffered >= 16)
                {
                    int commandLength = Pdu.ExtractLength(buffer);
                    if (commandLength < 16 || commandLength > 65535)
                    {
                        await SendNackAsync(PduCommandStatus.ESME_RINVCMDLEN);
                        return;
                    }
                    var commandId = Pdu.ExtractId(buffer);
                    if (!Enum.IsDefined(commandId))
                    {
                        await SendNackAsync(PduCommandStatus.ESME_RINVCMDID);
                        return;
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
        }
        finally
        {
            State = SessionState.Closed;
            tcpClient.Dispose();
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
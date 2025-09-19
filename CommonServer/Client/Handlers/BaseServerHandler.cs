using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CommonServer.Shared;

public abstract class BaseServerHandler
{
    private readonly ConcurrentQueue<BasePacket> _sendQueue = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private NetworkStream _stream;

    public async Task HandleMainLoopAsync(TcpClient client)
    {
        _stream = client.GetStream();
        var token = _cancellationTokenSource.Token;

        await OnConnectedAsync();

        var receiveTask = ReceiveLoopAsync(token);
        var sendTask = SendLoopAsync(token);

        await Task.WhenAny(receiveTask, sendTask);

        await OnDisconnectedAsync();
        client.Close();
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                byte[] idBuffer = new byte[4];
                int bytesRead = await _stream.ReadAsync(idBuffer, 0, 4, token);
                if (bytesRead == 0) break;
                int protocolId = BitConverter.ToInt32(idBuffer, 0);

                byte[] lengthBuffer = new byte[4];
                await _stream.ReadAsync(lengthBuffer, 0, 4, token);
                int packetLength = BitConverter.ToInt32(lengthBuffer, 0);

                byte[] dataBuffer = new byte[packetLength];
                await _stream.ReadAsync(dataBuffer, 0, packetLength, token);

                using var stream = new MemoryStream(dataBuffer);
                var packet = PacketManager.CreatePacket(protocolId);
                if (packet != null)
                {
                    packet.Deserialize(stream);
                    await HandlePacketAsync(packet);
                }
            }
        }
        catch (Exception ex) { Console.WriteLine($"Receive Error: {ex.Message}"); }
    }

    private async Task SendLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_sendQueue.TryDequeue(out var packet))
                {
                    using var memoryStream = new MemoryStream();
                    memoryStream.Write(BitConverter.GetBytes(packet.ProtocolID), 0, 4);
                    using var dataStream = new MemoryStream();
                    packet.Serialize(dataStream);
                    byte[] data = dataStream.ToArray();
                    memoryStream.Write(BitConverter.GetBytes(data.Length), 0, 4);
                    memoryStream.Write(data, 0, data.Length);
                    byte[] buffer = memoryStream.ToArray();
                    await _stream.WriteAsync(buffer, 0, buffer.Length, token);
                }
                await Task.Delay(10, token);
            }
        }
        catch (Exception ex) { Console.WriteLine($"Send Error: {ex.Message}"); }
    }

    public void SendMessage(BasePacket packet)
    {
        _sendQueue.Enqueue(packet);
    }

    public void Shutdown()
    {
        _cancellationTokenSource.Cancel();
    }

    protected abstract Task HandlePacketAsync(BasePacket packet);
    protected virtual Task OnConnectedAsync() => Task.CompletedTask;
    protected virtual Task OnDisconnectedAsync() => Task.CompletedTask;
}

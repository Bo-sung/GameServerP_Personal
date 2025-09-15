using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 서버와의 네트워크 통신을 처리하는 추상 기본 클래스
/// 클라이언트가 다양한 서버와 통신할 때 공통 기능을 제공
/// </summary>
public abstract class BaseServerHandler
{
    private readonly ConcurrentQueue<Protocol> _messageQueue = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public async Task HandleMainLoopAsync(TcpClient client)
    {
        using var ns = client.GetStream();
        using var reader = new StreamReader(ns, Encoding.UTF8);
        using var writer = new StreamWriter(ns, Encoding.UTF8) { AutoFlush = true };

        var token = _cancellationTokenSource.Token;

        // 연결 시작 이벤트
        await OnConnectedAsync();

        // 메시지 수신 태스크
        var receiveTask = ReceiveMessagesAsync(reader, token);

        // 메시지 송신 태스크
        var sendTask = SendMessagesAsync(writer, token);

        try
        {
            // 두 태스크를 동시에 실행
            await Task.WhenAny(receiveTask, sendTask);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Handler error: {ex.Message}");
            await OnErrorAsync(ex);
        }
        finally
        {
            await OnDisconnectedAsync();
            _cancellationTokenSource.Cancel();
            client.Close();
        }
    }

    /// <summary>
    /// 서버로부터 메시지를 수신하는 태스크
    /// </summary>
    private async Task ReceiveMessagesAsync(StreamReader reader, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var message = await reader.ReadLineAsync();

                if (message == null)
                {
                    Console.WriteLine("서버 연결이 종료되었습니다.");
                    break;
                }

                // 받은 메시지 처리
                await ProcessReceivedMessageAsync(message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"메시지 수신 오류: {ex.Message}");
            await OnReceiveErrorAsync(ex);
        }
    }

    /// <summary>
    /// 서버로 메시지를 송신하는 태스크
    /// </summary>
    private async Task SendMessagesAsync(StreamWriter writer, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_messageQueue.TryDequeue(out var protocol))
                {
                    await writer.WriteLineAsync(protocol.ToString());
                    Console.WriteLine($"메시지 전송: ID={protocol.ID}, Type={protocol.protocolType}");
                    await OnMessageSentAsync(protocol);
                }

                // CPU 사용률을 줄이기 위한 짧은 대기
                await Task.Delay(10, token);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"메시지 송신 오류: {ex.Message}");
            await OnSendErrorAsync(ex);
        }
    }

    private async Task ProcessReceivedMessageAsync(string message)
    {
        try
        {
            Console.WriteLine($"메시지 수신: {message}");

            // Protocol 메시지 파싱
            var protocol = ParseProtocol(message);

            if (protocol != null)
            {
                await OnMessageReceivedAsync(protocol);
                await HandleProtocolAsync(protocol);
            }
            else
            {
                Console.WriteLine("프로토콜 파싱 실패");
                await OnParseErrorAsync(message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"메시지 처리 오류: {ex.Message}");
            await OnProcessErrorAsync(ex, message);
        }
    }

    private Protocol ParseProtocol(string message)
    {
        try
        {
            // 메시지에서 ID 추출 (첫 번째 !@ 전까지)
            var tokIndex = message.IndexOf(Protocol.STREAM_TOK);
            if (tokIndex == -1)
            {
                // 파라미터가 없는 경우, #@@ 앞까지가 ID
                var endIndex = message.IndexOf(Protocol.STREAM_END);
                if (endIndex != -1)
                {
                    if (int.TryParse(message.Substring(0, endIndex), out int idOnly))
                    {
                        return new Protocol(idOnly, Protocol.ProtocolType.ToServer);
                    }
                }
                return null;
            }

            var idPart = message.Substring(0, tokIndex);
            if (!int.TryParse(idPart, out int id))
            {
                return null;
            }

            var protocol = new Protocol(id, Protocol.ProtocolType.Both);

            // 파라미터 파싱
            var paramPart = message.Substring(tokIndex);
            var parameters = protocol.FromString(paramPart);

            foreach (var param in parameters)
            {
                protocol.SetParam(param.Key, param.Value);
            }

            return protocol;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"프로토콜 파싱 오류: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 서버로 메시지를 전송 큐에 추가
    /// </summary>
    public void SendMessage(Protocol protocol)
    {
        if (protocol.protocolType == Protocol.ProtocolType.ToServer ||
            protocol.protocolType == Protocol.ProtocolType.Both)
        {
            _messageQueue.Enqueue(protocol);
        }
        else
        {
            Console.WriteLine($"서버로 전송할 수 없는 프로토콜: {protocol.ID}");
        }
    }

    /// <summary>
    /// 핸들러 종료
    /// </summary>
    public void Shutdown()
    {
        _cancellationTokenSource.Cancel();
    }

    // 추상 메서드 - 하위 클래스에서 구현
    protected abstract Task HandleProtocolAsync(Protocol protocol);

    // 가상 메서드들 - 필요시 하위 클래스에서 오버라이드
    protected virtual Task OnConnectedAsync() => Task.CompletedTask;
    protected virtual Task OnDisconnectedAsync() => Task.CompletedTask;
    protected virtual Task OnMessageReceivedAsync(Protocol protocol) => Task.CompletedTask;
    protected virtual Task OnMessageSentAsync(Protocol protocol) => Task.CompletedTask;
    protected virtual Task OnErrorAsync(Exception ex) => Task.CompletedTask;
    protected virtual Task OnReceiveErrorAsync(Exception ex) => Task.CompletedTask;
    protected virtual Task OnSendErrorAsync(Exception ex) => Task.CompletedTask;
    protected virtual Task OnParseErrorAsync(string message) => Task.CompletedTask;
    protected virtual Task OnProcessErrorAsync(Exception ex, string message) => Task.CompletedTask;
}
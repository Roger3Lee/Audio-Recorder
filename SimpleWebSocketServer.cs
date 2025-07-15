using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AudioRecorder
{
    public class SimpleWebSocketMessage
    {
        public string Command { get; set; } = "";
        public object? Data { get; set; }
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class SimpleWebSocketServer : IDisposable
    {
        private HttpListener? httpListener;
        private CancellationTokenSource? cancellationTokenSource;
        private readonly ConcurrentDictionary<string, WebSocket> clients;
        private readonly SimpleAudioRecorder audioRecorder;
        private bool isRunning = false;
        private readonly int port;

        public event EventHandler<string>? StatusChanged;

        public SimpleWebSocketServer(SimpleAudioRecorder audioRecorder, int port = 8080)
        {
            this.audioRecorder = audioRecorder;
            this.port = port;
            this.clients = new ConcurrentDictionary<string, WebSocket>();
            
            // 监听音频录制器事件
            audioRecorder.StatusChanged += AudioRecorder_StatusChanged;
            audioRecorder.ErrorOccurred += AudioRecorder_ErrorOccurred;
        }

        public async Task StartAsync()
        {
            if (isRunning) return;

            try
            {
                httpListener = new HttpListener();
                httpListener.Prefixes.Add($"http://localhost:{port}/");
                httpListener.Start();

                cancellationTokenSource = new CancellationTokenSource();
                isRunning = true;

                StatusChanged?.Invoke(this, $"WebSocket服务器已启动，监听端口: {port}");

                _ = Task.Run(() => ListenForConnectionsAsync(cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"WebSocket服务器启动失败: {ex.Message}");
            }
        }

        private async Task ListenForConnectionsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && httpListener != null)
            {
                try
                {
                    var context = await httpListener.GetContextAsync();
                    
                    if (context.Request.IsWebSocketRequest)
                    {
                        _ = Task.Run(() => HandleWebSocketAsync(context), cancellationToken);
                    }
                    else
                    {
                        await ServeTestPageAsync(context);
                    }
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    StatusChanged?.Invoke(this, $"WebSocket连接错误: {ex.Message}");
                }
            }
        }

        private async Task HandleWebSocketAsync(HttpListenerContext context)
        {
            WebSocket? webSocket = null;
            string clientId = Guid.NewGuid().ToString();

            try
            {
                var webSocketContext = await context.AcceptWebSocketAsync(null);
                webSocket = webSocketContext.WebSocket;
                
                clients.TryAdd(clientId, webSocket);
                StatusChanged?.Invoke(this, $"客户端已连接: {clientId}");

                await HandleClientMessagesAsync(webSocket, clientId);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"WebSocket处理错误: {ex.Message}");
            }
            finally
            {
                if (webSocket != null)
                {
                    clients.TryRemove(clientId, out _);
                    StatusChanged?.Invoke(this, $"客户端已断开: {clientId}");
                }
            }
        }

        private async Task HandleClientMessagesAsync(WebSocket webSocket, string clientId)
        {
            var buffer = new byte[1024 * 4];
            
            while (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await HandleMessage(webSocket, message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(this, $"客户端消息处理错误: {ex.Message}");
                    break;
                }
            }
        }

        private async Task HandleMessage(WebSocket webSocket, string message)
        {
            try
            {
                var request = JsonConvert.DeserializeObject<SimpleWebSocketMessage>(message);
                if (request == null)
                {
                    await SendErrorResponse(webSocket, "无效的JSON格式", "parse_error");
                    return;
                }

                SimpleWebSocketMessage response;

                switch (request.Command?.ToLowerInvariant())
                {
                    case "start_recording":
                        response = await HandleStartRecording();
                        break;

                    case "stop_recording":
                        response = await HandleStopRecording();
                        break;

                    case "get_status":
                        response = await HandleGetStatus();
                        break;

                    default:
                        response = new SimpleWebSocketMessage
                        {
                            Command = request.Command ?? "unknown",
                            Success = false,
                            Message = $"未知命令: {request.Command}"
                        };
                        break;
                }

                await SendMessage(webSocket, response);
            }
            catch (Exception ex)
            {
                await SendErrorResponse(webSocket, ex.Message, "internal_error");
            }
        }

        private async Task<SimpleWebSocketMessage> HandleStartRecording()
        {
            try
            {
                if (audioRecorder.IsRecording)
                {
                    return new SimpleWebSocketMessage
                    {
                        Command = "start_recording",
                        Success = false,
                        Message = "录制已在进行中"
                    };
                }

                audioRecorder.StartRecording();
                
                // 通知状态变化
                StatusChanged?.Invoke(this, "WebSocket命令：开始录制");

                // 广播状态更新给所有客户端
                await BroadcastStatusUpdate("recording_started", new { IsRecording = audioRecorder.IsRecording });

                return new SimpleWebSocketMessage
                {
                    Command = "start_recording",
                    Success = true,
                    Message = "录制已开始",
                    Data = new { IsRecording = audioRecorder.IsRecording }
                };
            }
            catch (Exception ex)
            {
                return new SimpleWebSocketMessage
                {
                    Command = "start_recording",
                    Success = false,
                    Message = $"开始录制失败: {ex.Message}"
                };
            }
        }

        private async Task<SimpleWebSocketMessage> HandleStopRecording()
        {
            try
            {
                if (!audioRecorder.IsRecording)
                {
                    return new SimpleWebSocketMessage
                    {
                        Command = "stop_recording",
                        Success = false,
                        Message = "当前没有录制在进行"
                    };
                }

                audioRecorder.StopRecording();
                
                // 通知状态变化
                StatusChanged?.Invoke(this, "WebSocket命令：停止录制");

                // 广播状态更新给所有客户端
                await BroadcastStatusUpdate("recording_stopped", new { IsRecording = audioRecorder.IsRecording });

                return new SimpleWebSocketMessage
                {
                    Command = "stop_recording",
                    Success = true,
                    Message = "录制已停止",
                    Data = new { IsRecording = audioRecorder.IsRecording }
                };
            }
            catch (Exception ex)
            {
                return new SimpleWebSocketMessage
                {
                    Command = "stop_recording",
                    Success = false,
                    Message = $"停止录制失败: {ex.Message}"
                };
            }
        }

        private async Task<SimpleWebSocketMessage> HandleGetStatus()
        {
            return new SimpleWebSocketMessage
            {
                Command = "get_status",
                Success = true,
                Message = "状态获取成功",
                Data = new 
                { 
                    IsRecording = audioRecorder.IsRecording,
                    Status = audioRecorder.IsRecording ? "录制中" : "就绪"
                }
            };
        }

        private async Task SendErrorResponse(WebSocket webSocket, string errorMessage, string command)
        {
            var errorResponse = new SimpleWebSocketMessage
            {
                Command = command,
                Success = false,
                Message = errorMessage
            };

            await SendMessage(webSocket, errorResponse);
        }

        private async Task SendMessage(WebSocket webSocket, SimpleWebSocketMessage message)
        {
            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    var json = JsonConvert.SerializeObject(message);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"发送消息失败: {ex.Message}");
            }
        }

        private async Task ServeTestPageAsync(HttpListenerContext context)
        {
            var response = context.Response;
            response.ContentType = "text/html; charset=utf-8";
            var html = "<html><body><h1>WebSocket Test Page</h1><p>Connect to ws://localhost:" + port + "/ws</p></body></html>";
            var buffer = Encoding.UTF8.GetBytes(html);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        private void AudioRecorder_StatusChanged(object? sender, string message)
        {
            StatusChanged?.Invoke(this, message);
        }

        private void AudioRecorder_ErrorOccurred(object? sender, Exception ex)
        {
            StatusChanged?.Invoke(this, $"录音错误: {ex.Message}");
        }

        private async Task BroadcastStatusUpdate(string command, object? data = null)
        {
            var message = new SimpleWebSocketMessage
            {
                Command = command,
                Success = true,
                Message = "状态更新",
                Data = data
            };

            await BroadcastMessageAsync(message);
        }

        private async Task BroadcastMessageAsync(SimpleWebSocketMessage message)
        {
            var json = JsonConvert.SerializeObject(message);
            var bytes = Encoding.UTF8.GetBytes(json);

            var tasks = new List<Task>();
            foreach (var client in clients.Values)
            {
                if (client.State == WebSocketState.Open)
                {
                    tasks.Add(client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None));
                }
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }
        }

        public void Stop()
        {
            isRunning = false;
            cancellationTokenSource?.Cancel();
            httpListener?.Stop();
            
            foreach (var client in clients.Values)
            {
                if (client.State == WebSocketState.Open)
                {
                    _ = client.CloseAsync(WebSocketCloseStatus.NormalClosure, "服务器关闭", CancellationToken.None);
                }
            }
            
            clients.Clear();
            StatusChanged?.Invoke(this, "WebSocket服务器已停止");
        }

        public void Dispose()
        {
            Stop();
            cancellationTokenSource?.Dispose();
            httpListener?.Close();
        }
    }
}
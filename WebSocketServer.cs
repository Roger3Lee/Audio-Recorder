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
    public class WebSocketMessage
    {
        public string Command { get; set; } = "";
        public object? Data { get; set; }
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class RecordingRequest
    {
        public bool RecordSystemAudio { get; set; } = true;
        public bool RecordMicrophone { get; set; } = true;
        public string Mode { get; set; } = "Mixed"; // "Mixed" or "Separate"
        public float MicrophoneGain { get; set; } = 2.0f;
    }

    public class WebSocketServer : IDisposable
    {
        private HttpListener? httpListener;
        private CancellationTokenSource? cancellationTokenSource;
        private readonly ConcurrentDictionary<string, WebSocket> clients;
        private readonly AudioRecorderEngine audioEngine;
        private bool isRunning = false;
        private readonly int port;
        private readonly DateTime serverStartTime;

        public event EventHandler<string>? StatusChanged;

        public WebSocketServer(AudioRecorderEngine audioEngine, int port = 8080)
        {
            this.audioEngine = audioEngine;
            this.port = port;
            this.serverStartTime = DateTime.Now;
            this.clients = new ConcurrentDictionary<string, WebSocket>();
            
            // 监听音频引擎事件
            audioEngine.StatusChanged += AudioEngine_StatusChanged;
            audioEngine.ErrorOccurred += AudioEngine_ErrorOccurred;
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
                StatusChanged?.Invoke(this, $"WebSocket地址: ws://localhost:{port}/ws");

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
                        // 返回简单的HTML页面用于测试
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
                StatusChanged?.Invoke(this, $"WebSocket客户端已连接: {clientId}");

                // 发送欢迎消息
                await SendMessageAsync(webSocket, new WebSocketMessage
                {
                    Command = "connected",
                    Message = "已连接到音频录制服务器",
                    Data = new
                    {
                        IsRecording = audioEngine.IsRecording,
                        MicrophoneGain = audioEngine.MicrophoneGain
                    }
                });

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
                    if (webSocket.State == WebSocketState.Open)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "连接关闭", CancellationToken.None);
                    }
                    webSocket.Dispose();
                    StatusChanged?.Invoke(this, $"WebSocket客户端已断开: {clientId}");
                }
            }
        }

        private async Task HandleClientMessagesAsync(WebSocket webSocket, string clientId)
        {
            var buffer = new byte[4096];

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
                        break;
                    }
                }
                catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    break;
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(this, $"处理客户端消息错误: {ex.Message}");
                    break;
                }
            }
        }

        private async Task HandleMessage(WebSocket webSocket, string message)
        {
            try
            {
                var request = JsonConvert.DeserializeObject<WebSocketMessage>(message);
                if (request == null)
                {
                    await SendErrorResponse(webSocket, "无效的JSON格式", "parse_error");
                    return;
                }

                WebSocketMessage response;

                switch (request.Command?.ToLowerInvariant())
                {
                    case "start_recording":
                        response = await HandleStartRecording(request.Data);
                        break;

                    case "stop_recording":
                        response = await HandleStopRecording();
                        break;

                    case "get_status":
                        response = await HandleGetStatus();
                        break;

                    case "set_microphone_gain":
                        response = await HandleSetMicrophoneGain(request.Data);
                        break;

                    case "get_microphones":
                        response = await HandleGetMicrophones();
                        break;
                        
                    case "set_audio_quality":
                        response = await HandleSetAudioQuality(request.Data);
                        break;
                        
                    case "set_noise_reduction":
                        response = await HandleSetNoiseReduction(request.Data);
                        break;
                        
                    case "set_dynamic_compression":
                        response = await HandleSetDynamicCompression(request.Data);
                        break;
                        
                    case "get_audio_settings":
                        response = await HandleGetAudioSettings();
                        break;

                    default:
                        response = new WebSocketMessage
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

        private async Task<WebSocketMessage> HandleStartRecording(object? data)
        {
            try
            {
                if (audioEngine.IsRecording)
                {
                    return new WebSocketMessage
                    {
                        Command = "start_recording",
                        Success = false,
                        Message = "录制已在进行中"
                    };
                }

                bool recordSystemAudio = true;
                bool recordMicrophone = true;
                RecordingMode mode = RecordingMode.Mixed;
                float microphoneGain = audioEngine.MicrophoneGain;

                if (data != null)
                {
                    var json = JsonConvert.SerializeObject(data);
                    var settings = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                    if (settings != null)
                    {
                        if (settings.ContainsKey("RecordSystemAudio"))
                            recordSystemAudio = Convert.ToBoolean(settings["RecordSystemAudio"]);

                        if (settings.ContainsKey("RecordMicrophone"))
                            recordMicrophone = Convert.ToBoolean(settings["RecordMicrophone"]);

                        if (settings.ContainsKey("Mode"))
                        {
                            if (Enum.TryParse<RecordingMode>(settings["Mode"].ToString(), out var parsedMode))
                                mode = parsedMode;
                        }

                        if (settings.ContainsKey("MicrophoneGain"))
                        {
                            if (float.TryParse(settings["MicrophoneGain"].ToString(), out var gain))
                                audioEngine.MicrophoneGain = gain;
                        }
                    }
                }

                audioEngine.StartRecording(recordSystemAudio, recordMicrophone, mode);

                var responseData = new
                {
                    IsRecording = audioEngine.IsRecording,
                    RecordSystemAudio = recordSystemAudio,
                    RecordMicrophone = recordMicrophone,
                    Mode = mode.ToString(),
                    MicrophoneGain = audioEngine.MicrophoneGain,
                    AudioQuality = audioEngine.Quality.ToString(),
                    NoiseReduction = audioEngine.EnableNoiseReduction,
                    DynamicCompression = audioEngine.EnableDynamicRangeCompression
                };

                await BroadcastStatusUpdate("开始录制", responseData);

                return new WebSocketMessage
                {
                    Command = "start_recording",
                    Success = true,
                    Message = $"高质量{mode}模式录制已开始",
                    Data = responseData
                };
            }
            catch (Exception ex)
            {
                return new WebSocketMessage
                {
                    Command = "start_recording",
                    Success = false,
                    Message = $"启动录制失败: {ex.Message}"
                };
            }
        }

        private async Task<WebSocketMessage> HandleStopRecording()
        {
            try
            {
                if (!audioEngine.IsRecording)
                {
                    return new WebSocketMessage
                    {
                        Command = "stop_recording",
                        Success = false,
                        Message = "当前没有进行录制"
                    };
                }

                audioEngine.StopRecording();

                var responseData = new
                {
                    IsRecording = audioEngine.IsRecording
                };

                await BroadcastStatusUpdate("停止录制", responseData);

                return new WebSocketMessage
                {
                    Command = "stop_recording",
                    Success = true,
                    Message = "录制已停止",
                    Data = responseData
                };
            }
            catch (Exception ex)
            {
                return new WebSocketMessage
                {
                    Command = "stop_recording",
                    Success = false,
                    Message = $"停止录制失败: {ex.Message}"
                };
            }
        }

        private async Task<WebSocketMessage> HandleGetStatus()
        {
            try
            {
                var statusData = new
                {
                    IsRecording = audioEngine.IsRecording,
                    MicrophoneGain = audioEngine.MicrophoneGain,
                    SystemAudioGain = audioEngine.SystemAudioGain,
                    AudioQuality = audioEngine.Quality.ToString(),
                    QualityDescription = GetQualityDescription(audioEngine.Quality),
                    NoiseReduction = audioEngine.EnableNoiseReduction,
                    DynamicCompression = audioEngine.EnableDynamicRangeCompression,
                    ConnectedClients = clients.Count,
                    ServerStartTime = serverStartTime.ToString("yyyy-MM-dd HH:mm:ss")
                };

                return new WebSocketMessage
                {
                    Command = "get_status",
                    Success = true,
                    Message = "状态获取成功",
                    Data = statusData
                };
            }
            catch (Exception ex)
            {
                return new WebSocketMessage
                {
                    Command = "get_status",
                    Success = false,
                    Message = $"获取状态失败: {ex.Message}"
                };
            }
        }

        private async Task<WebSocketMessage> HandleSetMicrophoneGain(object? data)
        {
            try
            {
                if (data == null)
                {
                    return new WebSocketMessage
                    {
                        Command = "set_microphone_gain",
                        Success = false,
                        Message = "未提供增益值"
                    };
                }

                if (float.TryParse(data.ToString(), out float gain))
                {
                    float oldGain = audioEngine.MicrophoneGain;
                    audioEngine.MicrophoneGain = gain;

                    var responseData = new
                    {
                        MicrophoneGain = audioEngine.MicrophoneGain,
                        PreviousGain = oldGain
                    };

                    await BroadcastStatusUpdate($"麦克风增益已调整", responseData);

                    return new WebSocketMessage
                    {
                        Command = "set_microphone_gain",
                        Success = true,
                        Message = $"麦克风增益已设置为 {audioEngine.MicrophoneGain:F1}x",
                        Data = responseData
                    };
                }
                else
                {
                    return new WebSocketMessage
                    {
                        Command = "set_microphone_gain",
                        Success = false,
                        Message = "无效的增益值"
                    };
                }
            }
            catch (Exception ex)
            {
                return new WebSocketMessage
                {
                    Command = "set_microphone_gain",
                    Success = false,
                    Message = $"设置麦克风增益失败: {ex.Message}"
                };
            }
        }

        private async Task<WebSocketMessage> HandleGetMicrophones()
        {
            try
            {
                var microphones = audioEngine.GetAvailableMicrophones();

                return new WebSocketMessage
                {
                    Command = "get_microphones",
                    Success = true,
                    Message = $"找到 {microphones.Length} 个麦克风设备",
                    Data = microphones
                };
            }
            catch (Exception ex)
            {
                return new WebSocketMessage
                {
                    Command = "get_microphones",
                    Success = false,
                    Message = $"获取麦克风列表失败: {ex.Message}"
                };
            }
        }
        
        private async Task<WebSocketMessage> HandleSetAudioQuality(object? data)
        {
            try
            {
                if (audioEngine.IsRecording)
                {
                    return new WebSocketMessage
                    {
                        Command = "set_audio_quality",
                        Success = false,
                        Message = "录制进行中，无法更改音质设置"
                    };
                }

                if (data == null)
                {
                    return new WebSocketMessage
                    {
                        Command = "set_audio_quality",
                        Success = false,
                        Message = "未提供音质设置"
                    };
                }

                AudioQuality oldQuality = audioEngine.Quality;
                
                if (int.TryParse(data.ToString(), out int qualityIndex) && 
                    qualityIndex >= 0 && qualityIndex <= 2)
                {
                    audioEngine.Quality = (AudioQuality)qualityIndex;
                }
                else if (Enum.TryParse<AudioQuality>(data.ToString(), true, out var parsedQuality))
                {
                    audioEngine.Quality = parsedQuality;
                }
                else
                {
                    return new WebSocketMessage
                    {
                        Command = "set_audio_quality",
                        Success = false,
                        Message = "无效的音质设置"
                    };
                }

                var responseData = new
                {
                    AudioQuality = audioEngine.Quality.ToString(),
                    QualityDescription = GetQualityDescription(audioEngine.Quality),
                    PreviousQuality = oldQuality.ToString()
                };

                return new WebSocketMessage
                {
                    Command = "set_audio_quality",
                    Success = true,
                    Message = $"音质已设置为: {GetQualityDescription(audioEngine.Quality)}",
                    Data = responseData
                };
            }
            catch (Exception ex)
            {
                return new WebSocketMessage
                {
                    Command = "set_audio_quality",
                    Success = false,
                    Message = $"设置音质失败: {ex.Message}"
                };
            }
        }
        
        private async Task<WebSocketMessage> HandleSetNoiseReduction(object? data)
        {
            try
            {
                if (data == null)
                {
                    return new WebSocketMessage
                    {
                        Command = "set_noise_reduction",
                        Success = false,
                        Message = "未提供降噪设置"
                    };
                }

                bool enable = Convert.ToBoolean(data);
                bool oldSetting = audioEngine.EnableNoiseReduction;
                audioEngine.EnableNoiseReduction = enable;

                var responseData = new
                {
                    NoiseReduction = audioEngine.EnableNoiseReduction,
                    PreviousSetting = oldSetting
                };

                return new WebSocketMessage
                {
                    Command = "set_noise_reduction",
                    Success = true,
                    Message = $"降噪处理已{(enable ? "启用" : "禁用")}",
                    Data = responseData
                };
            }
            catch (Exception ex)
            {
                return new WebSocketMessage
                {
                    Command = "set_noise_reduction",
                    Success = false,
                    Message = $"设置降噪失败: {ex.Message}"
                };
            }
        }
        
        private async Task<WebSocketMessage> HandleSetDynamicCompression(object? data)
        {
            try
            {
                if (data == null)
                {
                    return new WebSocketMessage
                    {
                        Command = "set_dynamic_compression",
                        Success = false,
                        Message = "未提供动态压缩设置"
                    };
                }

                bool enable = Convert.ToBoolean(data);
                bool oldSetting = audioEngine.EnableDynamicRangeCompression;
                audioEngine.EnableDynamicRangeCompression = enable;

                var responseData = new
                {
                    DynamicCompression = audioEngine.EnableDynamicRangeCompression,
                    PreviousSetting = oldSetting
                };

                return new WebSocketMessage
                {
                    Command = "set_dynamic_compression",
                    Success = true,
                    Message = $"动态范围压缩已{(enable ? "启用" : "禁用")}",
                    Data = responseData
                };
            }
            catch (Exception ex)
            {
                return new WebSocketMessage
                {
                    Command = "set_dynamic_compression",
                    Success = false,
                    Message = $"设置动态压缩失败: {ex.Message}"
                };
            }
        }
        
        private async Task<WebSocketMessage> HandleGetAudioSettings()
        {
            try
            {
                var settings = new
                {
                    AudioQuality = audioEngine.Quality.ToString(),
                    QualityDescription = GetQualityDescription(audioEngine.Quality),
                    QualityIndex = (int)audioEngine.Quality,
                    NoiseReduction = audioEngine.EnableNoiseReduction,
                    DynamicCompression = audioEngine.EnableDynamicRangeCompression,
                    MicrophoneGain = audioEngine.MicrophoneGain,
                    SystemAudioGain = audioEngine.SystemAudioGain,
                    IsRecording = audioEngine.IsRecording
                };

                return new WebSocketMessage
                {
                    Command = "get_audio_settings",
                    Success = true,
                    Message = "音频设置获取成功",
                    Data = settings
                };
            }
            catch (Exception ex)
            {
                return new WebSocketMessage
                {
                    Command = "get_audio_settings",
                    Success = false,
                    Message = $"获取音频设置失败: {ex.Message}"
                };
            }
        }
        
        private string GetQualityDescription(AudioQuality quality)
        {
            return quality switch
            {
                AudioQuality.Standard => "标准质量 (44.1kHz/16bit)",
                AudioQuality.High => "高质量 (48kHz/24bit)",
                AudioQuality.Studio => "录音棚质量 (96kHz/32bit)",
                _ => "未知质量"
            };
        }
        
        private async Task SendErrorResponse(WebSocket webSocket, string errorMessage, string command)
        {
            var errorResponse = new WebSocketMessage
            {
                Command = command,
                Success = false,
                Message = errorMessage
            };
            await SendMessage(webSocket, errorResponse);
        }
        
        private async Task BroadcastStatusUpdate(string message, object? data = null)
        {
            var statusMessage = new WebSocketMessage
            {
                Command = "status_update",
                Success = true,
                Message = message,
                Data = data
            };
            await BroadcastMessage(statusMessage);
        }
        
        private async Task SendMessage(WebSocket webSocket, WebSocketMessage message)
        {
            await SendMessageAsync(webSocket, message);
        }
        
        private async Task BroadcastMessage(WebSocketMessage message)
        {
            await BroadcastMessageAsync(message);
        }

        private async Task SendMessageAsync(WebSocket webSocket, WebSocketMessage message)
        {
            if (webSocket.State == WebSocketState.Open)
            {
                var json = JsonConvert.SerializeObject(message);
                var bytes = Encoding.UTF8.GetBytes(json);
                await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        private async Task BroadcastMessageAsync(WebSocketMessage message)
        {
            var disconnectedClients = new List<string>();

            foreach (var client in clients)
            {
                try
                {
                    if (client.Value.State == WebSocketState.Open)
                    {
                        await SendMessageAsync(client.Value, message);
                    }
                    else
                    {
                        disconnectedClients.Add(client.Key);
                    }
                }
                catch
                {
                    disconnectedClients.Add(client.Key);
                }
            }

            // 清理断开的连接
            foreach (var clientId in disconnectedClients)
            {
                clients.TryRemove(clientId, out _);
            }
        }

        private async Task ServeTestPageAsync(HttpListenerContext context)
        {
            var response = context.Response;
            var testPage = GetTestPageHtml();
            var buffer = Encoding.UTF8.GetBytes(testPage);

            response.ContentLength64 = buffer.Length;
            response.ContentType = "text/html; charset=utf-8";
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        private string GetTestPageHtml()
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <title>音频录制 WebSocket 测试</title>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        button {{ margin: 5px; padding: 10px; }}
        #messages {{ border: 1px solid #ccc; height: 300px; overflow-y: scroll; padding: 10px; margin: 10px 0; }}
        .success {{ color: green; }}
        .error {{ color: red; }}
    </style>
</head>
<body>
    <h1>音频录制 WebSocket 控制面板</h1>
    <div>
        <button onclick='connect()'>连接</button>
        <button onclick='disconnect()'>断开</button>
        <button onclick='getStatus()'>获取状态</button>
    </div>
    <div>
        <button onclick='startRecording()'>开始录制</button>
        <button onclick='stopRecording()'>停止录制</button>
    </div>
    <div>
        麦克风增益: <input type='number' id='gainInput' value='2.0' step='0.1' min='0.1' max='8.0'>
        <button onclick='setGain()'>设置增益</button>
    </div>
    <div id='messages'></div>

    <script>
        let ws = null;
        
        function connect() {{
            ws = new WebSocket('ws://localhost:{port}/ws');
            ws.onopen = () => addMessage('连接成功', 'success');
            ws.onmessage = (event) => {{
                const data = JSON.parse(event.data);
                addMessage('收到: ' + JSON.stringify(data, null, 2), data.Success ? 'success' : 'error');
            }};
            ws.onerror = (error) => addMessage('连接错误: ' + error, 'error');
            ws.onclose = () => addMessage('连接已关闭', 'error');
        }}
        
        function disconnect() {{
            if (ws) ws.close();
        }}
        
        function sendCommand(command, data = null) {{
            if (!ws || ws.readyState !== WebSocket.OPEN) {{
                addMessage('请先连接WebSocket', 'error');
                return;
            }}
            const message = {{ Command: command, Data: data }};
            ws.send(JSON.stringify(message));
        }}
        
        function startRecording() {{
            sendCommand('start_recording', {{
                RecordSystemAudio: true,
                RecordMicrophone: true,
                Mode: 'Mixed',
                MicrophoneGain: parseFloat(document.getElementById('gainInput').value)
            }});
        }}
        
        function stopRecording() {{
            sendCommand('stop_recording');
        }}
        
        function getStatus() {{
            sendCommand('get_status');
        }}
        
        function setGain() {{
            const gain = document.getElementById('gainInput').value;
            sendCommand('set_microphone_gain', gain);
        }}
        
        function addMessage(message, type = '') {{
            const div = document.createElement('div');
            div.className = type;
            div.textContent = new Date().toLocaleTimeString() + ' - ' + message;
            const messagesDiv = document.getElementById('messages');
            messagesDiv.appendChild(div);
            messagesDiv.scrollTop = messagesDiv.scrollHeight;
        }}
    </script>
</body>
</html>";
        }

        private void AudioEngine_StatusChanged(object? sender, string message)
        {
            _ = Task.Run(() => BroadcastMessageAsync(new WebSocketMessage
            {
                Command = "status_update",
                Message = message,
                Data = new { IsRecording = audioEngine.IsRecording }
            }));
        }

        private void AudioEngine_ErrorOccurred(object? sender, Exception ex)
        {
            _ = Task.Run(() => BroadcastMessageAsync(new WebSocketMessage
            {
                Command = "error_occurred",
                Success = false,
                Message = ex.Message
            }));
        }

        public void Stop()
        {
            if (!isRunning) return;

            isRunning = false;
            cancellationTokenSource?.Cancel();
            httpListener?.Stop();
            
            foreach (var client in clients.Values)
            {
                if (client.State == WebSocketState.Open)
                {
                    _ = client.CloseAsync(WebSocketCloseStatus.NormalClosure, "服务器关闭", CancellationToken.None);
                }
                client.Dispose();
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
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace AudioRecorder.Services
{
    /// <summary>
    /// 本地HTTP服务器 - 用于接收OAuth回调
    /// </summary>
    public class LocalHttpServer : IDisposable
    {
        private HttpListener? _listener;
        private readonly int _port;
        private readonly string _callbackPath;
        private bool _isRunning;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public event EventHandler<string>? AuthorizationCodeReceived;
        public event EventHandler<string>? ErrorOccurred;

        public LocalHttpServer(int port = 8081, string callbackPath = "/auth/callback")
        {
            _port = port;
            _callbackPath = callbackPath;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// 启动HTTP服务器
        /// </summary>
        public async Task<bool> StartAsync()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();
                _isRunning = true;

                Console.WriteLine($"🌐 本地HTTP服务器已启动: http://localhost:{_port}");
                Console.WriteLine($"📡 回调路径: {_callbackPath}");

                // 开始监听请求
                _ = Task.Run(ListenForRequestsAsync);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 启动HTTP服务器失败: {ex.Message}");
                ErrorOccurred?.Invoke(this, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 停止HTTP服务器
        /// </summary>
        public void Stop()
        {
            try
            {
                _isRunning = false;
                _cancellationTokenSource.Cancel();
                _listener?.Stop();
                _listener?.Close();
                Console.WriteLine("🛑 本地HTTP服务器已停止");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 停止HTTP服务器时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 监听HTTP请求
        /// </summary>
        private async Task ListenForRequestsAsync()
        {
            while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener!.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context));
                }
                catch (Exception ex) when (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Console.WriteLine($"⚠️ 处理HTTP请求时出错: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 处理HTTP请求
        /// </summary>
        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                Console.WriteLine($"📥 收到HTTP请求: {request.HttpMethod} {request.Url?.AbsolutePath}");

                if (request.Url?.AbsolutePath == _callbackPath)
                {
                    await HandleOAuthCallbackAsync(request, response);
                }
                else
                {
                    await HandleDefaultResponseAsync(response);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 处理HTTP请求失败: {ex.Message}");
                await SendErrorResponseAsync(context.Response, ex.Message);
            }
        }

        /// <summary>
        /// 处理OAuth回调
        /// </summary>
        private async Task HandleOAuthCallbackAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                var queryString = request.Url?.Query;
                if (string.IsNullOrEmpty(queryString))
                {
                    await SendErrorResponseAsync(response, "缺少查询参数");
                    return;
                }

                var queryParams = HttpUtility.ParseQueryString(queryString);
                var code = queryParams["code"];
                var error = queryParams["error"];
                var state = queryParams["state"];

                if (!string.IsNullOrEmpty(error))
                {
                    var errorDescription = queryParams["error_description"];
                    var errorMessage = $"授权失败: {error}";
                    if (!string.IsNullOrEmpty(errorDescription))
                    {
                        errorMessage += $" - {errorDescription}";
                    }

                    Console.WriteLine($"❌ OAuth回调错误: {errorMessage}");
                    await SendErrorResponseAsync(response, errorMessage);
                    ErrorOccurred?.Invoke(this, errorMessage);
                    return;
                }

                if (string.IsNullOrEmpty(code))
                {
                    await SendErrorResponseAsync(response, "缺少授权码");
                    return;
                }

                Console.WriteLine($"✅ 收到授权码: {code}");
                AuthorizationCodeReceived?.Invoke(this, code);

                // 发送成功页面
                await SendSuccessResponseAsync(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 处理OAuth回调失败: {ex.Message}");
                await SendErrorResponseAsync(response, ex.Message);
                ErrorOccurred?.Invoke(this, ex.Message);
            }
        }

        /// <summary>
        /// 发送成功响应
        /// </summary>
        private async Task SendSuccessResponseAsync(HttpListenerResponse response)
        {
            var html = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>授权成功</title>
    <style>
        body { 
            font-family: 'Microsoft YaHei', sans-serif; 
            text-align: center; 
            padding: 50px; 
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            margin: 0;
        }
        .container { 
            background: rgba(255,255,255,0.1); 
            padding: 40px; 
            border-radius: 20px; 
            backdrop-filter: blur(10px);
            box-shadow: 0 8px 32px rgba(0,0,0,0.1);
        }
        .success-icon { 
            font-size: 60px; 
            margin-bottom: 20px; 
        }
        h1 { 
            margin: 0 0 20px 0; 
            font-size: 28px; 
        }
        p { 
            margin: 0; 
            font-size: 16px; 
            opacity: 0.9; 
        }
        .close-btn { 
            margin-top: 30px; 
            padding: 12px 30px; 
            background: rgba(255,255,255,0.2); 
            border: 2px solid rgba(255,255,255,0.3); 
            color: white; 
            border-radius: 25px; 
            cursor: pointer; 
            font-size: 16px; 
            transition: all 0.3s; 
        }
        .close-btn:hover { 
            background: rgba(255,255,255,0.3); 
            transform: translateY(-2px); 
        }
    </style>
</head>
<body>
    <div class='container'>
        <div class='success-icon'>✅</div>
        <h1>授权成功！</h1>
        <p>您的Google账户已成功授权，可以关闭此页面了。</p>
        <button class='close-btn' onclick='window.close()'>关闭页面</button>
    </div>
    <script>
        // 3秒后自动关闭页面
        setTimeout(() => {
            window.close();
        }, 3000);
    </script>
</body>
</html>";

            var buffer = Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.StatusCode = 200;

            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        /// <summary>
        /// 发送错误响应
        /// </summary>
        private async Task SendErrorResponseAsync(HttpListenerResponse response, string errorMessage)
        {
            var html = @$"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>授权失败</title>
    <style>
        body {{ 
            font-family: 'Microsoft YaHei', sans-serif; 
            text-align: center; 
            padding: 50px; 
            background: linear-gradient(135deg, #ff6b6b 0%, #ee5a24 100%);
            color: white;
            margin: 0;
        }}
        .container {{ 
            background: rgba(255,255,255,0.1); 
            padding: 40px; 
            border-radius: 20px; 
            backdrop-filter: blur(10px);
            box-shadow: 0 8px 32px rgba(0,0,0,0.1);
        }}
        .error-icon {{ 
            font-size: 60px; 
            margin-bottom: 20px; 
        }}
        h1 {{ 
            margin: 0 0 20px 0; 
            font-size: 28px; 
        }}
        p {{ 
            margin: 0; 
            font-size: 16px; 
            opacity: 0.9; 
        }}
        .close-btn {{ 
            margin-top: 30px; 
            padding: 12px 30px; 
            background: rgba(255,255,255,0.2); 
            border: 2px solid rgba(255,255,255,0.3); 
            color: white; 
            border-radius: 25px; 
            cursor: pointer; 
            font-size: 16px; 
            transition: all 0.3s; 
        }}
        .close-btn:hover {{ 
            background: rgba(255,255,255,0.3); 
            transform: translateY(-2px); 
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='error-icon'>❌</div>
        <h1>授权失败</h1>
        <p>{errorMessage}</p>
        <button class='close-btn' onclick='window.close()'>关闭页面</button>
    </div>
    <script>
        // 5秒后自动关闭页面
        setTimeout(() => {{
            window.close();
        }}, 5000);
    </script>
</body>
</html>";

            var buffer = Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.StatusCode = 400;

            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        /// <summary>
        /// 发送默认响应
        /// </summary>
        private async Task HandleDefaultResponseAsync(HttpListenerResponse response)
        {
            var html = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>AudioRecorder OAuth</title>
    <style>
        body { 
            font-family: 'Microsoft YaHei', sans-serif; 
            text-align: center; 
            padding: 50px; 
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            margin: 0;
        }
        .container { 
            background: rgba(255,255,255,0.1); 
            padding: 40px; 
            border-radius: 20px; 
            backdrop-filter: blur(10px);
            box-shadow: 0 8px 32px rgba(0,0,0,0.1);
        }
        h1 { 
            margin: 0 0 20px 0; 
            font-size: 28px; 
        }
        p { 
            margin: 0; 
            font-size: 16px; 
            opacity: 0.9; 
        }
    </style>
</head>
<body>
    <div class='container'>
        <h1>AudioRecorder OAuth</h1>
        <p>这是OAuth授权回调服务器，请勿直接访问。</p>
    </div>
</body>
</html>";

            var buffer = Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.StatusCode = 200;

            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        /// <summary>
        /// 获取服务器URL
        /// </summary>
        public string GetServerUrl()
        {
            return $"http://localhost:{_port}";
        }

        /// <summary>
        /// 获取回调URL
        /// </summary>
        public string GetCallbackUrl()
        {
            return $"http://localhost:{_port}{_callbackPath}";
        }

        /// <summary>
        /// 检查服务器是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning;

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource?.Dispose();
        }
    }
}

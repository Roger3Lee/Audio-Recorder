using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using AudioRecorder.Models;
using Microsoft.Extensions.Logging;

namespace AudioRecorder.Services
{
    /// <summary>
    /// 音频文件上传服务
    /// </summary>
    public class AudioFileUploadService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly UploadSettings _uploadSettings;
        private readonly ILogger _logger;

        public event EventHandler<string>? UploadProgressChanged;
        public event EventHandler<Exception>? UploadErrorOccurred;
        public event EventHandler<string>? UploadCompleted;

        public AudioFileUploadService(UploadSettings uploadSettings)
        {
            _uploadSettings = uploadSettings ?? throw new ArgumentNullException(nameof(uploadSettings));
            _logger = LoggingServiceManager.CreateLogger("AudioFileUploadService");

            _logger.LogInformation("AudioFileUploadService 初始化开始");
            _logger.LogInformation("上传设置: 重试次数={RetryCount}, 重试延迟={RetryDelay}ms, 超时={Timeout}ms", 
                _uploadSettings.RetryCount, _uploadSettings.RetryDelay, _uploadSettings.UploadTimeout);
            
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(_uploadSettings.UploadTimeout)
            };
            
            // 设置默认请求头
            _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AudioRecorder/1.0.0");
            
            _logger.LogInformation("HTTP客户端配置完成，请求头已设置");
            _logger.LogInformation("AudioFileUploadService 初始化完成");
        }

        /// <summary>
        /// 上传音频文件
        /// </summary>
        /// <param name="systemAudioPath">系统音频文件路径</param>
        /// <param name="microphonePath">麦克风音频文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>上传结果</returns>
        public async Task<bool> UploadAudioFilesAsync(string systemAudioPath, string microphonePath, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("开始上传音频文件流程");
            
            // 验证文件路径
            if (string.IsNullOrEmpty(systemAudioPath) || !File.Exists(systemAudioPath))
            {
                var error = new FileNotFoundException("系统音频文件不存在", systemAudioPath);
                _logger.LogError(error, "系统音频文件验证失败: {Path}", systemAudioPath);
                throw error;
            }

            if (string.IsNullOrEmpty(microphonePath) || !File.Exists(microphonePath))
            {
                var error = new FileNotFoundException("麦克风音频文件不存在", microphonePath);
                _logger.LogError(error, "麦克风音频文件验证失败: {Path}", microphonePath);
                throw error;
            }

            // 记录文件信息
            var systemAudioInfo = new FileInfo(systemAudioPath);
            var microphoneInfo = new FileInfo(microphonePath);
            
            _logger.LogInformation("文件验证通过，准备上传: 系统音频={SystemAudio}({Size}字节), 麦克风={Microphone}({Size}字节)", 
                Path.GetFileName(systemAudioPath), systemAudioInfo.Length,
                Path.GetFileName(microphonePath), microphoneInfo.Length);

            try
            {
                OnUploadProgressChanged("🚀 开始上传音频文件...");

                var uploadResult = await UploadWithRetryAsync(systemAudioPath, microphonePath, cancellationToken);

                if (uploadResult)
                {
                    var successMessage = $"✅ 音频文件上传成功！\n系统音频: {Path.GetFileName(systemAudioPath)}\n麦克风: {Path.GetFileName(microphonePath)}";
                    OnUploadCompleted(successMessage);
                    _logger.LogInformation("音频文件上传成功: 系统音频={SystemAudio}, 麦克风={Microphone}", 
                        Path.GetFileName(systemAudioPath), Path.GetFileName(microphonePath));
                }
                else
                {
                    var errorMessage = "上传失败，所有重试都已完成";
                    _logger.LogError("音频文件上传失败: {SystemAudio}, {Microphone}", 
                        Path.GetFileName(systemAudioPath), Path.GetFileName(microphonePath));
                    OnUploadErrorOccurred(new Exception(errorMessage));
                }

                return uploadResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "上传音频文件时发生未预期错误: 系统音频={SystemAudio}, 麦克风={Microphone}", 
                    Path.GetFileName(systemAudioPath), Path.GetFileName(microphonePath));
                OnUploadErrorOccurred(ex);
                return false;
            }
        }



        /// <summary>
        /// 带重试的上传方法
        /// </summary>
        private async Task<bool> UploadWithRetryAsync(string systemAudioPath, string microphonePath, CancellationToken cancellationToken)
        {
            _logger.LogInformation("开始重试上传流程，最大重试次数: {MaxRetries}", _uploadSettings.RetryCount);
            
            for (int attempt = 1; attempt <= _uploadSettings.RetryCount; attempt++)
            {
                try
                {
                    _logger.LogInformation("开始上传尝试 {Attempt}/{MaxRetries}", attempt, _uploadSettings.RetryCount);
                    OnUploadProgressChanged($"📤 上传尝试 {attempt}/{_uploadSettings.RetryCount}...");

                    var result = await UploadSingleAttemptAsync(systemAudioPath, microphonePath, cancellationToken);
                    if (result)
                    {
                        _logger.LogInformation("上传尝试 {Attempt} 成功", attempt);
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("上传尝试 {Attempt} 失败，服务器返回失败状态", attempt);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "上传尝试 {Attempt} 发生异常", attempt);
                    
                    if (attempt < _uploadSettings.RetryCount)
                    {
                        var delaySeconds = _uploadSettings.RetryDelay / 1000;
                        _logger.LogInformation("等待 {Delay} 秒后进行第 {NextAttempt} 次重试", delaySeconds, attempt + 1);
                        OnUploadProgressChanged($"⚠️ 上传失败，{delaySeconds}秒后重试...");
                        await Task.Delay(_uploadSettings.RetryDelay, cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning("已达到最大重试次数，不再重试");
                    }
                }
            }

            _logger.LogError("所有 {MaxRetries} 次上传尝试都失败了", _uploadSettings.RetryCount);
            return false;
        }

        /// <summary>
        /// 单次上传尝试
        /// </summary>
        private async Task<bool> UploadSingleAttemptAsync(string systemAudioPath, string microphonePath, CancellationToken cancellationToken)
        {
            _logger.LogInformation("准备单次上传尝试");
            
            try
            {
                using var formData = new MultipartFormDataContent();

                // 添加系统音频文件
                var systemAudioContent = new StreamContent(File.OpenRead(systemAudioPath));
                systemAudioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                formData.Add(systemAudioContent, "files", Path.GetFileName(systemAudioPath));
                _logger.LogDebug("已添加系统音频文件到表单: {FileName}", Path.GetFileName(systemAudioPath));

                // 添加麦克风音频文件
                var microphoneContent = new StreamContent(File.OpenRead(microphonePath));
                microphoneContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                formData.Add(microphoneContent, "files", Path.GetFileName(microphonePath));
                _logger.LogDebug("已添加麦克风音频文件到表单: {FileName}", Path.GetFileName(microphonePath));

                // 添加其他参数
                formData.Add(new StringContent(_uploadSettings.BizType), "bizType");
                formData.Add(new StringContent(_uploadSettings.MergeAudio.ToString().ToLower()), "mergeAudio");
                _logger.LogDebug("已添加业务参数: bizType={BizType}, mergeAudio={MergeAudio}", 
                    _uploadSettings.BizType, _uploadSettings.MergeAudio);

                // 设置授权头
                var apiUrl = _uploadSettings.GetFullApiUrl();
                var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = formData
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _uploadSettings.AuthorizationToken);
                
                _logger.LogInformation("准备发送HTTP请求到: {ApiUrl}", apiUrl);
                _logger.LogDebug("请求头设置完成，授权令牌长度: {TokenLength}", 
                    _uploadSettings.AuthorizationToken?.Length ?? 0);

                OnUploadProgressChanged("📡 正在上传到服务器...");

                var startTime = DateTime.UtcNow;
                var response = await _httpClient.SendAsync(request, cancellationToken);
                var endTime = DateTime.UtcNow;
                var duration = endTime - startTime;
                
                _logger.LogInformation("HTTP请求完成，耗时: {Duration}ms, 状态码: {StatusCode}", 
                    duration.TotalMilliseconds, response.StatusCode);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogInformation("上传成功，服务器响应: {Response}", responseContent);
                    _logger.LogInformation("响应头: {Headers}", string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(";", h.Value)}")));
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("上传失败，HTTP状态码: {StatusCode}, 响应: {Response}", 
                        response.StatusCode, errorContent);
                    _logger.LogWarning("响应头: {Headers}", string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(";", h.Value)}")));
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "单次上传尝试过程中发生异常");
                throw;
            }
        }



        /// <summary>
        /// 触发上传进度变化事件
        /// </summary>
        protected virtual void OnUploadProgressChanged(string message)
        {
            _logger.LogDebug("触发上传进度变化事件: {Message}", message);
            UploadProgressChanged?.Invoke(this, message);
        }

        /// <summary>
        /// 触发上传错误事件
        /// </summary>
        protected virtual void OnUploadErrorOccurred(Exception exception)
        {
            _logger.LogError(exception, "触发上传错误事件: {Message}", exception.Message);
            UploadErrorOccurred?.Invoke(this, exception);
        }

        /// <summary>
        /// 触发上传完成事件
        /// </summary>
        protected virtual void OnUploadCompleted(string message)
        {
            _logger.LogInformation("触发上传完成事件: {Message}", message);
            UploadCompleted?.Invoke(this, message);
        }

        public void Dispose()
        {
            _logger.LogInformation("AudioFileUploadService 开始释放资源");
            
            try
            {
                _httpClient?.Dispose();
                _logger.LogInformation("HTTP客户端已释放");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "释放HTTP客户端资源时发生错误");
            }
            
            _logger.LogInformation("AudioFileUploadService 资源释放完成");
        }
    }
}

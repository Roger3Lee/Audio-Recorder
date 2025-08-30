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

        public AudioFileUploadService(UploadSettings uploadSettings, ILogger logger)
        {
            _uploadSettings = uploadSettings ?? throw new ArgumentNullException(nameof(uploadSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
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
            if (string.IsNullOrEmpty(systemAudioPath) || !File.Exists(systemAudioPath))
            {
                throw new FileNotFoundException("系统音频文件不存在", systemAudioPath);
            }

            if (string.IsNullOrEmpty(microphonePath) || !File.Exists(microphonePath))
            {
                throw new FileNotFoundException("麦克风音频文件不存在", microphonePath);
            }

            try
            {
                _logger.LogInformation("开始上传音频文件: 系统音频={SystemAudio}, 麦克风={Microphone}", 
                    Path.GetFileName(systemAudioPath), Path.GetFileName(microphonePath));

                OnUploadProgressChanged("🚀 开始上传音频文件...");

                var uploadResult = await UploadWithRetryAsync(systemAudioPath, microphonePath, cancellationToken);

                if (uploadResult)
                {
                    OnUploadCompleted($"✅ 音频文件上传成功！\n系统音频: {Path.GetFileName(systemAudioPath)}\n麦克风: {Path.GetFileName(microphonePath)}");
                    _logger.LogInformation("音频文件上传成功");
                }
                else
                {
                    OnUploadErrorOccurred(new Exception("上传失败，所有重试都已完成"));
                }

                return uploadResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "上传音频文件时发生错误");
                OnUploadErrorOccurred(ex);
                return false;
            }
        }



        /// <summary>
        /// 带重试的上传方法
        /// </summary>
        private async Task<bool> UploadWithRetryAsync(string systemAudioPath, string microphonePath, CancellationToken cancellationToken)
        {
            for (int attempt = 1; attempt <= _uploadSettings.RetryCount; attempt++)
            {
                try
                {
                    OnUploadProgressChanged($"📤 上传尝试 {attempt}/{_uploadSettings.RetryCount}...");

                    var result = await UploadSingleAttemptAsync(systemAudioPath, microphonePath, cancellationToken);
                    if (result)
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "上传尝试 {Attempt} 失败", attempt);
                    
                    if (attempt < _uploadSettings.RetryCount)
                    {
                        OnUploadProgressChanged($"⚠️ 上传失败，{_uploadSettings.RetryDelay / 1000}秒后重试...");
                        await Task.Delay(_uploadSettings.RetryDelay, cancellationToken);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 单次上传尝试
        /// </summary>
        private async Task<bool> UploadSingleAttemptAsync(string systemAudioPath, string microphonePath, CancellationToken cancellationToken)
        {
            using var formData = new MultipartFormDataContent();

            // 添加系统音频文件
            var systemAudioContent = new StreamContent(File.OpenRead(systemAudioPath));
            systemAudioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
            formData.Add(systemAudioContent, "files", Path.GetFileName(systemAudioPath));

            // 添加麦克风音频文件
            var microphoneContent = new StreamContent(File.OpenRead(microphonePath));
            microphoneContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
            formData.Add(microphoneContent, "files", Path.GetFileName(microphonePath));

            // 添加其他参数
            formData.Add(new StringContent(_uploadSettings.BizType), "bizType");
            formData.Add(new StringContent(_uploadSettings.MergeAudio.ToString().ToLower()), "mergeAudio");

            // 设置授权头
            var request = new HttpRequestMessage(HttpMethod.Post, _uploadSettings.GetFullApiUrl())
            {
                Content = formData
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _uploadSettings.AuthorizationToken);

            OnUploadProgressChanged("📡 正在上传到服务器...");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation("上传成功，服务器响应: {Response}", responseContent);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("上传失败，HTTP状态码: {StatusCode}, 响应: {Response}", 
                    response.StatusCode, errorContent);
                return false;
            }
        }



        /// <summary>
        /// 触发上传进度变化事件
        /// </summary>
        protected virtual void OnUploadProgressChanged(string message)
        {
            UploadProgressChanged?.Invoke(this, message);
        }

        /// <summary>
        /// 触发上传错误事件
        /// </summary>
        protected virtual void OnUploadErrorOccurred(Exception exception)
        {
            UploadErrorOccurred?.Invoke(this, exception);
        }

        /// <summary>
        /// 触发上传完成事件
        /// </summary>
        protected virtual void OnUploadCompleted(string message)
        {
            UploadCompleted?.Invoke(this, message);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}

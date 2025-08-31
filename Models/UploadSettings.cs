using System.ComponentModel.DataAnnotations;

namespace AudioRecorder.Models
{
    /// <summary>
    /// 上传设置配置模型
    /// </summary>
    public class UploadSettings
    {
        /// <summary>
        /// 服务器基础URL
        /// </summary>
        [Required]
        public string ServerUrl { get; set; } = string.Empty;

        /// <summary>
        /// API端点
        /// </summary>
        [Required]
        public string ApiEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// 授权令牌
        /// </summary>
        [Required]
        public string AuthorizationToken { get; set; } = string.Empty;

        /// <summary>
        /// 业务类型
        /// </summary>
        public string BizType { get; set; } = "asr";

        /// <summary>
        /// 是否合并音频
        /// </summary>
        public bool MergeAudio { get; set; } = true;

        /// <summary>
        /// 是否启用自动上传
        /// </summary>
        public bool EnableAutoUpload { get; set; } = true;

        /// <summary>
        /// 上传超时时间（毫秒）
        /// </summary>
        public int UploadTimeout { get; set; } = 30000;

        /// <summary>
        /// 重试次数
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// 重试延迟（毫秒）
        /// </summary>
        public int RetryDelay { get; set; } = 5000;

        /// <summary>
        /// 获取完整的API URL
        /// </summary>
        public string GetFullApiUrl()
        {
            return $"{ServerUrl.TrimEnd('/')}{ApiEndpoint}";
        }
    }

    /// <summary>
    /// 音频设置配置模型
    /// </summary>
    public class AudioSettings
    {
        /// <summary>
        /// 采样率
        /// </summary>
        public int SampleRate { get; set; } = 16000;

        /// <summary>
        /// 声道数
        /// </summary>
        public int Channels { get; set; } = 1;

        /// <summary>
        /// 位深度
        /// </summary>
        public int BitsPerSample { get; set; } = 16;

        /// <summary>
        /// 缓冲区持续时间（秒）
        /// </summary>
        public int BufferDuration { get; set; } = 2;

        /// <summary>
        /// 录音文件保存路径类型
        /// 可选值: Documents, AppData, Desktop, Install
        /// </summary>
        public string AudioSavePath { get; set; } = "Documents";
    }
}

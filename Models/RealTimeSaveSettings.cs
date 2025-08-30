using System.ComponentModel.DataAnnotations;

namespace AudioRecorder.Models
{
    /// <summary>
    /// 实时音频保存设置配置模型
    /// </summary>
    public class RealTimeSaveSettings
    {
        /// <summary>
        /// 是否启用实时保存功能
        /// </summary>
        public bool EnableRealTimeSave { get; set; } = true;

        /// <summary>
        /// 音频处理间隔（毫秒）
        /// </summary>
        [Range(10, 200)]
        public int ProcessingIntervalMs { get; set; } = 50;

        /// <summary>
        /// 文件刷新间隔（毫秒）
        /// </summary>
        [Range(10, 200)]
        public int FlushIntervalMs { get; set; } = 50;

        /// <summary>
        /// 实时缓冲区大小
        /// </summary>
        [Range(512, 4096)]
        public int BufferSize { get; set; } = 1024;

        /// <summary>
        /// 是否启用性能监控
        /// </summary>
        public bool EnablePerformanceMonitoring { get; set; } = true;

        /// <summary>
        /// 状态更新间隔（毫秒）
        /// </summary>
        [Range(1000, 10000)]
        public int StatusUpdateIntervalMs { get; set; } = 5000;

        /// <summary>
        /// 获取处理间隔的TimeSpan
        /// </summary>
        public TimeSpan GetProcessingInterval() => TimeSpan.FromMilliseconds(ProcessingIntervalMs);

        /// <summary>
        /// 获取刷新间隔的TimeSpan
        /// </summary>
        public TimeSpan GetFlushInterval() => TimeSpan.FromMilliseconds(FlushIntervalMs);

        /// <summary>
        /// 获取状态更新间隔的TimeSpan
        /// </summary>
        public TimeSpan GetStatusUpdateInterval() => TimeSpan.FromMilliseconds(StatusUpdateIntervalMs);

        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        public bool IsValid()
        {
            return ProcessingIntervalMs > 0 && 
                   FlushIntervalMs > 0 && 
                   BufferSize > 0 && 
                   StatusUpdateIntervalMs > 0;
        }

        /// <summary>
        /// 获取配置摘要信息
        /// </summary>
        public string GetSummary()
        {
            return $"实时保存: {(EnableRealTimeSave ? "启用" : "禁用")}, " +
                   $"处理间隔: {ProcessingIntervalMs}ms, " +
                   $"刷新间隔: {FlushIntervalMs}ms, " +
                   $"缓冲区: {BufferSize}, " +
                   $"性能监控: {(EnablePerformanceMonitoring ? "启用" : "禁用")}";
        }
    }
}

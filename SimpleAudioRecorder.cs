using System;
using System.IO;
using System.Threading;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using NAudio.Wave.SampleProviders;

namespace AudioRecorder
{
    public class SimpleAudioRecorder : IDisposable
    {
        private WasapiLoopbackCapture? systemAudioCapture;
        private WaveInEvent? microphoneCapture;
        private WaveFileWriter? systemAudioWriter;
        private WaveFileWriter? microphoneAudioWriter;

        // 缓冲区
        private BufferedWaveProvider? systemAudioBuffer;
        private BufferedWaveProvider? microphoneBuffer;

        // 独立处理时钟
        private System.Threading.Timer? systemAudioTimer;
        private System.Threading.Timer? microphoneTimer;
        private bool isRecording;
        private bool isPaused;
        private MMDevice? defaultRenderDevice; // 用于监控系统音量

        // 实时音频监控和自动增益控制
        private VolumeSampleProvider? systemVolumeProvider;
        private VolumeSampleProvider? microphoneVolumeProvider;
        private System.Threading.Timer? volumeBalanceTimer;
        
        // 音频电平监控变量（用于自动音量调整）
        private float currentSystemLevel = 0f;
        private float currentMicLevel = 0f;
        private readonly object levelLock = new object();
        
        // 自动音量平衡参数
        private float systemVolumeMultiplier = 1.0f;
        private float micVolumeMultiplier = 1.0f;
        private const float TargetLevel = 0.3f; // 目标音频电平
        private const float MaxVolumeMultiplier = 5.0f; // 最大音量倍数
        private const float MinVolumeMultiplier = 0.1f; // 最小音量倍数

        private readonly int targetSampleRate = 16000; // 修改为16000Hz语音采样率
        private readonly int targetChannels = 1; // 修改为单声道
        private readonly int targetBitsPerSample = 16; // 保持16位深度

        // 文件路径存储（用于上传）
        private string? currentSystemAudioPath;
        private string? currentMicrophonePath;

        public event EventHandler<string>? StatusChanged;
        public event EventHandler<Exception>? ErrorOccurred;
        public bool IsRecording => isRecording;

        /// <summary>
        /// 获取当前录制的系统音频文件路径
        /// </summary>
        public string? GetCurrentSystemAudioPath() => currentSystemAudioPath;

        /// <summary>
        /// 获取当前录制的麦克风音频文件路径
        /// </summary>
        public string? GetCurrentMicrophonePath() => currentMicrophonePath;

        public SimpleAudioRecorder()
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "AudioRecordings"));
        }

        public void StartRecording()
        {
            if (isRecording)
            {
                StatusChanged?.Invoke(this, "录制已在进行中。");
                return;
            }

            try
            {
                isRecording = true;
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "AudioRecordings");
                
                // 创建两个独立的输出文件
                string systemAudioPath = Path.Combine(baseDir, $"SystemAudio_{timestamp}.wav");
                string microphonePath = Path.Combine(baseDir, $"Microphone_{timestamp}.wav");
                
                // 保存文件路径（用于上传）
                currentSystemAudioPath = systemAudioPath;
                currentMicrophonePath = microphonePath;
                
                var outputFormat = new WaveFormat(targetSampleRate, targetBitsPerSample, targetChannels);
                systemAudioWriter = new WaveFileWriter(systemAudioPath, outputFormat);
                microphoneAudioWriter = new WaveFileWriter(microphonePath, outputFormat);

                // 初始化用于音量监控的设备
                defaultRenderDevice = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                SetupSystemAudioSource();
                SetupMicrophoneSource();
                StartSeparateProcessing();
                StartVolumeBalancing(); // 启动自动音量平衡

                StatusChanged?.Invoke(this, "✅ 录制已开始，分离录制两个音频文件：");
                StatusChanged?.Invoke(this, $"🔊 系统音频 -> {systemAudioPath}");
                StatusChanged?.Invoke(this, $"🎤 麦克风音频 -> {microphonePath}");
                StatusChanged?.Invoke(this, "🎚️ 已启用自动音量平衡。");
                StatusChanged?.Invoke(this, $"🎵 音频格式: {targetSampleRate}Hz, {targetChannels}声道, {targetBitsPerSample}位");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new Exception("启动录制失败", ex));
                StopRecording();
            }
        }

        private void SetupSystemAudioSource()
        {
            systemAudioCapture = new WasapiLoopbackCapture();
            systemAudioBuffer = new BufferedWaveProvider(systemAudioCapture.WaveFormat)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromSeconds(2) // 适中的缓冲区
            };
            systemAudioCapture.DataAvailable += (s, e) =>
            {
                systemAudioBuffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
                
                // 计算系统音频电平（用于自动音量调整）
                CalculateSystemAudioLevel(e.Buffer, 0, e.BytesRecorded);
            };
            systemAudioCapture.StartRecording();
            StatusChanged?.Invoke(this, "🔊 系统音频捕获已启动。");
        }

        private void SetupMicrophoneSource()
        {
            microphoneCapture = new WaveInEvent();
            microphoneBuffer = new BufferedWaveProvider(microphoneCapture.WaveFormat)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromSeconds(2) // 适中的缓冲区
            };
            microphoneCapture.DataAvailable += (s, e) =>
            {
                microphoneBuffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
                
                // 计算麦克风音频电平（用于自动音量调整）
                CalculateMicrophoneAudioLevel(e.Buffer, 0, e.BytesRecorded);
            };
            microphoneCapture.StartRecording();
            StatusChanged?.Invoke(this, "🎤 麦克风捕获已启动。");
        }
        
        // 计算系统音频电平（用于自动音量调整）
        private void CalculateSystemAudioLevel(byte[] buffer, int offset, int count)
        {
            if (buffer == null || count <= 0) return;
            
            try
            {
                float rms = CalculateSimpleRms(buffer, offset, count);
                
                lock (levelLock)
                {
                    // 快速响应的平滑处理
                    currentSystemLevel = currentSystemLevel * 0.6f + rms * 0.4f;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"系统音频电平计算错误: {ex.Message}");
            }
        }
        
        // 计算麦克风音频电平（用于自动音量调整）
        private void CalculateMicrophoneAudioLevel(byte[] buffer, int offset, int count)
        {
            if (buffer == null || count <= 0) return;
            
            try
            {
                float rms = CalculateSimpleRms(buffer, offset, count);
                
                lock (levelLock)
                {
                    // 快速响应的平滑处理
                    currentMicLevel = currentMicLevel * 0.6f + rms * 0.4f;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"麦克风音频电平计算错误: {ex.Message}");
            }
        }


        
        // 简化的RMS计算方法
        private float CalculateSimpleRms(byte[] buffer, int offset, int count)
        {
            float sum = 0f;
            int sampleCount = count / 2; // 假设16位音频
            
            for (int i = 0; i < sampleCount && offset + i * 2 + 1 < buffer.Length; i++)
            {
                short sample = BitConverter.ToInt16(buffer, offset + i * 2);
                float normalizedSample = sample / 32768f; // 归一化到-1到1
                sum += normalizedSample * normalizedSample;
            }
            
            return sampleCount > 0 ? (float)Math.Sqrt(sum / sampleCount) : 0f;
        }
        
        // 启动自动音量平衡
        private void StartVolumeBalancing()
        {
            volumeBalanceTimer = new System.Threading.Timer(state =>
            {
                if (!isRecording) return;
                
                try
                {
                    AdjustVolumeBalance();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"音量平衡调整错误: {ex.Message}");
                }
            }, null, 2000, 1000); // 2秒后开始，每1秒调整一次
        }
        
        // 自动调整音量平衡
        private void AdjustVolumeBalance()
        {
            float systemLevel, micLevel;
            lock (levelLock)
            {
                systemLevel = currentSystemLevel;
                micLevel = currentMicLevel;
            }
            
            // 计算系统音频的调整倍数
            if (systemLevel > 0.001f)
            {
                float systemRatio = TargetLevel / systemLevel;
                systemVolumeMultiplier = Math.Max(MinVolumeMultiplier, 
                    Math.Min(MaxVolumeMultiplier, systemRatio));
            }
            
            // 计算麦克风的调整倍数
            if (micLevel > 0.001f)
            {
                float micRatio = TargetLevel / micLevel;
                micVolumeMultiplier = Math.Max(MinVolumeMultiplier, 
                    Math.Min(MaxVolumeMultiplier, micRatio));
            }
            
            // 应用新的音量设置
            if (systemVolumeProvider != null)
            {
                systemVolumeProvider.Volume = 0.8f * systemVolumeMultiplier;
            }
            
            if (microphoneVolumeProvider != null)
            {
                microphoneVolumeProvider.Volume = 1.0f * micVolumeMultiplier;
            }
            
            // 调试输出
            System.Diagnostics.Debug.WriteLine($"音量调整 - 系统: {systemLevel:F4} -> 倍数{systemVolumeMultiplier:F2}, 麦克风: {micLevel:F4} -> 倍数{micVolumeMultiplier:F2}");
        }

        private void StartSeparateProcessing()
        {
            // 构建简化的处理管道
            var (systemProvider, micProvider) = BuildSimpleProcessingPipelines();
            
            // 更保守的处理频率，基于16000Hz单声道
            int intervalMilliseconds = 100; // 提高到100ms，更稳定
            var systemBuffer = new float[systemProvider.WaveFormat.SampleRate * systemProvider.WaveFormat.Channels * intervalMilliseconds / 1000];
            var micBuffer = new float[micProvider.WaveFormat.SampleRate * micProvider.WaveFormat.Channels * intervalMilliseconds / 1000];
            
            System.Diagnostics.Debug.WriteLine($"音频缓冲区 - 系统: {systemBuffer.Length}样本, 麦克风: {micBuffer.Length}样本");
            System.Diagnostics.Debug.WriteLine($"音频格式 - 采样率: {targetSampleRate}Hz, 声道: {targetChannels}, 位深: {targetBitsPerSample}bit");
            
            // 系统音频处理时钟
            systemAudioTimer = new System.Threading.Timer(state =>
            {
                if (!isRecording) return;
                try
                {
                    int samplesRead = systemProvider.Read(systemBuffer, 0, systemBuffer.Length);
                    if (samplesRead > 0)
                    {
                        systemAudioWriter?.WriteSamples(systemBuffer, 0, samplesRead);
                    }
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, new Exception("系统音频处理错误", ex));
                }
            }, null, 0, intervalMilliseconds);
            
            // 麦克风音频处理时钟
            microphoneTimer = new System.Threading.Timer(state =>
            {
                if (!isRecording) return;
                try
                {
                    int samplesRead = micProvider.Read(micBuffer, 0, micBuffer.Length);
                    if (samplesRead > 0)
                    {
                        microphoneAudioWriter?.WriteSamples(micBuffer, 0, samplesRead);
                    }
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, new Exception("麦克风音频处理错误", ex));
                }
            }, null, 0, intervalMilliseconds);
        }

        // 移除音频监控相关代码

        private (ISampleProvider systemProvider, ISampleProvider micProvider) BuildSimpleProcessingPipelines()
        {
            if (systemAudioBuffer == null || microphoneBuffer == null)
                throw new InvalidOperationException("音频缓冲区未初始化。");

            // 系统音频处理链 - 转换为单声道
            var systemSampleProvider = systemAudioBuffer.ToSampleProvider();
            
            // 转换为单声道（如果是立体声则混合）
            if (systemSampleProvider.WaveFormat.Channels > 1)
            {
                systemSampleProvider = new StereoToMonoSampleProvider(systemSampleProvider)
                {
                    LeftVolume = 0.5f,
                    RightVolume = 0.5f
                };
            }
            
            // 重采样到16000Hz
            if (systemSampleProvider.WaveFormat.SampleRate != targetSampleRate)
            {
                systemSampleProvider = new WdlResamplingSampleProvider(systemSampleProvider, targetSampleRate);
            }
            
            // 音量控制
            systemVolumeProvider = new VolumeSampleProvider(systemSampleProvider) { Volume = 0.8f };
            
            // 麦克风处理链 - 转换为单声道
            var micSampleProvider = microphoneBuffer.ToSampleProvider();
            
            // 转换为单声道（如果是立体声则混合）
            if (micSampleProvider.WaveFormat.Channels > 1)
            {
                micSampleProvider = new StereoToMonoSampleProvider(micSampleProvider)
                {
                    LeftVolume = 0.5f,
                    RightVolume = 0.5f
                };
            }
            
            // 重采样到16000Hz
            if (micSampleProvider.WaveFormat.SampleRate != targetSampleRate)
            {
                micSampleProvider = new WdlResamplingSampleProvider(micSampleProvider, targetSampleRate);
            }
            
            // 音量控制
            microphoneVolumeProvider = new VolumeSampleProvider(micSampleProvider) { Volume = 1.0f };

            return (systemVolumeProvider, microphoneVolumeProvider);
        }

        public void StopRecording()
        {
            if (!isRecording) return;
            
            isRecording = false;

            try
            {
                // 停止音频处理时钟
                systemAudioTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                systemAudioTimer?.Dispose();
                systemAudioTimer = null;

                // 停止麦克风时钟
                microphoneTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                microphoneTimer?.Dispose();
                microphoneTimer = null;

                // 停止音量平衡时钟
                volumeBalanceTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                volumeBalanceTimer?.Dispose();
                volumeBalanceTimer = null;

                // 停止音频捕获
                systemAudioCapture?.StopRecording();
                microphoneCapture?.StopRecording();
                
                Thread.Sleep(100);
                
                // 清理资源
                systemAudioCapture?.Dispose();
                microphoneCapture?.Dispose();
                systemAudioWriter?.Dispose();
                microphoneAudioWriter?.Dispose();
                defaultRenderDevice?.Dispose();

                systemAudioCapture = null;
                microphoneCapture = null;
                systemAudioWriter = null;
                microphoneAudioWriter = null;
                systemAudioBuffer = null;
                microphoneBuffer = null;
                defaultRenderDevice = null;
                systemVolumeProvider = null;
                microphoneVolumeProvider = null;

                // 重置电平和音量倍数
                lock (levelLock)
                {
                    currentSystemLevel = 0f;
                    currentMicLevel = 0f;
                }
                systemVolumeMultiplier = 1.0f;
                micVolumeMultiplier = 1.0f;

                // 清理文件路径
                currentSystemAudioPath = null;
                currentMicrophonePath = null;

                StatusChanged?.Invoke(this, "⏹ 录制已停止，文件已保存。");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new Exception("停止录制时出错", ex));
            }
        }

        public void PauseRecording()
        {
            if (!isRecording || isPaused) return;
            
            isPaused = true;
            
            try
            {
                // 暂停音频捕获
                systemAudioCapture?.StopRecording();
                microphoneCapture?.StopRecording();
                
                // 暂停处理时钟
                systemAudioTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                microphoneTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                volumeBalanceTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);

                StatusChanged?.Invoke(this, "⏸ 录制已暂停");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new Exception("暂停录制时出错", ex));
            }
        }

        public void ResumeRecording()
        {
            if (!isRecording || !isPaused) return;
            
            isPaused = false;
            
            try
            {
                // 恢复音频捕获
                systemAudioCapture?.StartRecording();
                microphoneCapture?.StartRecording();
                
                // 恢复处理时钟
                systemAudioTimer?.Change(0, 10);  // 每10ms处理一次
                microphoneTimer?.Change(0, 10);   // 每10ms处理一次
                volumeBalanceTimer?.Change(0, 500); // 每500ms调整一次音量平衡

                StatusChanged?.Invoke(this, "▶ 录制已恢复");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new Exception("恢复录制时出错", ex));
            }
        }

        public bool IsPaused => isPaused;

        public void Dispose()
        {
            StopRecording();
            GC.SuppressFinalize(this);
        }
    }

    // 移除音频电平监控事件参数类，不再需要
    // /// <summary>
    // /// 音频电平监控事件参数
    // /// </summary>
    // public class AudioLevelEventArgs : EventArgs
    // {
    //     public float SystemLevel { get; set; }
    //     public float SystemGain { get; set; }
    //     public float MicrophoneLevel { get; set; }
    //     public float MicrophoneGain { get; set; }
    //     public float SystemVolume { get; set; }
    //     public string Status { get; set; } = "";
    // }
}
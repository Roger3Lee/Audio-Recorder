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
        private MMDevice? defaultRenderDevice; // 用于监控系统音量

        // 实时音频监控
        private VolumeSampleProvider? systemVolumeProvider;
        private VolumeSampleProvider? microphoneVolumeProvider;

        private readonly int targetSampleRate = 44100; // 降低采样率
        private readonly int targetChannels = 2;
        private readonly int targetBitsPerSample = 16; // 降低位深度

        public event EventHandler<string>? StatusChanged;
        public event EventHandler<Exception>? ErrorOccurred;
        // 移除音频电平监控事件：public event EventHandler<AudioLevelEventArgs>? AudioLevelChanged;
        public bool IsRecording => isRecording;

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
                
                var outputFormat = new WaveFormat(targetSampleRate, targetBitsPerSample, targetChannels);
                systemAudioWriter = new WaveFileWriter(systemAudioPath, outputFormat);
                microphoneAudioWriter = new WaveFileWriter(microphonePath, outputFormat);

                // 初始化用于音量监控的设备
                defaultRenderDevice = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                SetupSystemAudioSource();
                SetupMicrophoneSource();
                StartSeparateProcessing();
                // 移除音频监控相关代码

                StatusChanged?.Invoke(this, "✅ 录制已开始，分离录制两个音频文件：");
                StatusChanged?.Invoke(this, $"🔊 系统音频 -> {systemAudioPath}");
                StatusChanged?.Invoke(this, $"🎤 麦克风音频 -> {microphonePath}");
                // 移除音频监控相关提示
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
                
                // 计算系统音频电平
                // CalculateSystemAudioLevel(e.Buffer, 0, e.BytesRecorded); // 移除旧的电平计算
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
                
                // 计算麦克风音频电平
                // CalculateMicrophoneAudioLevel(e.Buffer, 0, e.BytesRecorded); // 移除旧的电平计算
            };
            microphoneCapture.StartRecording();
            StatusChanged?.Invoke(this, "🎤 麦克风捕获已启动。");
        }
        
        // 移除旧的16位计算方法，已被新方法替代

        private void StartSeparateProcessing()
        {
            // 构建简化的处理管道
            var (systemProvider, micProvider) = BuildSimpleProcessingPipelines();
            
            // 更保守的处理频率
            int intervalMilliseconds = 100; // 提高到100ms，更稳定
            var systemBuffer = new float[systemProvider.WaveFormat.SampleRate * systemProvider.WaveFormat.Channels * intervalMilliseconds / 1000];
            var micBuffer = new float[micProvider.WaveFormat.SampleRate * micProvider.WaveFormat.Channels * intervalMilliseconds / 1000];
            
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

            // 系统音频处理链 - 简化版本
            var systemSampleProvider = systemAudioBuffer.ToSampleProvider();
            
            // 转换为立体声
            if (systemSampleProvider.WaveFormat.Channels == 1)
            {
                systemSampleProvider = new MonoToStereoSampleProvider(systemSampleProvider);
            }
            
            // 重采样
            if (systemSampleProvider.WaveFormat.SampleRate != targetSampleRate)
            {
                systemSampleProvider = new WdlResamplingSampleProvider(systemSampleProvider, targetSampleRate);
            }
            
            // 音量控制
            systemVolumeProvider = new VolumeSampleProvider(systemSampleProvider) { Volume = 0.8f };
            
            // 麦克风处理链 - 简化版本
            var micSampleProvider = microphoneBuffer.ToSampleProvider();
            
            // 转换为立体声
            if (micSampleProvider.WaveFormat.Channels == 1)
            {
                micSampleProvider = new MonoToStereoSampleProvider(micSampleProvider);
            }
            
            // 重采样
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
                // 停止系统音频时钟
                systemAudioTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                systemAudioTimer?.Dispose();
                systemAudioTimer = null;

                // 停止麦克风时钟
                microphoneTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                microphoneTimer?.Dispose();
                microphoneTimer = null;

                // 停止监控时钟
                // monitoringTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite); // 移除旧的监控时钟
                // monitoringTimer?.Dispose(); // 移除旧的监控时钟
                // monitoringTimer = null; // 移除旧的监控时钟

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

                // 重置电平
                // lock (levelLock) // 移除旧的电平重置
                // {
                //     currentSystemLevel = 0f;
                //     currentMicLevel = 0f;
                // }

                StatusChanged?.Invoke(this, "⏹ 录制已停止，文件已保存。");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new Exception("停止录制时出错", ex));
            }
        }

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
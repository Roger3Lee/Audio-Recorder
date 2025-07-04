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
        private WaveFileWriter? outputWriter;

        // 缓冲区
        private BufferedWaveProvider? systemAudioBuffer;
        private BufferedWaveProvider? microphoneBuffer;

        // 高精度混音时钟
        private System.Threading.Timer? mixingTimer;
        private bool isRecording;
        private MMDevice? defaultRenderDevice; // 用于监控系统音量

        private readonly int targetSampleRate = 48000;
        private readonly int targetChannels = 2;
        private readonly int targetBitsPerSample = 24;

        public event EventHandler<string>? StatusChanged;
        public event EventHandler<Exception>? ErrorOccurred;
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
                string outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "AudioRecordings", $"Recording_DynamicVol_{timestamp}.wav");
                var outputFormat = new WaveFormat(targetSampleRate, targetBitsPerSample, targetChannels);
                outputWriter = new WaveFileWriter(outputPath, outputFormat);

                // 初始化用于音量监控的设备
                defaultRenderDevice = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                SetupSystemAudioSource();
                SetupMicrophoneSource();
                StartMixingProcess();

                StatusChanged?.Invoke(this, $"✅ 专业录制已开始 -> {outputPath}");
                StatusChanged?.Invoke(this, "🔊 已启用动态系统音量调整。");
                StatusChanged?.Invoke(this, "🔇 已启用专业降噪处理 (噪音门控 + 频域滤波 + 自适应降噪)。");
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
                BufferDuration = TimeSpan.FromSeconds(2) // 巨大的缓冲区，应对延迟
            };
            systemAudioCapture.DataAvailable += (s, e) =>
            {
                systemAudioBuffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
            };
            systemAudioCapture.StartRecording();
            StatusChanged?.Invoke(this, "🔊 系统音频捕获已启动。");
        }

        private void SetupMicrophoneSource()
        {
            microphoneCapture = new WaveInEvent { WaveFormat = new WaveFormat(44100, 1) };
            microphoneBuffer = new BufferedWaveProvider(microphoneCapture.WaveFormat)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromSeconds(2) // 巨大的缓冲区，应对延迟
            };
            microphoneCapture.DataAvailable += (s, e) =>
            {
                microphoneBuffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
            };
            microphoneCapture.StartRecording();
            StatusChanged?.Invoke(this, "🎤 麦克风捕获已启动。");
        }

        private void StartMixingProcess()
        {
            var finalProvider = BuildProcessingPipeline();
            
            int intervalMilliseconds = 20; // 每20ms拉取一次数据
            var buffer = new float[finalProvider.WaveFormat.SampleRate * finalProvider.WaveFormat.Channels * intervalMilliseconds / 1000];
            
            mixingTimer = new System.Threading.Timer(state =>
            {
                if (!isRecording) return;
                try
                {
                    // 使用阻塞式读取，确保每次都读取完整的、与时间片匹配的数据量
                    int samplesRead = finalProvider.ReadFully(buffer, 0, buffer.Length);
                    if (samplesRead > 0)
                    {
                        outputWriter?.WriteSamples(buffer, 0, samplesRead);
                    }
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, new Exception("混音时钟错误", ex));
                    isRecording = false; 
                }
            }, null, 0, intervalMilliseconds);
        }

        private ISampleProvider BuildProcessingPipeline()
        {
            if (systemAudioBuffer == null || microphoneBuffer == null || defaultRenderDevice == null)
                throw new InvalidOperationException("音频缓冲区或音频设备未初始化。");

            // 系统音频处理链：专门针对背景噪音和电磁干扰
            var systemSampleProvider = systemAudioBuffer.ToSampleProvider()
                .ApplySystemAudioDenoiser(0.25f)  // 首先应用专业系统音频降噪
                .ToStereo(1, 1)
                .Resample(targetSampleRate)
                .ApplyNoiseGate(0.015f, 12f)      // 应用噪音门，更严格的阈值
                .ApplyDynamicSystemVolume(defaultRenderDevice.AudioEndpointVolume, 0.5f); // 降低基础增益
            
            // 麦克风处理链：保护语音同时降噪
            var micSampleProvider = microphoneBuffer.ToSampleProvider()
                .ApplyMicrophoneDenoiser()        // 专业麦克风降噪
                .ApplyVoiceEQ()                   // 语音均衡
                .ApplyNoiseGate(0.008f, 15f)      // 麦克风专用噪音门，更低的阈值
                .AdjustVolume(2.8f)               // 适当提升以补偿降噪损失
                .ToStereo(1, 1)
                .Resample(targetSampleRate);

            var mixer = new MixingSampleProvider(new[] { systemSampleProvider, micSampleProvider });
            
            // 最终输出处理：轻度压缩以确保动态范围
            return mixer.ApplyCompressor(threshold: 0.6f, ratio: 3.0f);
        }

        public void StopRecording()
        {
            if (!isRecording) return;
            
            isRecording = false;

            try
            {
                mixingTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                mixingTimer?.Dispose();
                mixingTimer = null;

                systemAudioCapture?.StopRecording();
                microphoneCapture?.StopRecording();
                
                Thread.Sleep(100);
                
                systemAudioCapture?.Dispose();
                microphoneCapture?.Dispose();
                outputWriter?.Dispose();
                defaultRenderDevice?.Dispose();
                
                systemAudioCapture = null;
                microphoneCapture = null;
                outputWriter = null;
                systemAudioBuffer = null;
                microphoneBuffer = null;
                defaultRenderDevice = null;
                
                StatusChanged?.Invoke(this, "🛑 录制已停止。");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new Exception("停止录制时出错", ex));
            }
        }

        public void Dispose()
        {
            StopRecording();
        }
    }

    /// <summary>
    /// 音频处理链的扩展方法
    /// </summary>
    public static class AudioProcessingExtensions
    {
        /// <summary>
        /// 阻塞式读取，直到填满缓冲区或源结束
        /// </summary>
        public static int ReadFully(this ISampleProvider provider, float[] buffer, int offset, int count)
        {
            int totalSamplesRead = 0;
            while (totalSamplesRead < count)
            {
                int samplesRead = provider.Read(buffer, offset + totalSamplesRead, count - totalSamplesRead);
                if (samplesRead == 0)
                {
                    // 源已结束
                    break;
                }
                totalSamplesRead += samplesRead;
            }
            return totalSamplesRead;
        }

        public static ISampleProvider Resample(this ISampleProvider source, int newSampleRate)
        {
            if (source.WaveFormat.SampleRate == newSampleRate)
            {
                return source;
            }
            return new WdlResamplingSampleProvider(source, newSampleRate);
        }

        public static ISampleProvider ToStereo(this ISampleProvider source, float leftVol = 1.0f, float rightVol = 1.0f)
        {
            if (source.WaveFormat.Channels == 2)
            {
                return source;
            }
            return new MonoToStereoSampleProvider(source) { LeftVolume = leftVol, RightVolume = rightVol };
        }

        public static ISampleProvider AdjustVolume(this ISampleProvider source, float volume)
        {
            return new VolumeSampleProvider(source) { Volume = volume };
        }

        public static ISampleProvider ApplyVoiceEQ(this ISampleProvider source)
        {
            return new VoiceEqSampleProvider(source);
        }

        public static ISampleProvider ApplyCompressor(this ISampleProvider source, float threshold = 0.5f, float ratio = 4.0f, float attack = 0.005f, float release = 0.2f)
        {
            return new SimpleCompressorSampleProvider(source, threshold, ratio, attack, release);
        }

        public static ISampleProvider ApplyDynamicSystemVolume(this ISampleProvider source, AudioEndpointVolume endpointVolume, float baseGain)
        {
            return new DynamicSystemVolumeSampleProvider(source, endpointVolume, baseGain);
        }

        public static ISampleProvider ApplyNoiseGate(this ISampleProvider source, float threshold = 0.02f, float ratio = 10f)
        {
            return new NoiseGateSampleProvider(source, threshold, ratio);
        }

        public static ISampleProvider ApplySystemAudioDenoiser(this ISampleProvider source, float adaptiveGain = 0.25f)
        {
            return new SystemAudioDenoiserProvider(source, adaptiveGain);
        }

        public static ISampleProvider ApplyMicrophoneDenoiser(this ISampleProvider source)
        {
            return new MicrophoneDenoiserProvider(source);
        }
    }
}
using System;
using NAudio.Wave;
using NAudio.Dsp;
using NAudio.CoreAudioApi;

namespace AudioRecorder
{
    /// <summary>
    /// 语音优化均衡器 (EQ)
    /// - 高通滤波器去除低频嗡嗡声
    /// - 中频增强以提高语音清晰度
    /// </summary>
    public class VoiceEqSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider sourceProvider;
        private readonly BiQuadFilter highPassFilter;
        private readonly BiQuadFilter midBoostFilter;

        public WaveFormat WaveFormat => sourceProvider.WaveFormat;

        public VoiceEqSampleProvider(ISampleProvider sourceProvider)
        {
            this.sourceProvider = sourceProvider;
            
            // 80Hz高通滤波器，消除环境低频噪声
            highPassFilter = BiQuadFilter.HighPassFilter(WaveFormat.SampleRate, 80f, 0.7f);
            
            // 3kHz中频增强，提升语音清晰度
            midBoostFilter = BiQuadFilter.PeakingEQ(WaveFormat.SampleRate, 3000f, 1.0f, 2.0f);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = sourceProvider.Read(buffer, offset, count);
            
            for (int i = 0; i < samplesRead; i++)
            {
                buffer[offset + i] = midBoostFilter.Transform(highPassFilter.Transform(buffer[offset + i]));
            }
            
            return samplesRead;
        }
    }

    /// <summary>
    /// 动态范围压缩器
    /// </summary>
    public class SimpleCompressorSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider sourceProvider;
        private readonly float threshold;
        private readonly float ratio;
        private readonly float attack;
        private readonly float release;
        private float envelope;

        public WaveFormat WaveFormat => sourceProvider.WaveFormat;

        public SimpleCompressorSampleProvider(ISampleProvider source, float threshold = 0.5f, float ratio = 4f, float attack = 0.002f, float release = 0.1f)
        {
            this.sourceProvider = source;
            this.threshold = threshold;
            this.ratio = ratio;
            this.attack = attack;
            this.release = release;
            this.envelope = 0f;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = sourceProvider.Read(buffer, offset, count);

            for (int i = 0; i < samplesRead; i++)
            {
                float sample = buffer[offset + i];
                float level = Math.Abs(sample);

                // 包络跟踪
                envelope += (level - envelope) * (level > envelope ? attack : release);
                
                // 计算增益衰减
                float gainReduction = 1.0f;
                if (envelope > threshold)
                {
                    float excess = envelope - threshold;
                    float compressedExcess = excess / ratio;
                    gainReduction = (threshold + compressedExcess) / envelope;
                }
                
                buffer[offset + i] = sample * gainReduction;
            }
            
            return samplesRead;
        }
    }

    /// <summary>
    /// 动态系统音量控制器
    /// - 根据系统主音量实时调整增益
    /// </summary>
    public class DynamicSystemVolumeSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider sourceProvider;
        private readonly AudioEndpointVolume endpointVolume;
        private readonly float baseGain;

        public WaveFormat WaveFormat => sourceProvider.WaveFormat;

        public DynamicSystemVolumeSampleProvider(ISampleProvider source, AudioEndpointVolume endpointVolume, float baseGain = 0.8f)
        {
            this.sourceProvider = source;
            this.endpointVolume = endpointVolume;
            this.baseGain = baseGain;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = sourceProvider.Read(buffer, offset, count);
            
            // 获取当前系统音量 (0.0 to 1.0)
            float systemVolume = endpointVolume.MasterVolumeLevelScalar;

            // 将系统音量应用到捕获的音频上，并乘以一个基础增益
            float finalGain = systemVolume * baseGain;

            for (int i = 0; i < samplesRead; i++)
            {
                buffer[offset + i] *= finalGain;
            }

            return samplesRead;
        }
    }

    /// <summary>
    /// 专业降噪门控器
    /// - 智能噪音门，自动检测和抑制背景噪音
    /// - 自适应阈值调整
    /// </summary>
    public class NoiseGateSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider sourceProvider;
        private readonly float gateThreshold;
        private readonly float gateRatio;
        private readonly float attack;
        private readonly float release;
        private readonly float holdTime;
        
        private float gateEnvelope;
        private float rmsLevel;
        private float noiseFloor;
        private int holdCounter;
        private readonly int holdSamples;
        private readonly float[] rmsBuffer;
        private int rmsIndex;
        private float rmsSum;

        public WaveFormat WaveFormat => sourceProvider.WaveFormat;

        public NoiseGateSampleProvider(ISampleProvider source, float threshold = 0.02f, float ratio = 10f, 
            float attack = 0.001f, float release = 0.1f, float holdTime = 0.01f)
        {
            this.sourceProvider = source;
            this.gateThreshold = threshold;
            this.gateRatio = ratio;
            this.attack = attack;
            this.release = release;
            this.holdTime = holdTime;
            
            this.gateEnvelope = 1.0f;
            this.holdSamples = (int)(holdTime * source.WaveFormat.SampleRate);
            this.rmsBuffer = new float[1024]; // 1024样本的RMS窗口
            this.noiseFloor = threshold * 0.5f;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = sourceProvider.Read(buffer, offset, count);

            for (int i = 0; i < samplesRead; i++)
            {
                float sample = buffer[offset + i];
                float absSample = Math.Abs(sample);
                
                // 更新RMS计算
                UpdateRMS(absSample);
                
                // 自适应噪音底线检测
                UpdateNoiseFloor();
                
                // 动态阈值
                float adaptiveThreshold = Math.Max(gateThreshold, noiseFloor * 2.0f);
                
                // 决定门控状态
                bool gateOpen = rmsLevel > adaptiveThreshold;
                
                if (gateOpen)
                {
                    // 门开启：快速上升
                    gateEnvelope += (1.0f - gateEnvelope) * attack;
                    holdCounter = holdSamples; // 重置保持计数器
                }
                else if (holdCounter > 0)
                {
                    // 保持期：维持当前包络
                    holdCounter--;
                }
                else
                {
                    // 门关闭：缓慢下降
                    float targetGain = 1.0f / gateRatio; // 不完全静音，保留一些信号
                    gateEnvelope += (targetGain - gateEnvelope) * release;
                }
                
                // 应用门控增益
                buffer[offset + i] = sample * gateEnvelope;
            }

            return samplesRead;
        }

        private void UpdateRMS(float sample)
        {
            // 移动平均RMS计算
            rmsSum -= rmsBuffer[rmsIndex];
            rmsBuffer[rmsIndex] = sample * sample;
            rmsSum += rmsBuffer[rmsIndex];
            rmsIndex = (rmsIndex + 1) % rmsBuffer.Length;
            rmsLevel = (float)Math.Sqrt(rmsSum / rmsBuffer.Length);
        }

        private void UpdateNoiseFloor()
        {
            // 缓慢适应噪音底线
            if (rmsLevel < noiseFloor * 1.5f)
            {
                noiseFloor = noiseFloor * 0.999f + rmsLevel * 0.001f;
            }
        }
    }

    /// <summary>
    /// 系统音频降噪处理器
    /// - 专门处理系统音频的电磁干扰和背景噪音
    /// </summary>
    public class SystemAudioDenoiserProvider : ISampleProvider
    {
        private readonly ISampleProvider sourceProvider;
        private readonly BiQuadFilter[] notchFilters; // 陷波滤波器组
        private readonly BiQuadFilter lowPassFilter;
        private readonly float[] delayBuffer;
        private int delayIndex;
        private readonly float adaptiveGain;

        public WaveFormat WaveFormat => sourceProvider.WaveFormat;

        public SystemAudioDenoiserProvider(ISampleProvider source, float adaptiveGain = 0.3f)
        {
            this.sourceProvider = source;
            this.adaptiveGain = adaptiveGain;
            
            // 创建陷波滤波器组，去除特定频率的电磁干扰
            notchFilters = new BiQuadFilter[]
            {
                BiQuadFilter.NotchFilter(source.WaveFormat.SampleRate, 50f, 0.5f),   // 50Hz电源噪音
                BiQuadFilter.NotchFilter(source.WaveFormat.SampleRate, 100f, 0.5f),  // 100Hz谐波
                BiQuadFilter.NotchFilter(source.WaveFormat.SampleRate, 150f, 0.5f),  // 150Hz谐波
                BiQuadFilter.NotchFilter(source.WaveFormat.SampleRate, 60f, 0.5f),   // 60Hz电源噪音(美国标准)
                BiQuadFilter.NotchFilter(source.WaveFormat.SampleRate, 120f, 0.5f)   // 120Hz谐波
            };
            
            // 轻微的低通滤波，去除高频噪音
            lowPassFilter = BiQuadFilter.LowPassFilter(source.WaveFormat.SampleRate, 16000f, 1.0f);
            
            // 延迟缓冲区用于自适应滤波
            delayBuffer = new float[128];
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = sourceProvider.Read(buffer, offset, count);

            for (int i = 0; i < samplesRead; i++)
            {
                float sample = buffer[offset + i];
                
                // 应用陷波滤波器组
                foreach (var filter in notchFilters)
                {
                    sample = filter.Transform(sample);
                }
                
                // 应用低通滤波
                sample = lowPassFilter.Transform(sample);
                
                // 自适应噪音抑制
                sample = ApplyAdaptiveNoiseReduction(sample);
                
                // 应用自适应增益（降低整体系统音频音量）
                buffer[offset + i] = sample * adaptiveGain;
            }

            return samplesRead;
        }

        private float ApplyAdaptiveNoiseReduction(float sample)
        {
            // 存储当前样本到延迟缓冲区
            delayBuffer[delayIndex] = sample;
            delayIndex = (delayIndex + 1) % delayBuffer.Length;
            
            // 计算局部能量
            float localEnergy = 0f;
            for (int j = 0; j < delayBuffer.Length; j++)
            {
                localEnergy += delayBuffer[j] * delayBuffer[j];
            }
            localEnergy /= delayBuffer.Length;
            
            // 如果局部能量很低，应用更强的噪音抑制
            if (localEnergy < 0.0001f) // 非常低的能量阈值
            {
                sample *= 0.1f; // 大幅降低噪音
            }
            else if (localEnergy < 0.001f) // 低能量
            {
                sample *= 0.5f; // 中等噪音抑制
            }
            
            return sample;
        }
    }

    /// <summary>
    /// 麦克风专用降噪处理器
    /// - 保护语音信号的同时抑制背景噪音
    /// </summary>
    public class MicrophoneDenoiserProvider : ISampleProvider
    {
        private readonly ISampleProvider sourceProvider;
        private readonly float[] spectralBuffer;
        private readonly float[] noiseProfile;
        private readonly BiQuadFilter preEmphasisFilter;
        private readonly BiQuadFilter deEmphasisFilter;
        private float voiceActivityLevel;

        public WaveFormat WaveFormat => sourceProvider.WaveFormat;

        public MicrophoneDenoiserProvider(ISampleProvider source)
        {
            this.sourceProvider = source;
            this.spectralBuffer = new float[512];
            this.noiseProfile = new float[256];
            
            // 预加重滤波器（提升高频）
            preEmphasisFilter = BiQuadFilter.HighPassFilter(source.WaveFormat.SampleRate, 200f, 0.7f);
            
            // 去加重滤波器（恢复频响）
            deEmphasisFilter = BiQuadFilter.LowPassFilter(source.WaveFormat.SampleRate, 8000f, 0.7f);
            
            // 初始化噪音轮廓
            for (int i = 0; i < noiseProfile.Length; i++)
            {
                noiseProfile[i] = 0.001f; // 初始噪音估计
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = sourceProvider.Read(buffer, offset, count);

            for (int i = 0; i < samplesRead; i++)
            {
                float sample = buffer[offset + i];
                
                // 检测语音活动
                UpdateVoiceActivity(sample);
                
                // 预加重处理
                sample = preEmphasisFilter.Transform(sample);
                
                // 频谱降噪
                sample = ApplySpectralNoiseReduction(sample);
                
                // 去加重处理
                sample = deEmphasisFilter.Transform(sample);
                
                // 动态范围保护
                sample = ProtectVoiceDynamics(sample);
                
                buffer[offset + i] = sample;
            }

            return samplesRead;
        }

        private void UpdateVoiceActivity(float sample)
        {
            float level = Math.Abs(sample);
            
            // 语音活动检测（简化版）
            voiceActivityLevel = voiceActivityLevel * 0.95f + level * 0.05f;
        }

        private float ApplySpectralNoiseReduction(float sample)
        {
            float absSample = Math.Abs(sample);
            
            // 如果检测到语音活动，减少降噪强度
            float noiseReductionStrength = voiceActivityLevel > 0.05f ? 0.3f : 0.7f;
            
            // 简化的频谱降噪：基于能量的门控
            if (absSample < 0.003f) // 很低的信号
            {
                sample *= (1.0f - noiseReductionStrength);
            }
            else if (absSample < 0.01f) // 低信号
            {
                sample *= (1.0f - noiseReductionStrength * 0.5f);
            }
            
            return sample;
        }

        private float ProtectVoiceDynamics(float sample)
        {
            // 保护语音动态范围，避免过度压缩
            float absSample = Math.Abs(sample);
            
            if (absSample > 0.05f && voiceActivityLevel > 0.03f)
            {
                // 检测到语音，轻微增强
                sample *= 1.1f;
            }
            
            return sample;
        }
    }
} 
using System;
using System.IO;
using System.Text.Json;
using AudioRecorder.Models;

namespace AudioRecorder.Services
{
    /// <summary>
    /// 配置服务
    /// </summary>
    public class ConfigurationService
    {
        private static readonly string ConfigFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        private static ConfigurationService? _instance;
        private static readonly object _lock = new object();

        public UploadSettings UploadSettings { get; private set; }
        public AudioSettings AudioSettings { get; private set; }

        private ConfigurationService()
        {
            LoadConfiguration();
        }

        /// <summary>
        /// 获取配置服务实例（单例模式）
        /// </summary>
        public static ConfigurationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ConfigurationService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var jsonContent = File.ReadAllText(ConfigFilePath);
                    var config = JsonSerializer.Deserialize<AppConfiguration>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (config?.UploadSettings != null)
                    {
                        UploadSettings = config.UploadSettings;
                    }
                    else
                    {
                        UploadSettings = GetDefaultUploadSettings();
                    }

                    if (config?.AudioSettings != null)
                    {
                        AudioSettings = config.AudioSettings;
                    }
                    else
                    {
                        AudioSettings = GetDefaultAudioSettings();
                    }

                    Console.WriteLine($"✅ 配置文件加载成功: {ConfigFilePath}");
                }
                else
                {
                    // 如果配置文件不存在，使用默认配置并创建配置文件
                    UploadSettings = GetDefaultUploadSettings();
                    AudioSettings = GetDefaultAudioSettings();
                    SaveConfiguration();
                    Console.WriteLine($"📝 创建默认配置文件: {ConfigFilePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 加载配置文件失败，使用默认配置: {ex.Message}");
                UploadSettings = GetDefaultUploadSettings();
                AudioSettings = GetDefaultAudioSettings();
            }
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        public void SaveConfiguration()
        {
            try
            {
                var config = new AppConfiguration
                {
                    UploadSettings = UploadSettings,
                    AudioSettings = AudioSettings
                };

                var jsonContent = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                File.WriteAllText(ConfigFilePath, jsonContent);
                Console.WriteLine($"💾 配置文件已保存: {ConfigFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 保存配置文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取默认上传设置
        /// </summary>
        private UploadSettings GetDefaultUploadSettings()
        {
            return new UploadSettings
            {
                ServerUrl = "http://10.10.21.67:38080",
                ApiEndpoint = "/admin-api/asr/file/upload-multiple",
                AuthorizationToken = "01809869aa1b4c98903495da6e00e11c",
                BizType = "asr",
                MergeAudio = true,
                EnableAutoUpload = true,
                UploadTimeout = 30000,
                RetryCount = 3,
                RetryDelay = 5000
            };
        }

        /// <summary>
        /// 获取默认音频设置
        /// </summary>
        private AudioSettings GetDefaultAudioSettings()
        {
            return new AudioSettings
            {
                SampleRate = 16000,
                Channels = 1,
                BitsPerSample = 16,
                BufferDuration = 2
            };
        }

        /// <summary>
        /// 更新上传设置
        /// </summary>
        public void UpdateUploadSettings(UploadSettings newSettings)
        {
            UploadSettings = newSettings ?? throw new ArgumentNullException(nameof(newSettings));
            SaveConfiguration();
        }

        /// <summary>
        /// 更新音频设置
        /// </summary>
        public void UpdateAudioSettings(AudioSettings newSettings)
        {
            AudioSettings = newSettings ?? throw new ArgumentNullException(nameof(newSettings));
            SaveConfiguration();
        }

        /// <summary>
        /// 重新加载配置文件
        /// </summary>
        public void ReloadConfiguration()
        {
            LoadConfiguration();
        }
    }

    /// <summary>
    /// 应用程序配置根对象
    /// </summary>
    public class AppConfiguration
    {
        public UploadSettings? UploadSettings { get; set; }
        public AudioSettings? AudioSettings { get; set; }
    }
}

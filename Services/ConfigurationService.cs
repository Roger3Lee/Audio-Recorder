using System;
using System.IO;
using System.Text.Json;
using AudioRecorder.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace AudioRecorder.Services
{
    /// <summary>
    /// 配置服务 - 负责读取和管理应用程序配置
    /// </summary>
    public class ConfigurationService
    {
        private static ConfigurationService? _instance;
        private static readonly object _lock = new object();
        private readonly ILogger _logger;
        
        public UploadSettings UploadSettings { get; private set; }
        public OAuthSettings OAuthSettings { get; private set; }
        public AudioSettings AudioSettings { get; private set; }
        public RealTimeSaveSettings RealTimeSaveSettings { get; private set; }

        private ConfigurationService()
        {
            _logger = LoggingServiceManager.CreateLogger("ConfigurationService");
            LoadConfiguration();
        }

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

        private void LoadConfiguration()
        {
            try
            {
                _logger.LogInformation("开始加载配置文件");
                
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    var jsonContent = File.ReadAllText(configPath);
                    
                    // 不使用CamelCase，保持原始属性名以匹配现有配置文件
                    var config = JsonSerializer.Deserialize<AppConfig>(jsonContent);

                    if (config != null)
                    {
                        UploadSettings = config.UploadSettings ?? new UploadSettings();
                        OAuthSettings = config.OAuthSettings ?? new OAuthSettings();
                        AudioSettings = config.AudioSettings ?? new AudioSettings();
                        RealTimeSaveSettings = config.RealTimeSaveSettings ?? new RealTimeSaveSettings();
                        
                        _logger.LogInformation("配置文件加载成功: {ConfigPath}", configPath);
                        _logger.LogInformation("OAuth认证状态: {AuthStatus}", OAuthSettings.EnableAuthentication ? "已启用" : "已禁用");
                        
                        // 记录OAuth提供商配置状态
                        var githubConfigured = !string.IsNullOrEmpty(OAuthSettings.GitHub.ClientId) && 
                                            OAuthSettings.GitHub.ClientId != "your-github-client-id";
                        var googleConfigured = !string.IsNullOrEmpty(OAuthSettings.Google.ClientId) && 
                                             OAuthSettings.Google.ClientId != "your-google-client-id";
                        
                        _logger.LogInformation("GitHub OAuth配置: {GitHubStatus}", githubConfigured ? "已配置" : "未配置");
                        _logger.LogInformation("Google OAuth配置: {GoogleStatus}", googleConfigured ? "已配置" : "未配置");
                    }
                    else
                    {
                        UploadSettings = new UploadSettings();
                        OAuthSettings = new OAuthSettings();
                        AudioSettings = new AudioSettings();
                        RealTimeSaveSettings = new RealTimeSaveSettings();
                        _logger.LogWarning("配置文件解析失败，使用默认配置");
                    }
                }
                else
                {
                    UploadSettings = new UploadSettings();
                    OAuthSettings = new OAuthSettings();
                    AudioSettings = new AudioSettings();
                    RealTimeSaveSettings = new RealTimeSaveSettings();
                    _logger.LogWarning("配置文件不存在: {ConfigPath}，使用默认配置", configPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"加载配置文件失败: {ex.Message}", "ConfigurationService", ex);
                UploadSettings = new UploadSettings();
                OAuthSettings = new OAuthSettings();
                AudioSettings = new AudioSettings();
                RealTimeSaveSettings = new RealTimeSaveSettings();
            }
        }

        /// <summary>
        /// 重新加载配置
        /// </summary>
        public void ReloadConfiguration()
        {
            _logger.LogInformation("重新加载配置文件", "ConfigurationService");
            LoadConfiguration();
        }

        /// <summary>
        /// 检查OAuth认证是否启用
        /// </summary>
        public bool IsOAuthEnabled()
        {
            return OAuthSettings.EnableAuthentication;
        }

        /// <summary>
        /// 启用OAuth认证
        /// </summary>
        public async Task EnableOAuthAsync()
        {
            try
            {
                _logger.LogInformation("正在启用OAuth认证", "ConfigurationService");
                
                OAuthSettings.EnableAuthentication = true;
                await SaveConfigurationAsync();
                
                _logger.LogInformation("OAuth认证已启用", "ConfigurationService");
            }
            catch (Exception ex)
            {
                _logger.LogError($"启用OAuth认证失败: {ex.Message}", "ConfigurationService", ex);
                throw;
            }
        }

        /// <summary>
        /// 禁用OAuth认证
        /// </summary>
        public async Task DisableOAuthAsync()
        {
            try
            {
                _logger.LogInformation("正在禁用OAuth认证", "ConfigurationService");
                
                OAuthSettings.EnableAuthentication = false;
                await SaveConfigurationAsync();
                
                _logger.LogInformation("OAuth认证已禁用", "ConfigurationService");
            }
            catch (Exception ex)
            {
                _logger.LogError($"禁用OAuth认证失败: {ex.Message}", "ConfigurationService", ex);
                throw;
            }
        }

        /// <summary>
        /// 切换OAuth认证状态
        /// </summary>
        public async Task ToggleOAuthAsync()
        {
            try
            {
                var oldStatus = OAuthSettings.EnableAuthentication;
                OAuthSettings.EnableAuthentication = !OAuthSettings.EnableAuthentication;
                var newStatus = OAuthSettings.EnableAuthentication;
                
                _logger.LogInformation($"OAuth认证状态从 {(oldStatus ? "启用" : "禁用")} 切换为 {(newStatus ? "启用" : "禁用")}", "ConfigurationService");
                
                await SaveConfigurationAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"切换OAuth认证状态失败: {ex.Message}", "ConfigurationService", ex);
                throw;
            }
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        private async Task SaveConfigurationAsync()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                var config = new AppConfig
                {
                    UploadSettings = UploadSettings,
                    OAuthSettings = OAuthSettings,
                    AudioSettings = AudioSettings,
                    RealTimeSaveSettings = RealTimeSaveSettings
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var jsonContent = JsonSerializer.Serialize(config, options);
                await File.WriteAllTextAsync(configPath, jsonContent);
                
                _logger.LogInformation($"配置文件已保存: {configPath}", "ConfigurationService");
            }
            catch (Exception ex)
            {
                _logger.LogError($"保存配置文件失败: {ex.Message}", "ConfigurationService", ex);
                throw;
            }
        }

        /// <summary>
        /// 获取GitHub OAuth配置
        /// </summary>
        public OAuthConfig GetGitHubOAuthConfig()
        {
            try
            {
                // 如果OAuth认证被禁用，返回空配置
                if (!IsOAuthEnabled())
                {
                    _logger.LogDebug("OAuth认证已禁用，返回空的GitHub配置", "ConfigurationService");
                    return new OAuthConfig
                    {
                        ProviderName = "GitHub",
                        ClientId = string.Empty,
                        ClientSecret = string.Empty
                    };
                }

                var settings = OAuthSettings.GitHub;
                var config = new OAuthConfig
                {
                    ClientId = settings.ClientId,
                    ClientSecret = settings.ClientSecret,
                    RedirectUri = settings.RedirectUri,
                    AuthorizationEndpoint = "https://github.com/login/oauth/authorize",
                    TokenEndpoint = "https://github.com/login/oauth/access_token",
                    Scopes = settings.Scopes,
                    ProviderName = "GitHub",
                    EnablePkce = false,
                    ResponseType = "code",
                    AccessType = "offline",
                    Prompt = "consent"
                };

                _logger.LogDebug($"获取GitHub OAuth配置: ClientId={(!string.IsNullOrEmpty(config.ClientId) ? "已设置" : "未设置")}, RedirectUri={config.RedirectUri}", "ConfigurationService");
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError($"获取GitHub OAuth配置失败: {ex.Message}", "ConfigurationService", ex);
                throw;
            }
        }

        /// <summary>
        /// 获取Google OAuth配置
        /// </summary>
        public OAuthConfig GetGoogleOAuthConfig()
        {
            try
            {
                // 如果OAuth认证被禁用，返回空配置
                if (!IsOAuthEnabled())
                {
                    _logger.LogDebug("OAuth认证已禁用，返回空的Google配置");
                    return new OAuthConfig
                    {
                        ProviderName = "Google",
                        ClientId = string.Empty,
                        ClientSecret = string.Empty
                    };
                }

                var settings = OAuthSettings.Google;
                var config = new OAuthConfig
                {
                    ClientId = settings.ClientId,
                    ClientSecret = settings.ClientSecret,
                    RedirectUri = settings.RedirectUri,
                    AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
                    TokenEndpoint = "https://oauth2.googleapis.com/token",
                    Scopes = settings.Scopes,
                    ProviderName = "Google",
                    EnablePkce = true,
                    ResponseType = "code",
                    AccessType = "offline",
                    Prompt = "consent"
                };

                _logger.LogDebug("获取Google OAuth配置: ClientId={ClientIdStatus}, RedirectUri={RedirectUri}", 
                    !string.IsNullOrEmpty(config.ClientId) ? "已设置" : "未设置", config.RedirectUri);
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取Google OAuth配置失败");
                throw;
            }
        }

        /// <summary>
        /// 获取认证状态摘要
        /// </summary>
        public string GetAuthenticationStatusSummary()
        {
            try
            {
                var status = IsOAuthEnabled() ? "已启用" : "已禁用";
                var providers = IsOAuthEnabled() ? 
                    string.Join(", ", OAuthSettings.GetAvailableProviders()) : 
                    "无";
                
                var summary = $"OAuth认证: {status}, 可用提供商: {providers}";
                _logger.LogDebug("获取认证状态摘要: {Summary}", summary);
                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取认证状态摘要失败");
                return "获取认证状态失败";
            }
        }

        /// <summary>
        /// 获取窗口位置
        /// </summary>
        public WindowPosition? GetWindowPosition()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    var jsonContent = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<AppConfig>(jsonContent);
                    
                    if (config?.WindowPosition != null)
                    {
                        _logger.LogDebug("获取窗口位置: ({X}, {Y})", config.WindowPosition.X, config.WindowPosition.Y);
                        return config.WindowPosition;
                    }
                }
                
                _logger.LogDebug("未找到保存的窗口位置");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取窗口位置失败");
                return null;
            }
        }

        /// <summary>
        /// 保存窗口位置
        /// </summary>
        public void SaveWindowPosition(WindowPosition position)
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                AppConfig config;
                
                if (File.Exists(configPath))
                {
                    var jsonContent = File.ReadAllText(configPath);
                    config = JsonSerializer.Deserialize<AppConfig>(jsonContent) ?? new AppConfig();
                }
                else
                {
                    config = new AppConfig();
                }

                // 更新窗口位置
                config.WindowPosition = position;
                
                // 保存到文件
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                
                var jsonString = JsonSerializer.Serialize(config, options);
                File.WriteAllText(configPath, jsonString);
                
                _logger.LogInformation($"窗口位置已保存: ({position.X}, {position.Y})", "ConfigurationService");
            }
            catch (Exception ex)
            {
                _logger.LogError($"保存窗口位置失败: {ex.Message}", "ConfigurationService", ex);
            }
        }
    }

    /// <summary>
    /// 应用程序配置
    /// </summary>
    public class AppConfig
    {
        public UploadSettings? UploadSettings { get; set; }
        public OAuthSettings? OAuthSettings { get; set; }
        public AudioSettings? AudioSettings { get; set; }
        public RealTimeSaveSettings? RealTimeSaveSettings { get; set; }
        public WindowPosition? WindowPosition { get; set; }
    }

    /// <summary>
    /// 窗口位置配置
    /// </summary>
    public class WindowPosition
    {
        public double X { get; set; }
        public double Y { get; set; }
        public DateTime LastSaved { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// OAuth设置
    /// </summary>
    public class OAuthSettings
    {
        public bool EnableAuthentication { get; set; } = true;
        public GitHubOAuthSettings GitHub { get; set; } = new();
        public GoogleOAuthSettings Google { get; set; } = new();

        /// <summary>
        /// 获取可用的OAuth提供商列表
        /// </summary>
        public List<string> GetAvailableProviders()
        {
            var providers = new List<string>();
            
            if (!string.IsNullOrEmpty(GitHub.ClientId) && GitHub.ClientId != "your-github-client-id")
            {
                providers.Add("GitHub");
            }
            
            if (!string.IsNullOrEmpty(Google.ClientId) && Google.ClientId != "your-google-client-id")
            {
                providers.Add("Google");
            }
            
            return providers;
        }
    }

    /// <summary>
    /// GitHub OAuth设置
    /// </summary>
    public class GitHubOAuthSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = "http://localhost:8081/auth/callback";
        public string[] Scopes { get; set; } = new[] { "user", "user:email" };
    }

    /// <summary>
    /// Google OAuth设置
    /// </summary>
    public class GoogleOAuthSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = "http://localhost:8081/auth/callback";
        public string[] Scopes { get; set; } = new[] { "openid", "profile", "email" };
    }
}

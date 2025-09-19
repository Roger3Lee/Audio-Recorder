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

        /// <summary>
        /// 获取用户配置文件路径
        /// </summary>
        private string GetUserConfigPath()
        {
            try
            {
                // 优先使用用户AppData目录
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var userConfigDir = Path.Combine(appDataPath, "AudioRecorder");
                var userConfigPath = Path.Combine(userConfigDir, "userconfig.json");
                
                // 确保目录存在
                if (!Directory.Exists(userConfigDir))
                {
                    Directory.CreateDirectory(userConfigDir);
                }
                
                return userConfigPath;
            }
            catch
            {
                try
                {
                    // 备用方案：使用用户文档目录
                    var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var userConfigDir = Path.Combine(documentsPath, "AudioRecorder");
                    var userConfigPath = Path.Combine(userConfigDir, "userconfig.json");
                    
                    if (!Directory.Exists(userConfigDir))
                    {
                        Directory.CreateDirectory(userConfigDir);
                    }
                    
                    return userConfigPath;
                }
                catch
                {
                    // 最后备用方案：使用当前目录
                    return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "userconfig.json");
                }
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
                        var genericOAuthConfigured = !string.IsNullOrEmpty(OAuthSettings.OauthServer.AuthorizeUrl) && 
                                                   !string.IsNullOrEmpty(OAuthSettings.OauthServer.TokenUrl) &&
                                                   !string.IsNullOrEmpty(OAuthSettings.OauthServer.ClientId) &&
                                                   OAuthSettings.OauthServer.ClientId != "audio_recorder";
                        var githubConfigured = !string.IsNullOrEmpty(OAuthSettings.GitHub.ClientId) && 
                                            OAuthSettings.GitHub.ClientId != "your-github-client-id";
                        var googleConfigured = !string.IsNullOrEmpty(OAuthSettings.Google.ClientId) && 
                                             OAuthSettings.Google.ClientId != "your-google-client-id";
                        
                        _logger.LogInformation("通用OAuth服务器配置: {GenericOAuthStatus}", genericOAuthConfigured ? "已配置" : "未配置");
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
                _logger.LogError(ex, "加载配置文件失败");
                
                // 使用默认配置
                UploadSettings = new UploadSettings();
                OAuthSettings = new OAuthSettings();
                AudioSettings = new AudioSettings();
                RealTimeSaveSettings = new RealTimeSaveSettings();
            }
        }

        /// <summary>
        /// 检查OAuth是否启用
        /// </summary>
        public bool IsOAuthEnabled()
        {
            return OAuthSettings?.EnableAuthentication == true;
        }

        /// <summary>
        /// 获取OAuth设置
        /// </summary>
        public OAuthSettings GetOAuthSettings()
        {
            return OAuthSettings ?? new OAuthSettings();
        }

        /// <summary>
        /// 获取上传设置
        /// </summary>
        public UploadSettings GetUploadSettings()
        {
            return UploadSettings ?? new UploadSettings();
        }

        /// <summary>
        /// 获取音频设置
        /// </summary>
        public AudioSettings GetAudioSettings()
        {
            return AudioSettings ?? new AudioSettings();
        }

        /// <summary>
        /// 获取实时保存设置
        /// </summary>
        public RealTimeSaveSettings GetRealTimeSaveSettings()
        {
            return RealTimeSaveSettings ?? new RealTimeSaveSettings();
        }

        /// <summary>
        /// 更新OAuth设置
        /// </summary>
        public async Task UpdateOAuthSettingsAsync(OAuthSettings newSettings)
        {
            try
            {
                OAuthSettings = newSettings ?? throw new ArgumentNullException(nameof(newSettings));
                _logger.LogInformation("OAuth设置已更新");
                
                await SaveConfigurationAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"更新OAuth设置失败: {ex.Message}", "ConfigurationService", ex);
                throw;
            }
        }

        /// <summary>
        /// 更新上传设置
        /// </summary>
        public async Task UpdateUploadSettingsAsync(UploadSettings newSettings)
        {
            try
            {
                UploadSettings = newSettings ?? throw new ArgumentNullException(nameof(newSettings));
                _logger.LogInformation("上传设置已更新");
                
                await SaveConfigurationAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"更新上传设置失败: {ex.Message}", "ConfigurationService", ex);
                throw;
            }
        }

        /// <summary>
        /// 更新音频设置
        /// </summary>
        public async Task UpdateAudioSettingsAsync(AudioSettings newSettings)
        {
            try
            {
                AudioSettings = newSettings ?? throw new ArgumentNullException(nameof(newSettings));
                _logger.LogInformation("音频设置已更新");
                
                await SaveConfigurationAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"更新音频设置失败: {ex.Message}", "ConfigurationService", ex);
                throw;
            }
        }

        /// <summary>
        /// 更新实时保存设置
        /// </summary>
        public async Task UpdateRealTimeSaveSettingsAsync(RealTimeSaveSettings newSettings)
        {
            try
            {
                RealTimeSaveSettings = newSettings ?? throw new ArgumentNullException(nameof(newSettings));
                _logger.LogInformation("实时保存设置已更新");
                
                await SaveConfigurationAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"更新实时保存设置失败: {ex.Message}", "ConfigurationService", ex);
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
        /// 启用OAuth认证
        /// </summary>
        public async Task EnableOAuthAsync()
        {
            try
            {
                OAuthSettings.EnableAuthentication = true;
                _logger.LogInformation("OAuth认证已启用");
                await SaveConfigurationAsync();
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
                OAuthSettings.EnableAuthentication = false;
                _logger.LogInformation("OAuth认证已禁用");
                await SaveConfigurationAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"禁用OAuth认证失败: {ex.Message}", "ConfigurationService", ex);
                throw;
            }
        }

        /// <summary>
        /// 重新加载配置
        /// </summary>
        public void ReloadConfiguration()
        {
            try
            {
                _logger.LogInformation("重新加载配置文件");
                LoadConfiguration();
            }
            catch (Exception ex)
            {
                _logger.LogError($"重新加载配置失败: {ex.Message}", "ConfigurationService", ex);
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
        /// 获取通用OAuth服务器配置
        /// </summary>
        public Models.OAuthConfig GetGenericOAuthConfig()
        {
            if (OAuthSettings?.OauthServer != null && 
                !string.IsNullOrEmpty(OAuthSettings.OauthServer.AuthorizeUrl) &&
                !string.IsNullOrEmpty(OAuthSettings.OauthServer.TokenUrl))
            {
                return new Models.OAuthConfig
                {
                    ClientId = OAuthSettings.OauthServer.ClientId,
                    ClientSecret = OAuthSettings.OauthServer.ClientSecret,
                    RedirectUri = OAuthSettings.OauthServer.RedirectUri,
                    AuthorizationEndpoint = OAuthSettings.OauthServer.AuthorizeUrl,
                    TokenEndpoint = OAuthSettings.OauthServer.TokenUrl,
                    UserInfoEndpoint = OAuthSettings.OauthServer.UserInfoUrl,
                    LogoutEndpoint = OAuthSettings.OauthServer.LogoutUrl,
                    Scopes = OAuthSettings.OauthServer.Scopes.ToArray(),
                    ProviderName = "GenericOAuth",
                    EnablePkce = false,
                    ResponseType = "code",
                    AccessType = "offline",
                    Prompt = "consent"
                };
            }
            return Models.GenericOAuthConfig.Default;
        }

        /// <summary>
        /// 获取GitHub OAuth配置
        /// </summary>
        public Models.OAuthConfig GetGitHubOAuthConfig()
        {
            if (OAuthSettings?.GitHub != null)
            {
                return new Models.OAuthConfig
                {
                    ClientId = OAuthSettings.GitHub.ClientId,
                    ClientSecret = OAuthSettings.GitHub.ClientSecret,
                    RedirectUri = OAuthSettings.GitHub.RedirectUri,
                    AuthorizationEndpoint = "https://github.com/login/oauth/authorize",
                    TokenEndpoint = "https://github.com/login/oauth/access_token",
                    Scopes = OAuthSettings.GitHub.Scopes.ToArray(),
                    ProviderName = "GitHub",
                    EnablePkce = false,
                    ResponseType = "code",
                    AccessType = "offline",
                    Prompt = "consent"
                };
            }
            return Models.GitHubOAuthConfig.Default;
        }

        /// <summary>
        /// 获取Google OAuth配置
        /// </summary>
        public Models.OAuthConfig GetGoogleOAuthConfig()
        {
            if (OAuthSettings?.Google != null)
            {
                return new Models.OAuthConfig
                {
                    ClientId = OAuthSettings.Google.ClientId,
                    ClientSecret = OAuthSettings.Google.ClientSecret,
                    RedirectUri = OAuthSettings.Google.RedirectUri,
                    AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
                    TokenEndpoint = "https://oauth2.googleapis.com/token",
                    Scopes = OAuthSettings.Google.Scopes.ToArray(),
                    ProviderName = "Google",
                    EnablePkce = true,
                    ResponseType = "code",
                    AccessType = "offline",
                    Prompt = "consent"
                };
            }
            return Models.GoogleOAuthConfig.Default;
        }

        /// <summary>
        /// 检查OAuth配置是否完整
        /// </summary>
        public bool IsOAuthConfigComplete(string provider)
        {
            try
            {
                switch (provider.ToLower())
                {
                    case "genericoauth":
                        var genericConfig = GetGenericOAuthConfig();
                        return !string.IsNullOrEmpty(genericConfig.ClientId) && 
                               !string.IsNullOrEmpty(genericConfig.ClientSecret) &&
                               !string.IsNullOrEmpty(genericConfig.AuthorizationEndpoint) &&
                               !string.IsNullOrEmpty(genericConfig.TokenEndpoint) &&
                               genericConfig.ClientId != "audio_recorder";
                    
                    case "github":
                        var githubConfig = GetGitHubOAuthConfig();
                        return !string.IsNullOrEmpty(githubConfig.ClientId) && 
                               !string.IsNullOrEmpty(githubConfig.ClientSecret) &&
                               githubConfig.ClientId != "your-github-client-id" &&
                               githubConfig.ClientSecret != "your-github-client-secret";
                    
                    case "google":
                        var googleConfig = GetGoogleOAuthConfig();
                        return !string.IsNullOrEmpty(googleConfig.ClientId) && 
                               !string.IsNullOrEmpty(googleConfig.ClientSecret) &&
                               googleConfig.ClientId != "your-google-client-id" &&
                               googleConfig.ClientSecret != "your-google-client-secret";
                    
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查OAuth配置完整性失败");
                return false;
            }
        }

        /// <summary>
        /// 获取可用的OAuth提供商列表
        /// </summary>
        public List<string> GetAvailableOAuthProviders()
        {
            var providers = new List<string>();
            
            try
            {
                if (IsOAuthConfigComplete("genericoauth"))
                {
                    providers.Add("GenericOAuth");
                }
                
                if (IsOAuthConfigComplete("github"))
                {
                    providers.Add("GitHub");
                }
                
                if (IsOAuthConfigComplete("google"))
                {
                    providers.Add("Google");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取可用OAuth提供商失败");
            }
            
            return providers;
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
                var userConfigPath = GetUserConfigPath();
                if (File.Exists(userConfigPath))
                {
                    var jsonContent = File.ReadAllText(userConfigPath);
                    var userConfig = JsonSerializer.Deserialize<UserConfig>(jsonContent);
                    
                    if (userConfig?.WindowPosition != null)
                    {
                        _logger.LogDebug("获取窗口位置: ({X}, {Y})", userConfig.WindowPosition.X, userConfig.WindowPosition.Y);
                        return userConfig.WindowPosition;
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
                var userConfigPath = GetUserConfigPath();
                UserConfig userConfig;
                
                if (File.Exists(userConfigPath))
                {
                    var jsonContent = File.ReadAllText(userConfigPath);
                    userConfig = JsonSerializer.Deserialize<UserConfig>(jsonContent) ?? new UserConfig();
                }
                else
                {
                    userConfig = new UserConfig();
                }

                // 更新窗口位置
                userConfig.WindowPosition = position;
                
                // 保存到用户配置文件
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                
                var jsonString = JsonSerializer.Serialize(userConfig, options);
                File.WriteAllText(userConfigPath, jsonString);
                
                _logger.LogInformation($"窗口位置已保存到用户配置: ({position.X}, {position.Y})", "ConfigurationService");
            }
            catch (Exception ex)
            {
                _logger.LogError($"保存窗口位置失败: {ex.Message}", "ConfigurationService", ex);
            }
        }
    }

    /// <summary>
    /// 应用程序配置（系统配置，只读）
    /// </summary>
    public class AppConfig
    {
        public UploadSettings? UploadSettings { get; set; }
        public OAuthSettings? OAuthSettings { get; set; }
        public AudioSettings? AudioSettings { get; set; }
        public RealTimeSaveSettings? RealTimeSaveSettings { get; set; }
    }

    /// <summary>
    /// 用户配置（可写，保存在用户目录）
    /// </summary>
    public class UserConfig
    {
        public WindowPosition? WindowPosition { get; set; }
        public Dictionary<string, object>? CustomSettings { get; set; }
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
        public bool EnableAuthentication { get; set; } = false;
        public OAuthServerConfig OauthServer { get; set; } = new OAuthServerConfig();
        public GitHubOAuthConfig GitHub { get; set; } = new GitHubOAuthConfig();
        public GoogleOAuthConfig Google { get; set; } = new GoogleOAuthConfig();

        /// <summary>
        /// 获取可用的OAuth提供商
        /// </summary>
        public List<string> GetAvailableProviders()
        {
            var providers = new List<string>();
            
            // 检查通用OAuth服务器配置
            if (OauthServer != null && 
                !string.IsNullOrEmpty(OauthServer.AuthorizeUrl) && 
                !string.IsNullOrEmpty(OauthServer.TokenUrl) &&
                !string.IsNullOrEmpty(OauthServer.ClientId) && OauthServer.ClientId != "audio_recorder")
            {
                providers.Add("GenericOAuth");
            }
            
            if (GitHub != null && !string.IsNullOrEmpty(GitHub.ClientId) && GitHub.ClientId != "your-github-client-id")
            {
                providers.Add("GitHub");
            }
            
            if (Google != null && !string.IsNullOrEmpty(Google.ClientId) && Google.ClientId != "your-google-client-id")
            {
                providers.Add("Google");
            }
            
            return providers;
        }
    }

    /// <summary>
    /// 通用OAuth服务器配置
    /// </summary>
    public class OAuthServerConfig
    {
        public string AuthorizeUrl { get; set; } = "";
        public string TokenUrl { get; set; } = "";

        public string UserInfoUrl { get; set; } = "";
        public string LogoutUrl { get; set; } = "";
        public string ClientId { get; set; } = "audio_recorder";
        public string ClientSecret { get; set; } = "Kj8mN2pQ9vX5wR7sT3uY1zA4bC6dE8fG0hI";
        public string RedirectUri { get; set; } = "http://localhost:8081/auth/callback"; // 默认回调地址（运行时使用动态端口）
        public List<string> Scopes { get; set; } = new List<string> { "user", "user:email" };
    }

    /// <summary>
    /// GitHub OAuth配置
    /// </summary>
    public class GitHubOAuthConfig
    {
        public string ClientId { get; set; } = "your-github-client-id";
        public string ClientSecret { get; set; } = "your-github-client-secret";
        public string RedirectUri { get; set; } = "http://localhost:8081/auth/callback"; // 默认回调地址（运行时使用动态端口）
        public List<string> Scopes { get; set; } = new List<string> { "user", "user:email" };
    }

    /// <summary>
    /// Google OAuth配置
    /// </summary>
    public class GoogleOAuthConfig
    {
        public string ClientId { get; set; } = "your-google-client-id";
        public string ClientSecret { get; set; } = "your-google-client-secret";
        public string RedirectUri { get; set; } = "http://localhost:8081/auth/callback"; // 默认回调地址（运行时使用动态端口）
        public List<string> Scopes { get; set; } = new List<string> { "openid", "profile", "email" };
    }
}

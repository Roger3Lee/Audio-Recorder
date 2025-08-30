using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AudioRecorder.Models;
using AudioRecorder.Services;
using System.Linq;

namespace AudioRecorder.Services
{
    /// <summary>
    /// OAuth登录服务 - 管理多个OAuth提供商的登录
    /// </summary>
    public class OAuthLoginService : IDisposable
    {
        private readonly Dictionary<string, AuthorizationManager> _authManagers;
        private readonly TokenManager _tokenManager;
        private readonly ConfigurationService _configService;
        private readonly LoggingService _logger;

        public event EventHandler<TokenInfo>? LoginCompleted;
        public event EventHandler<string>? LoginFailed;
        public event EventHandler<TokenInfo>? LoginStateRestored;
        public event EventHandler<bool>? AuthenticationStatusChanged;

        public OAuthLoginService()
        {
            _authManagers = new Dictionary<string, AuthorizationManager>();
            _tokenManager = new TokenManager();
            _configService = ConfigurationService.Instance;
            _logger = LoggingService.Instance;

            _logger.LogInformation("OAuth登录服务初始化开始", "OAuthLoginService");
            InitializeOAuthProviders();
            SubscribeToEvents();
            _logger.LogInformation("OAuth登录服务初始化完成", "OAuthLoginService");
        }

        /// <summary>
        /// 初始化OAuth提供商
        /// </summary>
        private void InitializeOAuthProviders()
        {
            try
            {
                // 检查OAuth认证是否启用
                if (!_configService.IsOAuthEnabled())
                {
                    _logger.LogWarning("OAuth认证已禁用，跳过提供商初始化", "OAuthLoginService");
                    return;
                }

                _logger.LogInformation("开始初始化OAuth提供商", "OAuthLoginService");

                // 初始化GitHub OAuth
                var githubConfig = _configService.GetGitHubOAuthConfig();
                if (!string.IsNullOrEmpty(githubConfig.ClientId) && githubConfig.ClientId != "your-github-client-id")
                {
                    var githubAuthManager = new AuthorizationManager(githubConfig);
                    _authManagers["GitHub"] = githubAuthManager;
                    _tokenManager.AddAuthorizationManager("GitHub", githubAuthManager);
                    _logger.LogInformation("GitHub OAuth提供商初始化成功", "OAuthLoginService");
                }
                else
                {
                    _logger.LogWarning("GitHub OAuth配置未设置，跳过初始化", "OAuthLoginService");
                }

                // 初始化Google OAuth
                var googleConfig = _configService.GetGoogleOAuthConfig();
                if (!string.IsNullOrEmpty(googleConfig.ClientId) && googleConfig.ClientId != "your-google-client-id")
                {
                    var googleAuthManager = new AuthorizationManager(googleConfig);
                    _authManagers["Google"] = googleAuthManager;
                    _tokenManager.AddAuthorizationManager("Google", googleAuthManager);
                    _logger.LogInformation("Google OAuth提供商初始化成功", "OAuthLoginService");
                }
                else
                {
                    _logger.LogWarning("Google OAuth配置未设置，跳过初始化", "OAuthLoginService");
                }

                _logger.LogInformation($"OAuth提供商初始化完成，共 {_authManagers.Count} 个提供商", "OAuthLoginService");
            }
            catch (Exception ex)
            {
                _logger.LogError($"初始化OAuth提供商失败: {ex.Message}", "OAuthLoginService", ex);
            }
        }

        /// <summary>
        /// 订阅事件
        /// </summary>
        private void SubscribeToEvents()
        {
            foreach (var kvp in _authManagers)
            {
                var provider = kvp.Key;
                var authManager = kvp.Value;

                authManager.AuthorizationCompleted += (sender, tokenInfo) =>
                {
                    _logger.LogInformation($"{provider} 授权完成，用户: {tokenInfo.UserName}", "OAuthLoginService");
                    LoginCompleted?.Invoke(this, tokenInfo);
                };

                authManager.AuthorizationFailed += (sender, error) =>
                {
                    _logger.LogError($"{provider} 授权失败: {error}", "OAuthLoginService");
                    LoginFailed?.Invoke(this, $"{provider}: {error}");
                };

                authManager.TokenRefreshed += (sender, tokenInfo) =>
                {
                    _logger.LogInformation($"{provider} 令牌已刷新，用户: {tokenInfo.UserName}", "OAuthLoginService");
                };
            }

            _tokenManager.LoginStateRestored += (sender, tokenInfo) =>
            {
                _logger.LogInformation($"登录状态已恢复: {tokenInfo.Provider} - {tokenInfo.UserName}", "OAuthLoginService");
                LoginStateRestored?.Invoke(this, tokenInfo);
            };
        }

        /// <summary>
        /// 检查OAuth认证是否启用
        /// </summary>
        public bool IsAuthenticationEnabled()
        {
            var enabled = _configService.IsOAuthEnabled();
            _logger.LogDebug($"OAuth认证状态检查: {(enabled ? "已启用" : "已禁用")}", "OAuthLoginService");
            return enabled;
        }

        /// <summary>
        /// 启用OAuth认证
        /// </summary>
        public async Task EnableAuthenticationAsync()
        {
            try
            {
                _logger.LogInformation("正在启用OAuth认证", "OAuthLoginService");
                
                await _configService.EnableOAuthAsync();
                InitializeOAuthProviders();
                AuthenticationStatusChanged?.Invoke(this, true);
                
                _logger.LogInformation("OAuth认证已启用", "OAuthLoginService");
            }
            catch (Exception ex)
            {
                _logger.LogError($"启用OAuth认证失败: {ex.Message}", "OAuthLoginService", ex);
                throw;
            }
        }

        /// <summary>
        /// 禁用OAuth认证
        /// </summary>
        public async Task DisableAuthenticationAsync()
        {
            try
            {
                _logger.LogInformation("正在禁用OAuth认证", "OAuthLoginService");
                
                await _configService.DisableOAuthAsync();
                
                // 清理所有授权管理器
                foreach (var authManager in _authManagers.Values)
                {
                    authManager?.Dispose();
                }
                _authManagers.Clear();
                
                // 清理令牌
                await _tokenManager.ClearAllTokensAsync();
                
                AuthenticationStatusChanged?.Invoke(this, false);
                
                _logger.LogInformation("OAuth认证已禁用，所有资源已清理", "OAuthLoginService");
            }
            catch (Exception ex)
            {
                _logger.LogError($"禁用OAuth认证失败: {ex.Message}", "OAuthLoginService", ex);
                throw;
            }
        }

        /// <summary>
        /// 切换OAuth认证状态
        /// </summary>
        public async Task ToggleAuthenticationAsync()
        {
            try
            {
                var currentStatus = IsAuthenticationEnabled();
                _logger.LogInformation($"正在切换OAuth认证状态，当前状态: {(currentStatus ? "启用" : "禁用")}", "OAuthLoginService");
                
                if (currentStatus)
                {
                    await DisableAuthenticationAsync();
                }
                else
                {
                    await EnableAuthenticationAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"切换OAuth认证状态失败: {ex.Message}", "OAuthLoginService", ex);
                throw;
            }
        }

        /// <summary>
        /// 开始GitHub登录
        /// </summary>
        public async Task<bool> StartGitHubLoginAsync()
        {
            try
            {
                if (!IsAuthenticationEnabled())
                {
                    _logger.LogWarning("OAuth认证已禁用，无法启动GitHub登录", "OAuthLoginService");
                    LoginFailed?.Invoke(this, "OAuth认证已禁用");
                    return false;
                }

                if (_authManagers.ContainsKey("GitHub"))
                {
                    _logger.LogInformation("开始GitHub OAuth登录流程", "OAuthLoginService");
                    var result = await _authManagers["GitHub"].StartAuthorizationAsync();
                    
                    if (result)
                    {
                        _logger.LogInformation("GitHub OAuth登录流程启动成功", "OAuthLoginService");
                    }
                    else
                    {
                        _logger.LogError("GitHub OAuth登录流程启动失败", "OAuthLoginService");
                    }
                    
                    return result;
                }
                else
                {
                    _logger.LogError("GitHub OAuth提供商未初始化", "OAuthLoginService");
                    LoginFailed?.Invoke(this, "GitHub OAuth提供商未初始化");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"启动GitHub登录失败: {ex.Message}", "OAuthLoginService", ex);
                LoginFailed?.Invoke(this, $"启动GitHub登录失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 开始Google登录
        /// </summary>
        public async Task<bool> StartGoogleLoginAsync()
        {
            try
            {
                if (!IsAuthenticationEnabled())
                {
                    _logger.LogWarning("OAuth认证已禁用，无法启动Google登录", "OAuthLoginService");
                    LoginFailed?.Invoke(this, "OAuth认证已禁用");
                    return false;
                }

                if (_authManagers.ContainsKey("Google"))
                {
                    _logger.LogInformation("开始Google OAuth登录流程", "OAuthLoginService");
                    var result = await _authManagers["Google"].StartAuthorizationAsync();
                    
                    if (result)
                    {
                        _logger.LogInformation("Google OAuth登录流程启动成功", "OAuthLoginService");
                    }
                    else
                    {
                        _logger.LogError("Google OAuth登录流程启动失败", "OAuthLoginService");
                    }
                    
                    return result;
                }
                else
                {
                    _logger.LogError("Google OAuth提供商未初始化", "OAuthLoginService");
                    LoginFailed?.Invoke(this, "Google OAuth提供商未初始化");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"启动Google登录失败: {ex.Message}", "OAuthLoginService", ex);
                LoginFailed?.Invoke(this, $"启动Google登录失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 开始指定提供商的登录
        /// </summary>
        public async Task<bool> StartLoginAsync(string provider)
        {
            try
            {
                if (!IsAuthenticationEnabled())
                {
                    _logger.LogWarning($"OAuth认证已禁用，无法启动{provider}登录", "OAuthLoginService");
                    LoginFailed?.Invoke(this, "OAuth认证已禁用");
                    return false;
                }

                if (_authManagers.ContainsKey(provider))
                {
                    _logger.LogInformation($"开始{provider} OAuth登录流程", "OAuthLoginService");
                    var result = await _authManagers[provider].StartAuthorizationAsync();
                    
                    if (result)
                    {
                        _logger.LogInformation($"{provider} OAuth登录流程启动成功", "OAuthLoginService");
                    }
                    else
                    {
                        _logger.LogError($"{provider} OAuth登录流程启动失败", "OAuthLoginService");
                    }
                    
                    return result;
                }
                else
                {
                    _logger.LogError($"{provider} OAuth提供商未初始化", "OAuthLoginService");
                    LoginFailed?.Invoke(this, $"{provider} OAuth提供商未初始化");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"启动{provider}登录失败: {ex.Message}", "OAuthLoginService", ex);
                LoginFailed?.Invoke(this, $"启动{provider}登录失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 恢复登录状态
        /// </summary>
        public async Task<bool> RestoreLoginStateAsync()
        {
            try
            {
                if (!IsAuthenticationEnabled())
                {
                    _logger.LogWarning("OAuth认证已禁用，跳过登录状态恢复", "OAuthLoginService");
                    return false;
                }

                _logger.LogInformation("开始恢复登录状态", "OAuthLoginService");
                var result = await _tokenManager.RestoreLoginStateAsync();
                
                if (result)
                {
                    _logger.LogInformation("登录状态恢复成功", "OAuthLoginService");
                }
                else
                {
                    _logger.LogInformation("无有效登录状态需要恢复", "OAuthLoginService");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"恢复登录状态失败: {ex.Message}", "OAuthLoginService", ex);
                return false;
            }
        }

        /// <summary>
        /// 检查指定提供商是否已登录
        /// </summary>
        public bool IsLoggedIn(string provider)
        {
            try
            {
                if (!IsAuthenticationEnabled())
                {
                    return false;
                }

                var isLoggedIn = _tokenManager.IsTokenValid(provider);
                _logger.LogDebug($"{provider} 登录状态检查: {(isLoggedIn ? "已登录" : "未登录")}", "OAuthLoginService");
                return isLoggedIn;
            }
            catch (Exception ex)
            {
                _logger.LogError($"检查{provider}登录状态失败: {ex.Message}", "OAuthLoginService", ex);
                return false;
            }
        }

        /// <summary>
        /// 获取指定提供商的令牌信息
        /// </summary>
        public TokenInfo? GetToken(string provider)
        {
            try
            {
                if (!IsAuthenticationEnabled())
                {
                    return null;
                }

                var token = _tokenManager.GetToken(provider);
                if (token != null)
                {
                    _logger.LogDebug($"获取{provider}令牌信息: 用户={token.UserName}, 过期时间={token.ExpiresAt:yyyy-MM-dd HH:mm:ss}", "OAuthLoginService");
                }
                else
                {
                    _logger.LogDebug($"获取{provider}令牌信息: 无有效令牌", "OAuthLoginService");
                }
                
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError($"获取{provider}令牌信息失败: {ex.Message}", "OAuthLoginService", ex);
                return null;
            }
        }

        /// <summary>
        /// 获取所有已登录的提供商
        /// </summary>
        public List<string> GetLoggedInProviders()
        {
            try
            {
                if (!IsAuthenticationEnabled())
                {
                    return new List<string>();
                }

                var providers = _tokenManager.GetProviders();
                _logger.LogDebug($"获取已登录提供商列表: {string.Join(", ", providers)}", "OAuthLoginService");
                return providers;
            }
            catch (Exception ex)
            {
                _logger.LogError($"获取已登录提供商列表失败: {ex.Message}", "OAuthLoginService", ex);
                return new List<string>();
            }
        }

        /// <summary>
        /// 登出指定提供商
        /// </summary>
        public async Task LogoutAsync(string provider)
        {
            try
            {
                if (!IsAuthenticationEnabled())
                {
                    _logger.LogWarning("OAuth认证已禁用，无需登出", "OAuthLoginService");
                    return;
                }

                if (_authManagers.ContainsKey(provider))
                {
                    _logger.LogInformation($"开始登出{provider}", "OAuthLoginService");
                    
                    await _authManagers[provider].LogoutAsync();
                    _tokenManager.RemoveToken(provider);
                    
                    _logger.LogInformation($"{provider}已登出", "OAuthLoginService");
                }
                else
                {
                    _logger.LogWarning($"{provider} OAuth提供商未初始化，无法登出", "OAuthLoginService");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"登出{provider}失败: {ex.Message}", "OAuthLoginService", ex);
            }
        }

        /// <summary>
        /// 登出所有提供商
        /// </summary>
        public async Task LogoutAllAsync()
        {
            try
            {
                if (!IsAuthenticationEnabled())
                {
                    _logger.LogWarning("OAuth认证已禁用，无需登出", "OAuthLoginService");
                    return;
                }

                _logger.LogInformation("开始登出所有提供商", "OAuthLoginService");
                
                foreach (var provider in _authManagers.Keys)
                {
                    await LogoutAsync(provider);
                }
                
                _logger.LogInformation("所有提供商已登出", "OAuthLoginService");
            }
            catch (Exception ex)
            {
                _logger.LogError($"登出所有提供商失败: {ex.Message}", "OAuthLoginService", ex);
            }
        }

        /// <summary>
        /// 刷新指定提供商的令牌
        /// </summary>
        public async Task<bool> RefreshTokenAsync(string provider)
        {
            try
            {
                if (!IsAuthenticationEnabled())
                {
                    _logger.LogWarning("OAuth认证已禁用，无法刷新令牌", "OAuthLoginService");
                    return false;
                }

                _logger.LogInformation($"开始刷新{provider}令牌", "OAuthLoginService");
                var result = await _tokenManager.RefreshTokenAsync(provider);
                
                if (result)
                {
                    _logger.LogInformation($"{provider}令牌刷新成功", "OAuthLoginService");
                }
                else
                {
                    _logger.LogWarning($"{provider}令牌刷新失败", "OAuthLoginService");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"刷新{provider}令牌失败: {ex.Message}", "OAuthLoginService", ex);
                return false;
            }
        }

        /// <summary>
        /// 获取登录状态摘要
        /// </summary>
        public string GetLoginStatusSummary()
        {
            try
            {
                if (!IsAuthenticationEnabled())
                {
                    return "OAuth认证已禁用";
                }

                var summary = _tokenManager.GetLoginStatusSummary();
                _logger.LogDebug($"获取登录状态摘要: {summary}", "OAuthLoginService");
                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError($"获取登录状态摘要失败: {ex.Message}", "OAuthLoginService", ex);
                return "获取登录状态失败";
            }
        }

        /// <summary>
        /// 检查是否有可用的OAuth提供商
        /// </summary>
        public bool HasAvailableProviders()
        {
            var hasProviders = IsAuthenticationEnabled() && _authManagers.Count > 0;
            _logger.LogDebug($"检查可用OAuth提供商: {(hasProviders ? "有" : "无")} ({_authManagers.Count}个)", "OAuthLoginService");
            return hasProviders;
        }

        /// <summary>
        /// 获取可用的OAuth提供商列表
        /// </summary>
        public List<string> GetAvailableProviders()
        {
            try
            {
                if (!IsAuthenticationEnabled())
                {
                    return new List<string>();
                }

                var providers = _authManagers.Keys.ToList();
                _logger.LogDebug($"获取可用OAuth提供商列表: {string.Join(", ", providers)}", "OAuthLoginService");
                return providers;
            }
            catch (Exception ex)
            {
                _logger.LogError($"获取可用OAuth提供商列表失败: {ex.Message}", "OAuthLoginService", ex);
                return new List<string>();
            }
        }

        /// <summary>
        /// 获取认证状态摘要
        /// </summary>
        public string GetAuthenticationStatusSummary()
        {
            try
            {
                var summary = _configService.GetAuthenticationStatusSummary();
                _logger.LogDebug($"获取认证状态摘要: {summary}", "OAuthLoginService");
                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError($"获取认证状态摘要失败: {ex.Message}", "OAuthLoginService", ex);
                return "获取认证状态失败";
            }
        }

        /// <summary>
        /// 重新加载配置
        /// </summary>
        public void ReloadConfiguration()
        {
            try
            {
                _logger.LogInformation("重新加载OAuth配置", "OAuthLoginService");
                
                _configService.ReloadConfiguration();
                
                // 如果认证状态发生变化，重新初始化
                if (IsAuthenticationEnabled())
                {
                    InitializeOAuthProviders();
                }
                else
                {
                    // 清理所有授权管理器
                    foreach (var authManager in _authManagers.Values)
                    {
                        authManager?.Dispose();
                    }
                    _authManagers.Clear();
                }
                
                _logger.LogInformation("OAuth配置重新加载完成", "OAuthLoginService");
            }
            catch (Exception ex)
            {
                _logger.LogError($"重新加载OAuth配置失败: {ex.Message}", "OAuthLoginService", ex);
            }
        }

        public void Dispose()
        {
            try
            {
                _logger.LogInformation("开始释放OAuth登录服务资源", "OAuthLoginService");
                
                foreach (var authManager in _authManagers.Values)
                {
                    authManager?.Dispose();
                }
                _tokenManager?.Dispose();
                
                _logger.LogInformation("OAuth登录服务资源释放完成", "OAuthLoginService");
            }
            catch (Exception ex)
            {
                _logger.LogError($"释放OAuth登录服务资源时出错: {ex.Message}", "OAuthLoginService", ex);
            }
        }
    }
}

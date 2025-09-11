using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using AudioRecorder.Models;

namespace AudioRecorder.Services
{
    /// <summary>
    /// 令牌管理器 - 负责管理OAuth令牌的存储、验证和自动刷新
    /// </summary>
    public class TokenManager : IDisposable
    {
        private readonly SecureStorageManager _storageManager;
        private readonly Dictionary<string, TokenInfo> _tokens;
        private readonly Dictionary<string, AuthorizationManager> _authManagers;
        private readonly System.Timers.Timer _refreshTimer;
        private readonly System.Timers.Timer _monitorTimer;

        public event EventHandler<TokenInfo>? TokenRefreshed;
        public event EventHandler<string>? TokenExpired;
        public event EventHandler<TokenInfo>? LoginStateRestored;

        public TokenManager()
        {
            _storageManager = new SecureStorageManager();
            _tokens = new Dictionary<string, TokenInfo>();
            _authManagers = new Dictionary<string, AuthorizationManager>();

            // 每30分钟检查一次令牌状态
            _refreshTimer = new System.Timers.Timer(30 * 60 * 1000);
            _refreshTimer.Elapsed += OnRefreshTimerElapsed;

            // 每5分钟监控一次令牌状态
            _monitorTimer = new System.Timers.Timer(5 * 60 * 1000);
            _monitorTimer.Elapsed += OnMonitorTimerElapsed;
        }

        /// <summary>
        /// 程序启动时自动恢复登录状态
        /// </summary>
        public async Task<bool> RestoreLoginStateAsync()
        {
            try
            {
                Console.WriteLine("🔄 开始恢复登录状态...");

                // 1. 从安全存储加载所有令牌
                var providers = await _storageManager.GetStoredProvidersAsync();
                var restoredCount = 0;

                foreach (var provider in providers)
                {
                    var tokenInfo = await _storageManager.LoadTokensAsync(provider);
                    if (tokenInfo != null)
                    {
                        // 2. 验证令牌有效性
                        if (await ValidateTokenAsync(tokenInfo))
                        {
                            AddTokenToMemory(provider, tokenInfo);
                            restoredCount++;

                            // 3. 如果令牌即将过期，立即刷新
                            if (IsTokenExpiringSoon(provider, 10)) // 10分钟内过期
                            {
                                _ = Task.Run(async () => await RefreshTokenAsync(provider));
                            }

                            Console.WriteLine($"✅ 恢复登录状态: {provider} - {tokenInfo.UserName}");
                            LoginStateRestored?.Invoke(this, tokenInfo);
                        }
                        else
                        {
                            // 4. 无效令牌自动清理
                            await _storageManager.DeleteTokensAsync(provider);
                            Console.WriteLine($"🧹 清理无效令牌: {provider}");
                        }
                    }
                }

                // 5. 启动自动刷新服务
                if (restoredCount > 0)
                {
                    StartAutoRefresh();
                    Console.WriteLine($"🔄 启动令牌自动刷新服务，已恢复 {restoredCount} 个账户");
                }
                else
                {
                    Console.WriteLine("ℹ️ 无有效登录状态，需要手动登录");
                }

                return restoredCount > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 恢复登录状态失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 验证令牌有效性
        /// </summary>
        private async Task<bool> ValidateTokenAsync(TokenInfo tokenInfo)
        {
            try
            {
                // 检查基本过期时间
                if (tokenInfo.IsExpired)
                {
                    return false;
                }

                // 检查刷新令牌是否过期
                if (tokenInfo.IsRefreshTokenExpired)
                {
                    return false;
                }

                // 可选：调用API验证令牌有效性
                if (await ValidateTokenWithProviderAsync(tokenInfo))
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 调用提供商API验证令牌
        /// </summary>
        private async Task<bool> ValidateTokenWithProviderAsync(TokenInfo tokenInfo)
        {
            try
            {
                if (tokenInfo.Provider.Equals("GitHub", StringComparison.OrdinalIgnoreCase))
                {
                    using (var client = new System.Net.Http.HttpClient())
                    {
                        client.DefaultRequestHeaders.Authorization = 
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenInfo.AccessToken);
                        client.DefaultRequestHeaders.Add("User-Agent", "AudioRecorder"); // GitHub API要求User-Agent
                        
                        // 调用用户信息API验证令牌
                        var response = await client.GetAsync("https://api.github.com/user");
                        return response.IsSuccessStatusCode;
                    }
                }
                
                // 其他提供商可以在这里添加验证逻辑
                return true; // 默认假设令牌有效
            }
            catch
            {
                // 网络错误时不阻止恢复，假设令牌有效
                return true;
            }
        }

        /// <summary>
        /// 启动自动刷新
        /// </summary>
        public void StartAutoRefresh()
        {
            _refreshTimer.Start();
            _monitorTimer.Start();
            Console.WriteLine("🔄 令牌自动刷新服务已启动");
        }

        /// <summary>
        /// 停止自动刷新
        /// </summary>
        public void StopAutoRefresh()
        {
            _refreshTimer.Stop();
            _monitorTimer.Stop();
            Console.WriteLine("🛑 令牌自动刷新服务已停止");
        }

        /// <summary>
        /// 刷新定时器事件
        /// </summary>
        private async void OnRefreshTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            await CheckAndRefreshTokensAsync();
        }

        /// <summary>
        /// 监控定时器事件
        /// </summary>
        private async void OnMonitorTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            await MonitorTokenStatusAsync();
        }

        /// <summary>
        /// 检查并刷新令牌
        /// </summary>
        private async Task CheckAndRefreshTokensAsync()
        {
            foreach (var provider in _tokens.Keys.ToList())
            {
                if (IsTokenExpiringSoon(provider, 5)) // 5分钟内过期
                {
                    await RefreshTokenAsync(provider);
                }
            }
        }

        /// <summary>
        /// 监控令牌状态
        /// </summary>
        private async Task MonitorTokenStatusAsync()
        {
            foreach (var kvp in _tokens.ToList())
            {
                var provider = kvp.Key;
                var tokenInfo = kvp.Value;

                if (tokenInfo.IsExpired)
                {
                    Console.WriteLine($"⚠️ 令牌已过期: {provider}");
                    TokenExpired?.Invoke(this, provider);
                    
                    // 尝试刷新令牌
                    await RefreshTokenAsync(provider);
                }
                else if (tokenInfo.IsExpiringSoon)
                {
                    Console.WriteLine($"⚠️ 令牌即将过期: {provider}, 剩余时间: {tokenInfo.TimeUntilExpiry.TotalMinutes:F1}分钟");
                }
            }
        }

        /// <summary>
        /// 刷新指定提供商的令牌
        /// </summary>
        public async Task<bool> RefreshTokenAsync(string provider)
        {
            try
            {
                if (!_tokens.ContainsKey(provider))
                {
                    Console.WriteLine($"⚠️ 未找到提供商: {provider}");
                    return false;
                }

                var currentToken = _tokens[provider];
                if (string.IsNullOrEmpty(currentToken.RefreshToken))
                {
                    Console.WriteLine($"⚠️ 缺少刷新令牌: {provider}");
                    return false;
                }

                if (_authManagers.ContainsKey(provider))
                {
                    var newToken = await _authManagers[provider].RefreshTokenAsync(currentToken);
                    if (newToken != null)
                    {
                        _tokens[provider] = newToken;
                        TokenRefreshed?.Invoke(this, newToken);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 刷新令牌失败: {provider}, 错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查令牌是否即将过期
        /// </summary>
        public bool IsTokenExpiringSoon(string provider, int minutesAhead = 5)
        {
            if (_tokens.ContainsKey(provider))
            {
                return _tokens[provider].IsExpiringSoon;
            }
            return false;
        }

        /// <summary>
        /// 检查令牌是否有效
        /// </summary>
        public bool IsTokenValid(string provider)
        {
            if (_tokens.ContainsKey(provider))
            {
                return _tokens[provider].IsExpired == false;
            }
            return false;
        }

        /// <summary>
        /// 获取令牌信息
        /// </summary>
        public TokenInfo? GetToken(string provider)
        {
            return _tokens.ContainsKey(provider) ? _tokens[provider] : null;
        }

        /// <summary>
        /// 获取所有提供商
        /// </summary>
        public List<string> GetProviders()
        {
            return _tokens.Keys.ToList();
        }

        /// <summary>
        /// 获取过期的令牌
        /// </summary>
        public List<TokenInfo> GetExpiredTokens()
        {
            return _tokens.Values.Where(t => t.IsExpired).ToList();
        }

        /// <summary>
        /// 添加授权管理器
        /// </summary>
        public void AddAuthorizationManager(string provider, AuthorizationManager authManager)
        {
            _authManagers[provider] = authManager;
        }

        /// <summary>
        /// 添加令牌
        /// </summary>
        public async Task AddTokenAsync(string provider, TokenInfo tokenInfo)
        {
            try
            {
                // 同时更新内存和存储
                _tokens[provider] = tokenInfo;
                await _storageManager.SaveTokensAsync(provider, tokenInfo);
                
                Console.WriteLine($"✅ 令牌已添加并保存: {provider} - {tokenInfo.UserName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 添加令牌失败: {provider}, 错误: {ex.Message}");
                // 如果保存失败，从内存中移除
                _tokens.Remove(provider);
                throw;
            }
        }

        /// <summary>
        /// 添加令牌（仅内存，用于从存储恢复时）
        /// </summary>
        public void AddTokenToMemory(string provider, TokenInfo tokenInfo)
        {
            _tokens[provider] = tokenInfo;
        }

        /// <summary>
        /// 移除令牌
        /// </summary>
        public async Task RemoveTokenAsync(string provider)
        {
            try
            {
                // 同时从内存和存储中移除
                if (_tokens.ContainsKey(provider))
                {
                    _tokens.Remove(provider);
                }
                await _storageManager.DeleteTokensAsync(provider);
                
                Console.WriteLine($"✅ 令牌已移除: {provider}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 移除令牌失败: {provider}, 错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 移除令牌（仅内存）
        /// </summary>
        public void RemoveTokenFromMemory(string provider)
        {
            if (_tokens.ContainsKey(provider))
            {
                _tokens.Remove(provider);
            }
        }

        /// <summary>
        /// 清除所有令牌
        /// </summary>
        public async Task ClearAllTokensAsync()
        {
            try
            {
                _tokens.Clear();
                await _storageManager.ClearAllTokensAsync();
                Console.WriteLine("🧹 已清除所有令牌");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 清除所有令牌失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取登录状态摘要
        /// </summary>
        public string GetLoginStatusSummary()
        {
            if (_tokens.Count == 0)
            {
                return "未登录";
            }

            var summaries = _tokens.Select(kvp => 
            {
                var provider = kvp.Key;
                var token = kvp.Value;
                var status = token.IsExpired ? "已过期" : "有效";
                var timeLeft = token.IsExpired ? "已过期" : $"{token.TimeUntilExpiry.TotalMinutes:F1}分钟";
                return $"{provider}: {token.UserName} ({status}, 剩余: {timeLeft})";
            });

            return string.Join("; ", summaries);
        }

        public void Dispose()
        {
            _refreshTimer?.Dispose();
            _monitorTimer?.Dispose();
            
            foreach (var authManager in _authManagers.Values)
            {
                authManager?.Dispose();
            }
        }
    }
}

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AudioRecorder.Models;
using System.Collections.Generic;
using System.Linq;

namespace AudioRecorder.Services
{
    /// <summary>
    /// OAuth授权管理器
    /// </summary>
    public class AuthorizationManager : IDisposable
    {
        private readonly SecureStorageManager _storageManager;
        private readonly LocalHttpServer _httpServer;
        private readonly HttpClient _httpClient;
        private readonly OAuthConfig _config;

        public event EventHandler<TokenInfo>? AuthorizationCompleted;
        public event EventHandler<string>? AuthorizationFailed;
        public event EventHandler<TokenInfo>? TokenRefreshed;

        public AuthorizationManager(OAuthConfig config)
        {
            _config = config;
            _storageManager = new SecureStorageManager();
            _httpServer = new LocalHttpServer();
            _httpClient = new HttpClient();

            // 订阅HTTP服务器事件
            _httpServer.AuthorizationCodeReceived += OnAuthorizationCodeReceived;
            _httpServer.ErrorOccurred += OnHttpServerError;
        }

        /// <summary>
        /// 开始授权流程
        /// </summary>
        public async Task<bool> StartAuthorizationAsync()
        {
            try
            {
                Console.WriteLine($"🚀 开始 {_config.ProviderName} OAuth授权流程");

                // 1. 启动本地HTTP服务器
                var serverStarted = await _httpServer.StartAsync();
                if (!serverStarted)
                {
                    throw new Exception("无法启动本地HTTP服务器");
                }

                // 2. 构建授权URL
                var authUrl = BuildAuthorizationUrl();
                Console.WriteLine($"🔗 授权URL: {authUrl}");

                // 3. 打开浏览器
                OpenBrowser(authUrl);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 启动授权流程失败: {ex.Message}");
                AuthorizationFailed?.Invoke(this, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 构建授权URL
        /// </summary>
        private string BuildAuthorizationUrl()
        {
            var state = Guid.NewGuid().ToString("N");
            var scope = string.Join(" ", _config.Scopes);
            var callbackUrl = _httpServer.GetCallbackUrl();

            var queryParams = new Dictionary<string, string>
            {
                ["client_id"] = _config.ClientId,
                ["redirect_uri"] = callbackUrl,
                ["response_type"] = _config.ResponseType,
                ["scope"] = scope,
                ["state"] = state
            };

            // 添加可选的OAuth参数
            if (!string.IsNullOrEmpty(_config.AccessType))
            {
                queryParams["access_type"] = _config.AccessType;
            }

            if (!string.IsNullOrEmpty(_config.Prompt))
            {
                queryParams["prompt"] = _config.Prompt;
            }

            var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
            return $"{_config.AuthorizationEndpoint}?{queryString}";
        }

        /// <summary>
        /// 打开浏览器
        /// </summary>
        private void OpenBrowser(string url)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };

                Process.Start(processStartInfo);
                Console.WriteLine("🌐 已在默认浏览器中打开授权页面");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 无法自动打开浏览器: {ex.Message}");
                Console.WriteLine($"请手动复制以下URL到浏览器: {url}");
            }
        }

        /// <summary>
        /// 处理授权码接收
        /// </summary>
        private async void OnAuthorizationCodeReceived(object? sender, string authorizationCode)
        {
            try
            {
                Console.WriteLine($"📥 收到授权码，开始交换令牌...");

                // 1. 使用授权码交换访问令牌
                var tokenInfo = await ExchangeAuthorizationCodeAsync(authorizationCode);
                if (tokenInfo == null)
                {
                    throw new Exception("令牌交换失败");
                }

                // 2. 设置提供商信息
                tokenInfo.Provider = _config.ProviderName;

                // 3. 获取用户信息
                await GetUserInfoAsync(tokenInfo);

                // 4. 重新计算过期时间
                tokenInfo.RecalculateExpiryTimes();

                // 5. 保存令牌
                await _storageManager.SaveTokensAsync(_config.ProviderName, tokenInfo);

                // 6. 停止HTTP服务器
                _httpServer.Stop();

                Console.WriteLine($"✅ {_config.ProviderName} 授权完成！");
                AuthorizationCompleted?.Invoke(this, tokenInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 处理授权码失败: {ex.Message}");
                AuthorizationFailed?.Invoke(this, ex.Message);
                _httpServer.Stop();
            }
        }

        /// <summary>
        /// 处理HTTP服务器错误
        /// </summary>
        private void OnHttpServerError(object? sender, string error)
        {
            Console.WriteLine($"❌ HTTP服务器错误: {error}");
            AuthorizationFailed?.Invoke(this, error);
        }

        /// <summary>
        /// 使用授权码交换访问令牌
        /// </summary>
        private async Task<TokenInfo?> ExchangeAuthorizationCodeAsync(string authorizationCode)
        {
            try
            {
                var tokenRequest = new Dictionary<string, string>
                {
                    ["client_id"] = _config.ClientId,
                    ["client_secret"] = _config.ClientSecret,
                    ["code"] = authorizationCode,
                    ["grant_type"] = "authorization_code",
                    ["redirect_uri"] = _httpServer.GetCallbackUrl()
                };

                var content = new FormUrlEncodedContent(tokenRequest);
                
                // 设置请求头
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.PostAsync(_config.TokenEndpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ 令牌交换失败: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"📥 令牌响应: {responseContent}");

                // GitHub返回的是application/x-www-form-urlencoded格式，需要特殊处理
                TokenInfo? tokenInfo;
                if (_config.ProviderName.Equals("GitHub", StringComparison.OrdinalIgnoreCase))
                {
                    tokenInfo = ParseGitHubTokenResponse(responseContent);
                }
                else
                {
                    tokenInfo = JsonSerializer.Deserialize<TokenInfo>(responseContent);
                }

                if (tokenInfo == null)
                {
                    Console.WriteLine("❌ 无法解析令牌响应");
                    return null;
                }

                Console.WriteLine($"✅ 令牌交换成功，有效期: {tokenInfo.ExpiresIn}秒");
                return tokenInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 令牌交换异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 解析GitHub令牌响应（GitHub返回的是form-urlencoded格式）
        /// </summary>
        private TokenInfo? ParseGitHubTokenResponse(string responseContent)
        {
            try
            {
                var tokenInfo = new TokenInfo();
                var lines = responseContent.Split('&');
                
                foreach (var line in lines)
                {
                    var parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        var key = Uri.UnescapeDataString(parts[0]);
                        var value = Uri.UnescapeDataString(parts[1]);
                        
                        switch (key)
                        {
                            case "access_token":
                                tokenInfo.AccessToken = value;
                                break;
                            case "token_type":
                                tokenInfo.TokenType = value;
                                break;
                            case "scope":
                                tokenInfo.Scope = value;
                                break;
                        }
                    }
                }

                // GitHub OAuth App不返回refresh_token和expires_in
                // 设置一个合理的过期时间（1小时）
                tokenInfo.ExpiresIn = 3600;
                
                return tokenInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 解析GitHub令牌响应失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取用户信息
        /// </summary>
        private async Task GetUserInfoAsync(TokenInfo tokenInfo)
        {
            try
            {
                if (_config.ProviderName.Equals("GitHub", StringComparison.OrdinalIgnoreCase))
                {
                    await GetGitHubUserInfoAsync(tokenInfo);
                }
                else if (_config.ProviderName.Equals("Google", StringComparison.OrdinalIgnoreCase))
                {
                    await GetGoogleUserInfoAsync(tokenInfo);
                }
                else
                {
                    Console.WriteLine($"⚠️ 暂不支持获取 {_config.ProviderName} 用户信息");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 获取用户信息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取GitHub用户信息
        /// </summary>
        private async Task GetGitHubUserInfoAsync(TokenInfo tokenInfo)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenInfo.AccessToken);
                request.Headers.Add("User-Agent", "AudioRecorder"); // GitHub API要求User-Agent
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var userInfo = JsonSerializer.Deserialize<GitHubUserInfo>(content);

                    if (userInfo != null)
                    {
                        tokenInfo.UserId = userInfo.Id.ToString();
                        tokenInfo.UserEmail = userInfo.Email ?? userInfo.Login; // 如果邮箱为空，使用用户名
                        tokenInfo.UserName = userInfo.Name ?? userInfo.Login; // 如果姓名为空，使用用户名
                        tokenInfo.UserAvatar = userInfo.AvatarUrl;

                        Console.WriteLine($"👤 GitHub用户信息: {userInfo.Name ?? userInfo.Login} ({userInfo.Login})");
                        
                        // 如果邮箱为空，尝试获取邮箱信息
                        if (string.IsNullOrEmpty(userInfo.Email))
                        {
                            await GetGitHubUserEmailsAsync(tokenInfo);
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ 获取GitHub用户信息失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 获取GitHub用户信息异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取GitHub用户邮箱信息
        /// </summary>
        private async Task GetGitHubUserEmailsAsync(TokenInfo tokenInfo)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenInfo.AccessToken);
                request.Headers.Add("User-Agent", "AudioRecorder");
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var emails = JsonSerializer.Deserialize<GitHubUserEmail[]>(content);

                    if (emails != null && emails.Length > 0)
                    {
                        // 优先使用主邮箱
                        var primaryEmail = emails.FirstOrDefault(e => e.Primary) ?? emails[0];
                        tokenInfo.UserEmail = primaryEmail.Email;
                        Console.WriteLine($"📧 GitHub用户邮箱: {primaryEmail.Email}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 获取GitHub用户邮箱失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取Google用户信息
        /// </summary>
        private async Task GetGoogleUserInfoAsync(TokenInfo tokenInfo)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenInfo.AccessToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var userInfo = JsonSerializer.Deserialize<GoogleUserInfo>(content);

                    if (userInfo != null)
                    {
                        tokenInfo.UserId = userInfo.Id;
                        tokenInfo.UserEmail = userInfo.Email;
                        tokenInfo.UserName = userInfo.Name;
                        tokenInfo.UserAvatar = userInfo.Picture;

                        Console.WriteLine($"👤 Google用户信息: {userInfo.Name} ({userInfo.Email})");
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ 获取Google用户信息失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 获取Google用户信息异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新访问令牌
        /// </summary>
        public async Task<TokenInfo?> RefreshTokenAsync(TokenInfo currentToken)
        {
            try
            {
                if (string.IsNullOrEmpty(currentToken.RefreshToken))
                {
                    // GitHub OAuth App不支持刷新令牌，需要重新授权
                    if (_config.ProviderName.Equals("GitHub", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"⚠️ GitHub OAuth App不支持刷新令牌，需要重新授权");
                        return null;
                    }
                    
                    throw new Exception("缺少刷新令牌");
                }

                Console.WriteLine($"🔄 刷新 {_config.ProviderName} 访问令牌...");

                var refreshRequest = new Dictionary<string, string>
                {
                    ["client_id"] = _config.ClientId,
                    ["client_secret"] = _config.ClientSecret,
                    ["refresh_token"] = currentToken.RefreshToken,
                    ["grant_type"] = "refresh_token"
                };

                var content = new FormUrlEncodedContent(refreshRequest);
                var response = await _httpClient.PostAsync(_config.TokenEndpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ 令牌刷新失败: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var newTokenInfo = JsonSerializer.Deserialize<TokenInfo>(responseContent);

                if (newTokenInfo == null)
                {
                    throw new Exception("无法解析刷新令牌响应");
                }

                // 保持原有的用户信息和刷新令牌
                newTokenInfo.Provider = currentToken.Provider;
                newTokenInfo.UserId = currentToken.UserId;
                newTokenInfo.UserEmail = currentToken.UserEmail;
                newTokenInfo.UserName = currentToken.UserName;
                newTokenInfo.UserAvatar = currentToken.UserAvatar;
                newTokenInfo.RefreshToken = currentToken.RefreshToken; // 刷新令牌通常不变

                // 重新计算过期时间
                newTokenInfo.RecalculateExpiryTimes();

                // 保存新的令牌
                await _storageManager.SaveTokensAsync(_config.ProviderName, newTokenInfo);

                Console.WriteLine($"✅ 令牌刷新成功，新有效期: {newTokenInfo.ExpiresIn}秒");
                TokenRefreshed?.Invoke(this, newTokenInfo);

                return newTokenInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 刷新令牌失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 检查令牌是否有效
        /// </summary>
        public bool IsTokenValid(TokenInfo tokenInfo)
        {
            if (tokenInfo == null) return false;

            // 检查访问令牌是否过期
            if (tokenInfo.IsExpired)
            {
                Console.WriteLine($"⚠️ 访问令牌已过期: {_config.ProviderName}");
                return false;
            }

            // 检查刷新令牌是否过期（如果有的话）
            if (!string.IsNullOrEmpty(tokenInfo.RefreshToken) && tokenInfo.IsRefreshTokenExpired)
            {
                Console.WriteLine($"⚠️ 刷新令牌已过期: {_config.ProviderName}");
                return false;
            }

            // 检查是否即将过期（5分钟内）
            if (tokenInfo.IsExpiringSoon)
            {
                Console.WriteLine($"⚠️ 访问令牌即将过期: {_config.ProviderName}, 剩余时间: {tokenInfo.TimeUntilExpiry.TotalMinutes:F1}分钟");
            }

            return true;
        }

        /// <summary>
        /// 登出
        /// </summary>
        public async Task LogoutAsync()
        {
            try
            {
                await _storageManager.DeleteTokensAsync(_config.ProviderName);
                Console.WriteLine($"🚪 已登出 {_config.ProviderName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 登出失败: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _httpServer?.Dispose();
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// GitHub用户邮箱信息
    /// </summary>
    public class GitHubUserEmail
    {
        [System.Text.Json.Serialization.JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("primary")]
        public bool Primary { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("verified")]
        public bool Verified { get; set; }
    }

    /// <summary>
    /// Google用户信息
    /// </summary>
    public class GoogleUserInfo
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("verified_email")]
        public bool VerifiedEmail { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("given_name")]
        public string GivenName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("family_name")]
        public string FamilyName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("picture")]
        public string Picture { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("locale")]
        public string Locale { get; set; } = string.Empty;
    }
}

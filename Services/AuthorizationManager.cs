using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AudioRecorder.Models;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Drawing;

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
        private readonly ILogger _logger;
        private string? _sentState; // 存储发送的state参数

        public event EventHandler<TokenInfo>? AuthorizationCompleted;
        public event EventHandler<string>? AuthorizationFailed;
        public event EventHandler<TokenInfo>? TokenRefreshed;
        public event EventHandler<TokenInfo>? TokenSaved; // 新增：令牌保存事件

        public AuthorizationManager(OAuthConfig config)
        {
            _config = config;
            _storageManager = new SecureStorageManager();
            _httpServer = new LocalHttpServer(0); // 使用随机端口 (0表示动态分配)
            _httpClient = new HttpClient();
            _logger = LoggingServiceManager.CreateLogger("AuthorizationManager");

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
                _logger.LogInformation($"🚀 开始 {_config.ProviderName} OAuth授权流程");

                // 1. 启动本地HTTP服务器
                var serverStarted = await _httpServer.StartAsync();
                if (!serverStarted)
                {
                    throw new Exception("无法启动本地HTTP服务器");
                }

                // 记录实际使用的端口
                _logger.LogInformation($"🔌 使用端口: {_httpServer.GetPort()}");
                _logger.LogInformation($"🔗 回调URL: {_httpServer.GetCallbackUrl()}");

                // 2. 构建授权URL
                var authUrl = BuildAuthorizationUrl();
                _logger.LogInformation($"🔗 授权URL: {authUrl}");

                // 3. 打开浏览器
                OpenBrowser(authUrl);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ 启动授权流程失败: {ex.Message}");
                AuthorizationFailed?.Invoke(this, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 构建授权URL
        /// </summary>
        private string BuildAuthorizationUrl()
        {
            // 生成并存储state参数
            _sentState = Guid.NewGuid().ToString("N");
            var scope = string.Join(" ", _config.Scopes);
            var callbackUrl = _httpServer.GetCallbackUrl();

            var queryParams = new Dictionary<string, string>
            {
                ["client_id"] = _config.ClientId,
                ["redirect_uri"] = callbackUrl,
                ["response_type"] = _config.ResponseType,
                ["scope"] = scope,
                ["state"] = _sentState
            };

            _logger.LogInformation($"📤 发送State参数: {_sentState}");

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
                _logger.LogInformation("🌐 已在默认浏览器中打开授权页面");
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"⚠️ 无法自动打开浏览器: {ex.Message}");
                _logger.LogInformation($"请手动复制以下URL到浏览器: {url}");
            }
        }

        /// <summary>
        /// 处理授权码接收
        /// </summary>
        private async void OnAuthorizationCodeReceived(object? sender, string authorizationCodeAndState)
        {
            try
            {
                _logger.LogInformation($"📥 收到授权码，开始交换令牌...");
                var list = authorizationCodeAndState.Split("|");
                var authorizationCode = list[0];
                var receivedState = list[1];

                if (!string.IsNullOrEmpty(receivedState))
                {
                    _logger.LogInformation($"📋 从回调获取到State参数: {receivedState}");
                    
                    // 验证state参数是否匹配（防止CSRF攻击）
                    if (!string.IsNullOrEmpty(_sentState) && receivedState != _sentState)
                    {
                        throw new Exception($"State参数不匹配，可能存在安全风险。发送: {_sentState}, 接收: {receivedState}");
                    }
                    _logger.LogInformation("✅ State参数验证通过");
                }
                else if (!string.IsNullOrEmpty(_sentState))
                {
                    _logger.LogInformation("⚠️ 未收到State参数，但发送时包含了State参数");
                }

                // 1. 使用授权码交换访问令牌，传递state参数
                var tokenInfo = await ExchangeAuthorizationCodeAsync(authorizationCode, receivedState);
                if (tokenInfo == null || String.IsNullOrEmpty( tokenInfo.AccessToken))
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
                
                // 5.1. 通知令牌已保存
                TokenSaved?.Invoke(this, tokenInfo);

                // 6. 停止HTTP服务器
                _httpServer.Stop();

                _logger.LogInformation($"✅ {_config.ProviderName} 授权完成！");
                AuthorizationCompleted?.Invoke(this, tokenInfo);
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"❌ 处理授权码失败: {ex.Message}");
                AuthorizationFailed?.Invoke(this, ex.Message);
            }
        }

        /// <summary>
        /// 处理HTTP服务器错误
        /// </summary>
        private void OnHttpServerError(object? sender, string error)
        {
            _logger.LogInformation($"❌ HTTP服务器错误: {error}");
            AuthorizationFailed?.Invoke(this, error);
        }

        /// <summary>
        /// 使用授权码交换访问令牌
        /// </summary>
        private async Task<TokenInfo?> ExchangeAuthorizationCodeAsync(string authorizationCode, string? state = null)
        {
            try
            {
                var tokenRequest = new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["client_id"] = _config.ClientId,
                    ["client_secret"] = _config.ClientSecret,
                    ["code"] = authorizationCode,
                    ["redirect_uri"] = _httpServer.GetCallbackUrl()
                };

                // 如果有state参数，包含在请求中
                if (!string.IsNullOrEmpty(state))
                {
                    tokenRequest["state"] = state;
                    _logger.LogInformation($"📋 在令牌交换请求中包含State参数: {state}");
                }

                var content = new FormUrlEncodedContent(tokenRequest);
                
                // 设置请求头 - 使用Basic Authentication
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));
                _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "AudioRecorder/1.0");

                // 创建Basic Authentication头 (clientId:clientSecret的base64编码)
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.ClientId}:{_config.ClientSecret}"));
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

                var response = await _httpClient.PostAsync(_config.TokenEndpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"❌ 令牌交换失败: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"📥 令牌响应: {responseContent}");

                // 根据Content-Type和提供商类型解析响应
                TokenInfo? tokenInfo;
                var contentType = response.Content.Headers.ContentType?.MediaType?.ToLower();
                
                if (_config.ProviderName.Equals("GitHub", StringComparison.OrdinalIgnoreCase))
                {
                    // GitHub返回的是application/x-www-form-urlencoded格式
                    tokenInfo = ParseGitHubTokenResponse(responseContent);
                }
                else if (contentType?.Contains("application/json") == true || responseContent.TrimStart().StartsWith("{"))
                {
                    // JSON格式响应
                    try
                    {
                        // 首先尝试解析包装的响应格式
                        if (responseContent.Contains("\"code\"") && responseContent.Contains("\"data\""))
                        {
                            var wrappedResponse = JsonSerializer.Deserialize<WrappedTokenResponse>(responseContent, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            if (wrappedResponse != null)
                            {
                                if (wrappedResponse.IsSuccess)
                                {
                                    tokenInfo = wrappedResponse.GetTokenInfo();
                                    _logger.LogInformation($"✅ 成功解析包装的令牌响应");
                                }
                                else
                                {
                                    _logger.LogInformation($"❌ 服务器返回错误: Code={wrappedResponse.Code}, Message={wrappedResponse.Message}");
                                    return null;
                                }
                            }
                            else
                            {
                                _logger.LogInformation($"❌ 无法解析包装的令牌响应");
                                return null;
                            }
                        }
                        else
                        {
                            // 尝试直接解析TokenInfo格式
                            tokenInfo = JsonSerializer.Deserialize<TokenInfo>(responseContent, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogInformation($"❌ JSON解析失败: {ex.Message}");
                        _logger.LogInformation($"📄 响应内容: {responseContent}");
                        return null;
                    }
                }
                else
                {
                    // 尝试作为form-urlencoded格式解析
                    tokenInfo = ParseFormUrlEncodedTokenResponse(responseContent);
                }

                if (tokenInfo == null)
                {
                    _logger.LogInformation("❌ 无法解析令牌响应");
                    return null;
                }

                _logger.LogInformation($"✅ 令牌交换成功，有效期: {tokenInfo.ExpiresIn}秒");
                return tokenInfo;
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"❌ 令牌交换异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 解析form-urlencoded格式的令牌响应
        /// </summary>
        private TokenInfo? ParseFormUrlEncodedTokenResponse(string responseContent)
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
                        
                        switch (key.ToLower())
                        {
                            case "access_token":
                                tokenInfo.AccessToken = value;
                                break;
                            case "refresh_token":
                                tokenInfo.RefreshToken = value;
                                break;
                            case "id_token":
                                tokenInfo.IdToken = value;
                                break;
                            case "token_type":
                                tokenInfo.TokenType = value;
                                break;
                            case "expires_in":
                                if (int.TryParse(value, out int expiresIn))
                                {
                                    tokenInfo.ExpiresIn = expiresIn;
                                }
                                break;
                            case "scope":
                                tokenInfo.Scope = value;
                                break;
                        }
                    }
                }

                // 如果没有设置过期时间，设置一个默认值
                if (tokenInfo.ExpiresIn <= 0)
                {
                    tokenInfo.ExpiresIn = 3600; // 1小时
                }
                
                return tokenInfo;
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"❌ 解析form-urlencoded令牌响应失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 解析GitHub令牌响应（GitHub返回的是form-urlencoded格式）
        /// </summary>
        private TokenInfo? ParseGitHubTokenResponse(string responseContent)
        {
            // 使用通用的form-urlencoded解析方法
            var tokenInfo = ParseFormUrlEncodedTokenResponse(responseContent);
            
            if (tokenInfo != null)
            {
                // GitHub OAuth App不返回refresh_token和expires_in
                // 设置一个合理的过期时间（1小时）
                if (tokenInfo.ExpiresIn <= 0)
                {
                    tokenInfo.ExpiresIn = 3600;
                }
            }
            
            return tokenInfo;
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
                else if (!string.IsNullOrEmpty(_config.UserInfoEndpoint))
                {
                    // 自定义OAuth2服务器
                    await GetGenericUserInfoAsync(tokenInfo);
                }
                else
                {
                    _logger.LogWarning($"⚠️ {_config.ProviderName} 未配置用户信息端点，跳过获取用户信息");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"⚠️ 获取用户信息失败: {ex.Message}");
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

                        _logger.LogInformation($"👤 GitHub用户信息: {userInfo.Name ?? userInfo.Login} ({userInfo.Login})");
                        
                        // 如果邮箱为空，尝试获取邮箱信息
                        if (string.IsNullOrEmpty(userInfo.Email))
                        {
                            await GetGitHubUserEmailsAsync(tokenInfo);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning($"⚠️ 获取GitHub用户信息失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"⚠️ 获取GitHub用户信息异常: {ex.Message}");
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
                        _logger.LogInformation($"📧 GitHub用户邮箱: {primaryEmail.Email}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"⚠️ 获取GitHub用户邮箱失败: {ex.Message}");
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

                        _logger.LogInformation($"👤 Google用户信息: {userInfo.Name} ({userInfo.Email})");
                    }
                }
                else
                {
                    _logger.LogWarning($"⚠️ 获取Google用户信息失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"⚠️ 获取Google用户信息异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取自定义OAuth2服务器用户信息
        /// </summary>
        private async Task GetGenericUserInfoAsync(TokenInfo tokenInfo)
        {
            try
            {
                _logger.LogInformation($"🔍 正在从 {_config.ProviderName} 获取用户信息...");

                var request = new HttpRequestMessage(HttpMethod.Get, _config.UserInfoEndpoint);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenInfo.AccessToken);
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                // 添加User-Agent头，某些服务器可能需要
                request.Headers.Add("User-Agent", "AudioRecorder/1.0");

                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug($"📥 用户信息响应: {content}");

                    try
                    {
                        // 首先尝试解析包装的用户信息响应格式
                        if (content.Contains("\"code\"") && content.Contains("\"data\""))
                        {
                            var wrappedUserResponse = JsonSerializer.Deserialize<WrappedUserInfoResponse>(content, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            if (wrappedUserResponse != null && wrappedUserResponse.IsSuccess)
                            {
                                var serverUserInfo = wrappedUserResponse.GetUserInfo();
                                if (serverUserInfo != null)
                                {
                                    // 映射服务器用户信息到TokenInfo
                                    tokenInfo.UserId = serverUserInfo.Id.ToString();
                                    tokenInfo.UserEmail = serverUserInfo.Email;
                                    tokenInfo.UserName = !string.IsNullOrEmpty(serverUserInfo.Nickname) ? serverUserInfo.Nickname : serverUserInfo.Username;
                                    tokenInfo.UserAvatar = serverUserInfo.Avatar ?? string.Empty;

                                    _logger.LogInformation($"👤 {_config.ProviderName} 用户信息: {tokenInfo.UserName} ({tokenInfo.UserEmail})");
                                    _logger.LogDebug($"🆔 用户ID: {tokenInfo.UserId}");
                                    _logger.LogDebug($"📱 手机号: {serverUserInfo.Mobile}");
                                    _logger.LogDebug($"👥 用户名: {serverUserInfo.Username}");
                                    
                                    if (!string.IsNullOrEmpty(tokenInfo.UserAvatar))
                                    {
                                        _logger.LogDebug($"🖼️ 头像URL: {tokenInfo.UserAvatar}");
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning($"⚠️ 包装响应中的用户数据为空");
                                }
                            }
                            else
                            {
                                var errorMsg = wrappedUserResponse?.Message ?? "未知错误";
                                var errorCode = wrappedUserResponse?.Code ?? -1;
                                _logger.LogWarning($"⚠️ 获取用户信息失败: Code={errorCode}, Message={errorMsg}");
                            }
                        }
                        else
                        {
                            // 尝试解析通用用户信息格式（向后兼容）
                            var userInfo = JsonSerializer.Deserialize<GenericUserInfo>(content, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            if (userInfo != null)
                            {
                                // 使用通用方法获取用户信息
                                tokenInfo.UserId = userInfo.GetUserId();
                                tokenInfo.UserEmail = userInfo.Email ?? string.Empty;
                                tokenInfo.UserName = userInfo.GetUserName();
                                tokenInfo.UserAvatar = userInfo.GetAvatarUrl();

                                _logger.LogInformation($"👤 {_config.ProviderName} 用户信息: {tokenInfo.UserName} ({tokenInfo.UserEmail})");
                                
                                // 记录调试信息
                                if (!string.IsNullOrEmpty(tokenInfo.UserId))
                                {
                                    _logger.LogDebug($"🆔 用户ID: {tokenInfo.UserId}");
                                }
                                if (!string.IsNullOrEmpty(tokenInfo.UserAvatar))
                                {
                                    _logger.LogDebug($"🖼️ 头像URL: {tokenInfo.UserAvatar}");
                                }
                            }
                            else
                            {
                                _logger.LogWarning($"⚠️ 无法解析 {_config.ProviderName} 用户信息响应");
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError($"❌ 解析用户信息JSON失败: {ex.Message}");
                        _logger.LogDebug($"📄 响应内容: {content}");
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"⚠️ 获取 {_config.ProviderName} 用户信息失败: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ 获取 {_config.ProviderName} 用户信息异常: {ex.Message}");
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
                        _logger.LogInformation($"⚠️ GitHub OAuth App不支持刷新令牌，需要重新授权");
                        return null;
                    }
                    
                    throw new Exception("缺少刷新令牌");
                }

                _logger.LogInformation($"🔄 刷新 {_config.ProviderName} 访问令牌...");

                var refreshRequest = new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["client_id"] = _config.ClientId,
                    ["client_secret"] = _config.ClientSecret,
                    ["refresh_token"] = currentToken.RefreshToken
                };

                var content = new FormUrlEncodedContent(refreshRequest);
                
                // 设置请求头 - 使用Basic Authentication (与token exchange保持一致)
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));
                _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "AudioRecorder/1.0");

                // 创建Basic Authentication头
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.ClientId}:{_config.ClientSecret}"));
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

                var response = await _httpClient.PostAsync(_config.TokenEndpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"❌ 令牌刷新失败: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                
                // 使用与token exchange相同的解析逻辑
                TokenInfo? newTokenInfo;
                var contentType = response.Content.Headers.ContentType?.MediaType?.ToLower();
                
                if (contentType?.Contains("application/json") == true || responseContent.TrimStart().StartsWith("{"))
                {
                    // JSON格式响应
                    try
                    {
                        // 首先尝试解析包装的响应格式
                        if (responseContent.Contains("\"code\"") && responseContent.Contains("\"data\""))
                        {
                            var wrappedResponse = JsonSerializer.Deserialize<WrappedTokenResponse>(responseContent, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            if (wrappedResponse != null)
                            {
                                if (wrappedResponse.IsSuccess)
                                {
                                    newTokenInfo = wrappedResponse.GetTokenInfo();
                                    _logger.LogInformation($"✅ 成功解析包装的刷新令牌响应");
                                }
                                else
                                {
                                    _logger.LogInformation($"❌ 刷新令牌服务器返回错误: Code={wrappedResponse.Code}, Message={wrappedResponse.Message}");
                                    return null;
                                }
                            }
                            else
                            {
                                _logger.LogInformation($"❌ 无法解析包装的刷新令牌响应");
                                return null;
                            }
                        }
                        else
                        {
                            // 尝试直接解析TokenInfo格式
                            newTokenInfo = JsonSerializer.Deserialize<TokenInfo>(responseContent, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogInformation($"❌ 刷新令牌JSON解析失败: {ex.Message}");
                        _logger.LogInformation($"📄 响应内容: {responseContent}");
                        return null;
                    }
                }
                else
                {
                    // 尝试作为form-urlencoded格式解析
                    newTokenInfo = ParseFormUrlEncodedTokenResponse(responseContent);
                }

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
                
                // 通知令牌已保存
                TokenSaved?.Invoke(this, newTokenInfo);

                _logger.LogInformation($"✅ 令牌刷新成功，新有效期: {newTokenInfo.ExpiresIn}秒");
                TokenRefreshed?.Invoke(this, newTokenInfo);

                return newTokenInfo;
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"❌ 刷新令牌失败: {ex.Message}");
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
                _logger.LogInformation($"⚠️ 访问令牌已过期: {_config.ProviderName}");
                return false;
            }

            // 检查刷新令牌是否过期（如果有的话）
            if (!string.IsNullOrEmpty(tokenInfo.RefreshToken) && tokenInfo.IsRefreshTokenExpired)
            {
                _logger.LogInformation($"⚠️ 刷新令牌已过期: {_config.ProviderName}");
                return false;
            }

            // 检查是否即将过期（5分钟内）
            if (tokenInfo.IsExpiringSoon)
            {
                _logger.LogInformation($"⚠️ 访问令牌即将过期: {_config.ProviderName}, 剩余时间: {tokenInfo.TimeUntilExpiry.TotalMinutes:F1}分钟");
            }

            return true;
        }

        /// <summary>
        /// 登出（包含单点登出）
        /// </summary>
        public async Task LogoutAsync()
        {
            try
            {
                _logger.LogInformation($"🚪 开始登出 {_config.ProviderName}");

                // 1. 获取当前令牌信息
                var tokenInfo = await _storageManager.LoadTokensAsync(_config.ProviderName);
                
                // 2. 如果有访问令牌且配置了登出端点，执行单点登出
                if (tokenInfo != null && !string.IsNullOrEmpty(tokenInfo.AccessToken) && !string.IsNullOrEmpty(_config.LogoutEndpoint))
                {
                    await PerformSingleSignOutAsync(tokenInfo.AccessToken);
                }

                // 3. 清理本地令牌存储
                await _storageManager.DeleteTokensAsync(_config.ProviderName);
                
                _logger.LogInformation($"✅ {_config.ProviderName} 登出成功");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ {_config.ProviderName} 登出失败: {ex.Message}", ex);
                
                // 即使单点登出失败，也要清理本地令牌
                try
                {
                    await _storageManager.DeleteTokensAsync(_config.ProviderName);
                    _logger.LogInformation("✅ 本地令牌已清理");
                }
                catch (Exception localEx)
                {
                    _logger.LogError($"❌ 清理本地令牌失败: {localEx.Message}", localEx);
                }
            }
        }

        /// <summary>
        /// 执行单点登出
        /// </summary>
        private async Task PerformSingleSignOutAsync(string accessToken)
        {
            try
            {
                _logger.LogInformation($"🔄 开始单点登出: {_config.LogoutEndpoint}");

                // 构建登出URL，包含token参数
                var logoutUrl = $"{_config.LogoutEndpoint}?token={accessToken}";
                
                // 创建Basic Authentication头
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.ClientId}:{_config.ClientSecret}"));
                
                using var request = new HttpRequestMessage(HttpMethod.Delete, logoutUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));
                request.Headers.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
                request.Headers.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("deflate"));
                request.Headers.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("br"));
                request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
                request.Headers.Connection.Add("keep-alive");
                request.Headers.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("AudioRecorder", "1.0"));

                _logger.LogDebug($"📤 单点登出请求: {logoutUrl}");
                _logger.LogDebug($"🔐 Authorization: Basic {credentials}");

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogDebug($"📥 单点登出响应状态: {response.StatusCode}");
                _logger.LogDebug($"📄 单点登出响应内容: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ 单点登出成功");
                }
                else
                {
                    _logger.LogWarning($"⚠️ 单点登出返回非成功状态: {response.StatusCode} - {responseContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ 单点登出请求失败: {ex.Message}", ex);
                throw; // 重新抛出异常，让调用方处理
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

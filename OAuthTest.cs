using System;
using System.Threading.Tasks;
using AudioRecorder.Services;
using AudioRecorder.Models;

namespace AudioRecorder
{
    /// <summary>
    /// OAuth测试程序
    /// </summary>
    public class OAuthTest
    {
        private OAuthLoginService _oauthService;
        private LoggingService _logger;

        public OAuthTest()
        {
            _logger = LoggingService.Instance;
            _oauthService = new OAuthLoginService();
            SubscribeToEvents();
            
            _logger.LogInformation("OAuth测试程序已创建", "OAuthTest");
        }

        /// <summary>
        /// 订阅OAuth事件
        /// </summary>
        private void SubscribeToEvents()
        {
            _oauthService.LoginCompleted += OnLoginCompleted;
            _oauthService.LoginFailed += OnLoginFailed;
            _oauthService.LoginStateRestored += OnLoginStateRestored;
            _oauthService.AuthenticationStatusChanged += OnAuthenticationStatusChanged;
            
            _logger.LogDebug("已订阅OAuth事件", "OAuthTest");
        }

        /// <summary>
        /// 运行测试
        /// </summary>
        public async Task RunTestAsync()
        {
            try
            {
                _logger.LogInformation("🚀 开始OAuth测试...", "OAuthTest");
                Console.WriteLine("🚀 开始OAuth测试...");
                Console.WriteLine();

                // 检查可用的OAuth提供商
                if (!_oauthService.HasAvailableProviders())
                {
                    var message = "没有可用的OAuth提供商";
                    _logger.LogWarning(message, "OAuthTest");
                    Console.WriteLine($"❌ {message}");
                    Console.WriteLine("请检查appsettings.json配置文件中的OAuth设置");
                    return;
                }

                var providers = _oauthService.GetAvailableProviders();
                _logger.LogInformation($"可用的OAuth提供商: {string.Join(", ", providers)}", "OAuthTest");
                Console.WriteLine($"✅ 可用的OAuth提供商: {string.Join(", ", providers)}");
                Console.WriteLine();

                // 尝试恢复登录状态
                _logger.LogInformation("尝试恢复登录状态...", "OAuthTest");
                Console.WriteLine("🔄 尝试恢复登录状态...");
                var restored = await _oauthService.RestoreLoginStateAsync();
                if (restored)
                {
                    _logger.LogInformation("登录状态已恢复", "OAuthTest");
                    Console.WriteLine("✅ 登录状态已恢复");
                    var loggedInProviders = _oauthService.GetLoggedInProviders();
                    foreach (var provider in loggedInProviders)
                    {
                        var token = _oauthService.GetToken(provider);
                        if (token != null)
                        {
                            _logger.LogInformation($"已登录提供商: {provider} - {token.UserName} ({token.UserEmail})", "OAuthTest");
                            Console.WriteLine($"  - {provider}: {token.UserName} ({token.UserEmail})");
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("无有效登录状态，需要手动登录", "OAuthTest");
                    Console.WriteLine("ℹ️ 无有效登录状态，需要手动登录");
                }
                Console.WriteLine();

                // 显示登录状态摘要
                var statusSummary = _oauthService.GetLoginStatusSummary();
                _logger.LogInformation($"登录状态摘要: {statusSummary}", "OAuthTest");
                Console.WriteLine($"📊 登录状态摘要: {statusSummary}");
                Console.WriteLine();

                // 测试GitHub登录
                if (providers.Contains("GitHub"))
                {
                    await TestGitHubLoginAsync();
                }

                // 测试Google登录
                if (providers.Contains("Google"))
                {
                    await TestGoogleLoginAsync();
                }

                _logger.LogInformation("✅ OAuth测试完成", "OAuthTest");
                Console.WriteLine("✅ OAuth测试完成");
            }
            catch (Exception ex)
            {
                _logger.LogError($"OAuth测试过程中发生错误: {ex.Message}", "OAuthTest", ex);
                Console.WriteLine($"❌ OAuth测试过程中发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试GitHub登录
        /// </summary>
        private async Task TestGitHubLoginAsync()
        {
            try
            {
                _logger.LogInformation("🐙 测试GitHub OAuth登录...", "OAuthTest");
                Console.WriteLine("🐙 测试GitHub OAuth登录...");
                
                if (_oauthService.IsLoggedIn("GitHub"))
                {
                    _logger.LogInformation("已登录GitHub，跳过登录测试", "OAuthTest");
                    Console.WriteLine("✅ 已登录GitHub，跳过登录测试");
                    return;
                }

                _logger.LogInformation("等待用户按键开始GitHub登录...", "OAuthTest");
                Console.WriteLine("点击任意键开始GitHub登录...");
                Console.ReadKey();

                var success = await _oauthService.StartGitHubLoginAsync();
                if (success)
                {
                    _logger.LogInformation("GitHub登录流程已启动，请在浏览器中完成授权", "OAuthTest");
                    Console.WriteLine("✅ GitHub登录流程已启动，请在浏览器中完成授权");
                }
                else
                {
                    _logger.LogError("GitHub登录流程启动失败", "OAuthTest");
                    Console.WriteLine("❌ GitHub登录流程启动失败");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"测试GitHub登录失败: {ex.Message}", "OAuthTest", ex);
                Console.WriteLine($"❌ 测试GitHub登录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试Google登录
        /// </summary>
        private async Task TestGoogleLoginAsync()
        {
            try
            {
                _logger.LogInformation("🔍 测试Google OAuth登录...", "OAuthTest");
                Console.WriteLine("🔍 测试Google OAuth登录...");
                
                if (_oauthService.IsLoggedIn("Google"))
                {
                    _logger.LogInformation("已登录Google，跳过登录测试", "OAuthTest");
                    Console.WriteLine("✅ 已登录Google，跳过登录测试");
                    return;
                }

                _logger.LogInformation("等待用户按键开始Google登录...", "OAuthTest");
                Console.WriteLine("点击任意键开始Google登录...");
                Console.ReadKey();

                var success = await _oauthService.StartGoogleLoginAsync();
                if (success)
                {
                    _logger.LogInformation("Google登录流程已启动，请在浏览器中完成授权", "OAuthTest");
                    Console.WriteLine("✅ Google登录流程已启动，请在浏览器中完成授权");
                }
                else
                {
                    _logger.LogError("Google登录流程启动失败", "OAuthTest");
                    Console.WriteLine("❌ Google登录流程启动失败");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"测试Google登录失败: {ex.Message}", "OAuthTest", ex);
                Console.WriteLine($"❌ 测试Google登录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 登录完成事件处理
        /// </summary>
        private void OnLoginCompleted(object? sender, TokenInfo tokenInfo)
        {
            try
            {
                _logger.LogInformation($"登录完成: {tokenInfo.Provider} - {tokenInfo.UserName}", "OAuthTest");
                Console.WriteLine($"✅ 登录完成: {tokenInfo.Provider}");
                Console.WriteLine($"  用户: {tokenInfo.UserName}");
                Console.WriteLine($"  邮箱: {tokenInfo.UserEmail}");
                Console.WriteLine($"  令牌类型: {tokenInfo.TokenType}");
                Console.WriteLine($"  过期时间: {tokenInfo.ExpiresAt:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理登录完成事件失败: {ex.Message}", "OAuthTest", ex);
            }
        }

        /// <summary>
        /// 登录失败事件处理
        /// </summary>
        private void OnLoginFailed(object? sender, string error)
        {
            try
            {
                _logger.LogError($"登录失败: {error}", "OAuthTest");
                Console.WriteLine($"❌ 登录失败: {error}");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理登录失败事件失败: {ex.Message}", "OAuthTest", ex);
            }
        }

        /// <summary>
        /// 登录状态恢复事件处理
        /// </summary>
        private void OnLoginStateRestored(object? sender, TokenInfo tokenInfo)
        {
            try
            {
                _logger.LogInformation($"登录状态已恢复: {tokenInfo.Provider} - {tokenInfo.UserName}", "OAuthTest");
                Console.WriteLine($"🔄 登录状态已恢复: {tokenInfo.Provider} - {tokenInfo.UserName}");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理登录状态恢复事件失败: {ex.Message}", "OAuthTest", ex);
            }
        }

        /// <summary>
        /// 认证状态变化事件处理
        /// </summary>
        private void OnAuthenticationStatusChanged(object? sender, bool enabled)
        {
            try
            {
                var status = enabled ? "启用" : "禁用";
                _logger.LogInformation($"OAuth认证状态已变化: {status}", "OAuthTest");
                Console.WriteLine($"🔄 OAuth认证状态已变化: {status}");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理认证状态变化事件失败: {ex.Message}", "OAuthTest", ex);
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                _logger.LogInformation("开始清理OAuth测试程序资源", "OAuthTest");
                _oauthService?.Dispose();
                _logger.LogInformation("OAuth测试程序资源清理完成", "OAuthTest");
            }
            catch (Exception ex)
            {
                _logger.LogError($"清理OAuth测试程序资源失败: {ex.Message}", "OAuthTest", ex);
            }
        }
    }
}

using System.Text.Json.Serialization;

namespace AudioRecorder.Models
{
    /// <summary>
    /// OAuth配置信息
    /// </summary>
    public class OAuthConfig
    {
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; } = string.Empty;

        [JsonPropertyName("client_secret")]
        public string ClientSecret { get; set; } = string.Empty;

        [JsonPropertyName("redirect_uri")]
        public string RedirectUri { get; set; } = string.Empty;

        [JsonPropertyName("authorization_endpoint")]
        public string AuthorizationEndpoint { get; set; } = string.Empty;

        [JsonPropertyName("token_endpoint")]
        public string TokenEndpoint { get; set; } = string.Empty;

        [JsonPropertyName("scopes")]
        public string[] Scopes { get; set; } = Array.Empty<string>();

        [JsonPropertyName("provider_name")]
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用PKCE（Proof Key for Code Exchange）
        /// </summary>
        [JsonPropertyName("enable_pkce")]
        public bool EnablePkce { get; set; } = false;

        /// <summary>
        /// 授权类型
        /// </summary>
        [JsonPropertyName("response_type")]
        public string ResponseType { get; set; } = "code";

        /// <summary>
        /// 访问类型（用于获取刷新令牌）
        /// </summary>
        [JsonPropertyName("access_type")]
        public string AccessType { get; set; } = "offline";

        /// <summary>
        /// 是否每次都显示同意页面
        /// </summary>
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "consent";
    }

    /// <summary>
    /// GitHub OAuth配置
    /// </summary>
    public static class GitHubOAuthConfig
    {
        public static OAuthConfig Default => new OAuthConfig
        {
            ClientId = "your-github-client-id", // 需要替换为实际的Client ID
            ClientSecret = "your-github-client-secret", // 需要替换为实际的Client Secret
            RedirectUri = "http://localhost:8081/auth/callback",
            AuthorizationEndpoint = "https://github.com/login/oauth/authorize",
            TokenEndpoint = "https://github.com/login/oauth/access_token",
            Scopes = new[] { "user", "user:email" }, // GitHub scopes: user(读取用户信息), user:email(读取邮箱)
            ProviderName = "GitHub",
            EnablePkce = false, // GitHub OAuth App不支持PKCE
            ResponseType = "code",
            AccessType = "offline", // GitHub不支持此参数，但保留用于兼容性
            Prompt = "consent"
        };

        /// <summary>
        /// 创建自定义GitHub OAuth配置
        /// </summary>
        public static OAuthConfig Create(string clientId, string clientSecret, string redirectUri = "http://localhost:8081/auth/callback")
        {
            return new OAuthConfig
            {
                ClientId = clientId,
                ClientSecret = clientSecret,
                RedirectUri = redirectUri,
                AuthorizationEndpoint = "https://github.com/login/oauth/authorize",
                TokenEndpoint = "https://github.com/login/oauth/access_token",
                Scopes = new[] { "user", "user:email" },
                ProviderName = "GitHub",
                EnablePkce = false,
                ResponseType = "code",
                AccessType = "offline",
                Prompt = "consent"
            };
        }
    }

    /// <summary>
    /// Google OAuth配置（保留兼容性）
    /// </summary>
    public static class GoogleOAuthConfig
    {
        public static OAuthConfig Default => new OAuthConfig
        {
            ClientId = "your-google-client-id", // 需要替换为实际的Client ID
            ClientSecret = "your-google-client-secret", // 需要替换为实际的Client Secret
            RedirectUri = "http://localhost:8081/auth/callback",
            AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
            TokenEndpoint = "https://oauth2.googleapis.com/token",
            Scopes = new[] { "openid", "profile", "email" },
            ProviderName = "Google",
            EnablePkce = true, // Google支持PKCE
            ResponseType = "code",
            AccessType = "offline",
            Prompt = "consent"
        };

        /// <summary>
        /// 创建自定义Google OAuth配置
        /// </summary>
        public static OAuthConfig Create(string clientId, string clientSecret, string redirectUri = "http://localhost:8081/auth/callback")
        {
            return new OAuthConfig
            {
                ClientId = clientId,
                ClientSecret = clientSecret,
                RedirectUri = redirectUri,
                AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
                TokenEndpoint = "https://oauth2.googleapis.com/token",
                Scopes = new[] { "openid", "profile", "email" },
                ProviderName = "Google",
                EnablePkce = true,
                ResponseType = "code",
                AccessType = "offline",
                Prompt = "consent"
            };
        }
    }
}

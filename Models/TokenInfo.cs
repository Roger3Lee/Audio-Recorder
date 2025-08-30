using System.Text.Json.Serialization;

namespace AudioRecorder.Models
{
    /// <summary>
    /// OAuth令牌信息
    /// </summary>
    public class TokenInfo
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("id_token")]
        public string IdToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public string Scope { get; set; } = string.Empty;

        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("user_email")]
        public string UserEmail { get; set; } = string.Empty;

        [JsonPropertyName("user_name")]
        public string UserName { get; set; } = string.Empty;

        [JsonPropertyName("user_avatar")]
        public string UserAvatar { get; set; } = string.Empty;

        // 私有字段用于存储实际的过期时间
        private DateTime? _expiresAt;
        private DateTime? _refreshTokenExpiresAt;

        // 计算属性
        [JsonIgnore]
        public DateTime ExpiresAt 
        { 
            get 
            {
                if (!_expiresAt.HasValue && ExpiresIn > 0)
                {
                    _expiresAt = DateTime.UtcNow.AddSeconds(ExpiresIn);
                }
                return _expiresAt ?? DateTime.UtcNow;
            }
            set { _expiresAt = value; }
        }

        [JsonIgnore]
        public DateTime RefreshTokenExpiresAt 
        { 
            get 
            {
                if (!_refreshTokenExpiresAt.HasValue)
                {
                    // GitHub的刷新令牌通常不会过期，但为了安全起见，我们设置一个合理的过期时间
                    _refreshTokenExpiresAt = DateTime.UtcNow.AddDays(90);
                }
                return _refreshTokenExpiresAt.Value;
            }
            set { _refreshTokenExpiresAt = value; }
        }

        [JsonIgnore]
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

        [JsonIgnore]
        public bool IsExpiringSoon => DateTime.UtcNow >= ExpiresAt.AddMinutes(-5);

        [JsonIgnore]
        public bool IsRefreshTokenExpired => DateTime.UtcNow >= RefreshTokenExpiresAt;

        [JsonIgnore]
        public TimeSpan TimeUntilExpiry => ExpiresAt - DateTime.UtcNow;

        /// <summary>
        /// 设置过期时间（用于反序列化后重新计算）
        /// </summary>
        public void RecalculateExpiryTimes()
        {
            if (ExpiresIn > 0)
            {
                _expiresAt = DateTime.UtcNow.AddSeconds(ExpiresIn);
            }
        }
    }

    /// <summary>
    /// GitHub用户信息
    /// </summary>
    public class GitHubUserInfo
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; } = string.Empty;

        [JsonPropertyName("company")]
        public string? Company { get; set; }

        [JsonPropertyName("blog")]
        public string? Blog { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("bio")]
        public string? Bio { get; set; }

        [JsonPropertyName("public_repos")]
        public int PublicRepos { get; set; }

        [JsonPropertyName("public_gists")]
        public int PublicGists { get; set; }

        [JsonPropertyName("followers")]
        public int Followers { get; set; }

        [JsonPropertyName("following")]
        public int Following { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}

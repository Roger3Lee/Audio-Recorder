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

    /// <summary>
    /// 通用OAuth2用户信息
    /// </summary>
    public class GenericUserInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("sub")]
        public string? Sub { get; set; } // OpenID Connect标准字段

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("login")]
        public string? Login { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }

        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }

        [JsonPropertyName("picture")]
        public string? Picture { get; set; }

        [JsonPropertyName("profile_image_url")]
        public string? ProfileImageUrl { get; set; }

        [JsonPropertyName("company")]
        public string? Company { get; set; }

        [JsonPropertyName("organization")]
        public string? Organization { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("bio")]
        public string? Bio { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("website")]
        public string? Website { get; set; }

        [JsonPropertyName("blog")]
        public string? Blog { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// 获取用户ID（优先级：id > sub > username > login）
        /// </summary>
        public string GetUserId()
        {
            return !string.IsNullOrEmpty(Id) ? Id :
                   !string.IsNullOrEmpty(Sub) ? Sub :
                   !string.IsNullOrEmpty(Username) ? Username :
                   !string.IsNullOrEmpty(Login) ? Login :
                   string.Empty;
        }

        /// <summary>
        /// 获取用户名（优先级：name > display_name > username > login）
        /// </summary>
        public string GetUserName()
        {
            return !string.IsNullOrEmpty(Name) ? Name :
                   !string.IsNullOrEmpty(DisplayName) ? DisplayName :
                   !string.IsNullOrEmpty(Username) ? Username :
                   !string.IsNullOrEmpty(Login) ? Login :
                   string.Empty;
        }

        /// <summary>
        /// 获取头像URL（优先级：avatar_url > avatar > picture > profile_image_url）
        /// </summary>
        public string GetAvatarUrl()
        {
            return !string.IsNullOrEmpty(AvatarUrl) ? AvatarUrl :
                   !string.IsNullOrEmpty(Avatar) ? Avatar :
                   !string.IsNullOrEmpty(Picture) ? Picture :
                   !string.IsNullOrEmpty(ProfileImageUrl) ? ProfileImageUrl :
                   string.Empty;
        }
    }

    /// <summary>
    /// 包装的OAuth令牌响应（用于处理服务器返回的包装格式）
    /// </summary>
    public class WrappedTokenResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("data")]
        public TokenInfo? Data { get; set; }

        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 检查响应是否成功
        /// </summary>
        public bool IsSuccess => Code == 0;

        /// <summary>
        /// 获取令牌信息（如果成功的话）
        /// </summary>
        public TokenInfo? GetTokenInfo()
        {
            return IsSuccess ? Data : null;
        }
    }

    /// <summary>
    /// 服务器用户信息（基于你提供的JSON结构）
    /// </summary>
    public class ServerUserInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("mobile")]
        public string Mobile { get; set; } = string.Empty;

        [JsonPropertyName("sex")]
        public int Sex { get; set; }

        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }

        [JsonPropertyName("dept_id")]
        public int? DeptId { get; set; }

        [JsonPropertyName("post_ids")]
        public int[]? PostIds { get; set; }

        [JsonPropertyName("login_ip")]
        public string? LoginIp { get; set; }

        [JsonPropertyName("login_date")]
        public DateTime? LoginDate { get; set; }

        [JsonPropertyName("creator")]
        public string? Creator { get; set; }

        [JsonPropertyName("create_time")]
        public DateTime? CreateTime { get; set; }

        [JsonPropertyName("updater")]
        public string? Updater { get; set; }

        [JsonPropertyName("update_time")]
        public DateTime? UpdateTime { get; set; }

        [JsonPropertyName("tenant_id")]
        public int? TenantId { get; set; }
    }

    /// <summary>
    /// 包装的用户信息响应
    /// </summary>
    public class WrappedUserInfoResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("data")]
        public ServerUserInfo? Data { get; set; }

        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 检查响应是否成功
        /// </summary>
        public bool IsSuccess => Code == 0;

        /// <summary>
        /// 获取用户信息（如果成功的话）
        /// </summary>
        public ServerUserInfo? GetUserInfo()
        {
            return IsSuccess ? Data : null;
        }
    }
}

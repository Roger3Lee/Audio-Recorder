# OAuth 配置更新说明

## 配置变更

将原来的单个 `ServerUrl` 配置改为两个独立的接口配置：

### 旧配置结构
```json
{
    "OAuthSettings": {
        "EnableAuthentication": false,
        "OauthServer": {
            "ServerUrl": "https://your-oauth-server.com",
            "ClientId": "audio_recorder",
            "ClientSecret": "Kj8mN2pQ9vX5wR7sT3uY1zA4bC6dE8fG0hI",
            "RedirectUri": "http://localhost:8081/auth/callback",
            "Scopes": [ "user", "user:email" ]
        }
    }
}
```

### 新配置结构
```json
{
    "OAuthSettings": {
        "EnableAuthentication": false,
        "OauthServer": {
            "AuthorizeUrl": "https://your-oauth-server.com/oauth/authorize",
            "TokenUrl": "https://your-oauth-server.com/oauth/token",
            "ClientId": "audio_recorder",
            "ClientSecret": "Kj8mN2pQ9vX5wR7sT3uY1zA4bC6dE8fG0hI",
            "RedirectUri": "http://localhost:8081/auth/callback",
            "Scopes": [ "user", "user:email" ]
        }
    }
}
```

## 变更说明

1. **移除字段**：`ServerUrl`
2. **新增字段**：
   - `AuthorizeUrl`: 获取授权码的完整 URL 地址
   - `TokenUrl`: 获取访问令牌的完整 URL 地址

## 优势

1. **更灵活的配置**：可以支持不同的授权服务器架构
2. **更明确的接口分离**：明确区分授权和令牌获取接口
3. **更好的兼容性**：支持非标准的 OAuth 服务器实现

## 配置示例

### 标准 OAuth 2.0 服务器
```json
{
    "AuthorizeUrl": "https://oauth.example.com/oauth/authorize",
    "TokenUrl": "https://oauth.example.com/oauth/token"
}
```

### 自定义路径的服务器
```json
{
    "AuthorizeUrl": "https://api.example.com/auth/authorize",
    "TokenUrl": "https://api.example.com/auth/token"
}
```

### GitHub 风格的服务器
```json
{
    "AuthorizeUrl": "https://github.example.com/login/oauth/authorize",
    "TokenUrl": "https://github.example.com/login/oauth/access_token"
}
```

## 迁移指南

如果你已经有旧的配置文件，需要进行以下更新：

1. 将 `ServerUrl` 字段删除
2. 添加 `AuthorizeUrl` 字段，值为 `{原ServerUrl}/oauth/authorize`
3. 添加 `TokenUrl` 字段，值为 `{原ServerUrl}/oauth/token`

### 迁移示例

**旧配置**：
```json
{
    "ServerUrl": "https://my-oauth-server.com"
}
```

**新配置**：
```json
{
    "AuthorizeUrl": "https://my-oauth-server.com/oauth/authorize",
    "TokenUrl": "https://my-oauth-server.com/oauth/token"
}
```

## 代码变更

相关的代码变更包括：

1. `Services/ConfigurationService.cs` 中的 `OAuthServerConfig` 类
2. `GetGenericOAuthConfig()` 方法的实现
3. OAuth 配置完整性检查逻辑

这些变更确保了应用程序能够正确处理新的配置结构。
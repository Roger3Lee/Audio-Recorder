# Google OAuth 使用说明

## 📋 概述

本文档说明如何在AudioRecorder应用中使用Google OAuth 2.0进行用户身份验证。

## 🚀 快速开始

### 1. 获取Google OAuth凭据

#### 步骤1: 访问Google Cloud Console
1. 打开 [Google Cloud Console](https://console.cloud.google.com/)
2. 创建新项目或选择现有项目

#### 步骤2: 启用OAuth 2.0 API
1. 在左侧菜单中选择 "API和服务" > "库"
2. 搜索并启用 "Google+ API" 或 "Google Identity API"

#### 步骤3: 创建OAuth 2.0凭据
1. 在左侧菜单中选择 "API和服务" > "凭据"
2. 点击 "创建凭据" > "OAuth 2.0 客户端ID"
3. 选择应用类型: "桌面应用"
4. 填写应用名称: "AudioRecorder"
5. 点击 "创建"

#### 步骤4: 获取凭据信息
创建完成后，您将获得：
- **客户端ID** (Client ID)
- **客户端密钥** (Client Secret)

### 2. 配置应用

#### 方法1: 修改代码配置
编辑 `Models/GoogleOAuthConfig.cs` 文件：

```csharp
public static class GoogleOAuthConfig
{
    public static OAuthConfig Default => new OAuthConfig
    {
        ClientId = "你的客户端ID",           // 替换这里
        ClientSecret = "你的客户端密钥",     // 替换这里
        RedirectUri = "http://localhost:8081/auth/callback",
        AuthorizationEndpoint = "https://accounts.google.com/oauth/authorize",
        TokenEndpoint = "https://oauth2.googleapis.com/token",
        Scopes = new[] { "openid", "email", "profile" },
        ProviderName = "Google"
    };
}
```

#### 方法2: 使用配置文件
编辑 `appsettings.json` 文件：

```json
{
  "GoogleOAuth": {
    "ClientId": "你的客户端ID",
    "ClientSecret": "你的客户端密钥",
    "RedirectUri": "http://localhost:8081/auth/callback",
    "Scopes": ["openid", "email", "profile"]
  }
}
```

### 3. 运行应用

1. 编译并运行AudioRecorder应用
2. 点击模态二中的 "登录Google账户" 按钮
3. 系统将自动打开浏览器进行Google授权
4. 完成授权后，应用将显示用户信息

## 🔧 技术实现

### 架构组件

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   RecorderWindow│    │ Authorization    │    │ LocalHttpServer │
│                 │    │ Manager          │    │                 │
│ - 登录按钮      │◄──►│ - OAuth流程      │◄──►│ - 接收回调      │
│ - 用户信息显示  │    │ - 令牌管理       │    │ - 响应页面      │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                │
                                ▼
                       ┌──────────────────┐
                       │ TokenManager     │
                       │                  │
                       │ - 令牌存储       │
                       │ - 自动刷新       │
                       │ - 状态恢复       │
                       └──────────────────┘
```

### 工作流程

1. **用户点击登录**
   - 启动本地HTTP服务器 (端口8081)
   - 构建Google授权URL
   - 打开浏览器访问授权页面

2. **Google授权**
   - 用户在Google页面登录并授权
   - Google重定向到本地回调地址
   - 本地服务器接收授权码

3. **令牌交换**
   - 使用授权码交换访问令牌
   - 获取用户信息 (姓名、邮箱、头像)
   - 保存令牌到安全存储

4. **状态管理**
   - 启动自动令牌刷新
   - 程序重启时自动恢复登录状态
   - 令牌过期前自动刷新

## 📱 用户界面

### 登录前状态
```
┌─────────────────────────────────────┐
│           [登录Google账户]          │
└─────────────────────────────────────┘
```

### 登录后状态
```
┌─────────────────────────────────────┐
│ [头像] 用户名                       │
│        user@example.com            │
│           [登出]                    │
└─────────────────────────────────────┘
```

## 🔐 安全特性

### 令牌存储
- 使用Windows凭据管理器安全存储
- 本地文件备份 (AppData/AudioRecorder/tokens/)
- 加密存储敏感信息

### 自动刷新
- 访问令牌过期前5分钟自动刷新
- 刷新令牌90天有效期
- 网络中断后自动重试

### 权限范围
- **openid**: 用户身份标识
- **email**: 用户邮箱地址
- **profile**: 用户基本信息

## 🚨 故障排除

### 常见问题

#### 1. "无法启动本地HTTP服务器"
**原因**: 端口8081被占用
**解决**: 
- 修改 `LocalHttpServer.cs` 中的端口号
- 检查防火墙设置
- 关闭占用端口的其他应用

#### 2. "授权失败: invalid_client"
**原因**: 客户端ID或密钥错误
**解决**:
- 检查Google Cloud Console中的凭据
- 确认应用类型为"桌面应用"
- 重新生成客户端密钥

#### 3. "重定向URI不匹配"
**原因**: 回调地址配置错误
**解决**:
- 确保重定向URI为 `http://localhost:8081/auth/callback`
- 检查Google Cloud Console中的授权重定向URI

#### 4. "令牌交换失败"
**原因**: 网络问题或API限制
**解决**:
- 检查网络连接
- 确认Google API已启用
- 检查API配额限制

### 调试信息

应用运行时会在控制台输出详细的调试信息：

```
🚀 开始 Google OAuth授权流程
🌐 本地HTTP服务器已启动: http://localhost:8081
📡 回调路径: /auth/callback
🔗 授权URL: https://accounts.google.com/oauth/authorize?...
🌐 已在默认浏览器中打开授权页面
📥 收到授权码，开始交换令牌...
✅ 令牌交换成功，有效期: 3600秒
👤 用户信息: 张三 (zhangsan@gmail.com)
✅ Google授权完成！
```

## 📚 相关文档

- [Google OAuth 2.0 文档](https://developers.google.com/identity/protocols/oauth2)
- [Google Identity API](https://developers.google.com/identity)
- [OAuth 2.0 规范](https://tools.ietf.org/html/rfc6749)

## 🔄 更新日志

### v1.0.0 (2024-12)
- 初始版本发布
- 支持Google OAuth 2.0
- 自动令牌刷新
- 登录状态恢复
- 安全令牌存储

---

**注意**: 请确保在生产环境中使用真实的Google OAuth凭据，并遵循Google的安全最佳实践。

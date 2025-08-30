# GitHub OAuth 设置说明

本文档说明如何在AudioRecorder应用中配置GitHub OAuth登录。

## 1. 创建GitHub OAuth应用

### 1.1 访问GitHub开发者设置
1. 登录GitHub账户
2. 点击右上角头像 → Settings
3. 左侧菜单选择 "Developer settings"
4. 选择 "OAuth Apps"
5. 点击 "New OAuth App"

### 1.2 填写OAuth应用信息
```
Application name: AudioRecorder
Homepage URL: http://localhost:8081
Application description: Audio Recorder with OAuth login
Authorization callback URL: http://localhost:8081/auth/callback
```

### 1.3 获取Client ID和Client Secret
创建完成后，你会看到：
- **Client ID**: 一个长字符串（公开信息）
- **Client Secret**: 点击"Generate a new client secret"生成（保密信息）

## 2. 配置应用

### 2.1 编辑配置文件
打开 `appsettings.json` 文件，找到OAuth配置部分：

```json
{
  "OAuth": {
    "GitHub": {
      "ClientId": "你的GitHub Client ID",
      "ClientSecret": "你的GitHub Client Secret",
      "RedirectUri": "http://localhost:8081/auth/callback",
      "Scopes": ["user", "user:email"]
    }
  }
}
```

### 2.2 替换配置值
- 将 `"你的GitHub Client ID"` 替换为实际的Client ID
- 将 `"你的GitHub Client Secret"` 替换为实际的Client Secret

## 3. GitHub OAuth流程说明

### 3.1 授权流程
1. 用户点击"GitHub登录"按钮
2. 应用启动本地HTTP服务器（端口8081）
3. 打开浏览器，跳转到GitHub授权页面
4. 用户在GitHub上授权应用访问
5. GitHub重定向到本地回调地址
6. 应用接收授权码，交换访问令牌
7. 获取用户信息，完成登录

### 3.2 权限范围（Scopes）
- `user`: 读取用户基本信息
- `user:email`: 读取用户邮箱信息

### 3.3 令牌特性
- **访问令牌**: 有效期1小时
- **刷新令牌**: GitHub OAuth App不支持，需要重新授权
- **用户信息**: 包括用户名、邮箱、头像等

## 4. 安全注意事项

### 4.1 保护Client Secret
- 永远不要将Client Secret提交到版本控制系统
- 在生产环境中使用环境变量或安全的配置管理
- 定期轮换Client Secret

### 4.2 回调URL验证
- GitHub会验证回调URL是否与注册的一致
- 确保回调URL完全匹配，包括协议、主机、端口和路径

### 4.3 状态参数
- 应用使用state参数防止CSRF攻击
- 每次授权都会生成新的随机state值

## 5. 故障排除

### 5.1 常见错误
- **"redirect_uri_mismatch"**: 回调URL不匹配
- **"invalid_client"**: Client ID或Client Secret错误
- **"invalid_grant"**: 授权码已使用或过期

### 5.2 调试步骤
1. 检查配置文件中的Client ID和Client Secret
2. 确认回调URL完全匹配
3. 查看应用控制台输出的详细错误信息
4. 检查本地HTTP服务器是否正常启动

### 5.3 网络问题
- 确保能够访问GitHub API
- 检查防火墙设置，确保端口8081可用
- 如果使用代理，确保代理配置正确

## 6. 测试

### 6.1 基本测试
1. 启动应用
2. 点击"GitHub登录"按钮
3. 在浏览器中完成GitHub授权
4. 检查应用是否显示用户信息

### 6.2 令牌验证
1. 登录成功后，检查令牌是否保存
2. 重启应用，验证登录状态是否恢复
3. 等待令牌过期，测试自动刷新机制

## 7. 生产环境部署

### 7.1 更新回调URL
在生产环境中，需要更新GitHub OAuth应用的配置：
- 将Homepage URL改为生产环境域名
- 将Authorization callback URL改为生产环境回调地址

### 7.2 环境变量
建议使用环境变量存储敏感信息：
```bash
export GITHUB_CLIENT_ID="your-client-id"
export GITHUB_CLIENT_SECRET="your-client-secret"
```

### 7.3 HTTPS要求
生产环境必须使用HTTPS，GitHub不允许HTTP回调URL。

## 8. 相关链接

- [GitHub OAuth Apps文档](https://docs.github.com/en/developers/apps/building-oauth-apps)
- [GitHub API文档](https://docs.github.com/en/rest)
- [OAuth 2.0标准](https://tools.ietf.org/html/rfc6749)

## 9. 更新日志

- **v1.0.0**: 初始版本，支持基本的GitHub OAuth登录
- 支持用户信息获取
- 支持邮箱信息获取
- 本地令牌存储和自动刷新

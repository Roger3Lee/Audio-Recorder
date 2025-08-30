# 🚀 OAuth 快速启动指南

本指南将帮助你在5分钟内启动GitHub OAuth登录功能。

## ⚡ 快速开始

### 1. 创建GitHub OAuth应用 (2分钟)

1. 访问 [GitHub OAuth Apps](https://github.com/settings/developers)
2. 点击 "New OAuth App"
3. 填写信息：
   - **Application name**: `AudioRecorder`
   - **Homepage URL**: `http://localhost:8081`
   - **Authorization callback URL**: `http://localhost:8081/auth/callback`
4. 点击 "Register application"
5. 复制 **Client ID** 和 **Client Secret**

### 2. 配置应用 (1分钟)

编辑 `appsettings.json` 文件：

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

### 3. 测试OAuth功能 (2分钟)

运行测试程序：

```bash
# Windows
test_oauth.bat

# 或手动运行
dotnet build
dotnet run
```

## 🎯 预期结果

成功运行后，你应该看到：

```
🚀 开始OAuth测试...
✅ 可用的OAuth提供商: GitHub
🔄 尝试恢复登录状态...
ℹ️ 无有效登录状态，需要手动登录
🐙 测试GitHub OAuth登录...
点击任意键开始GitHub登录...
✅ GitHub登录流程已启动，请在浏览器中完成授权
```

## 🔍 验证步骤

1. **浏览器自动打开** GitHub授权页面
2. **点击 "Authorize AudioRecorder"**
3. **查看控制台输出** 登录成功信息
4. **检查用户信息** 显示用户名和邮箱

## ❗ 常见问题

### Q: 浏览器没有自动打开？
A: 手动复制控制台中的授权URL到浏览器

### Q: 显示 "redirect_uri_mismatch"？
A: 检查GitHub OAuth应用的回调URL设置

### Q: 端口8081被占用？
A: 修改配置文件中的端口号，同时更新GitHub OAuth应用设置

## 📚 下一步

- 阅读 `README_GitHub_OAuth_Setup.md` 了解详细配置
- 查看 `README_OAuth_Implementation.md` 了解技术实现
- 集成到主应用程序界面

## 🆘 需要帮助？

如果遇到问题，请检查：
1. 配置文件格式是否正确
2. GitHub OAuth应用设置是否匹配
3. 网络连接是否正常
4. 控制台错误信息

---

**恭喜！** 🎉 你现在已经成功配置了GitHub OAuth登录功能！

using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using AudioRecorder.Models;

namespace AudioRecorder.Services
{
    /// <summary>
    /// 系统托盘管理器
    /// </summary>
    public class SystemTrayManager : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private ContextMenuStrip? _contextMenu;
        private readonly ILogger _logger;
        private readonly SecureStorageManager _storageManager;
        private TokenInfo? _currentUser;

        // 事件
        public event EventHandler? ShowWindowRequested;
        public event EventHandler? ExitApplicationRequested;
        public event EventHandler? LogoutRequested;

        public SystemTrayManager()
        {
            _logger = LoggingServiceManager.CreateLogger("SystemTrayManager");
            _storageManager = new SecureStorageManager();
            InitializeTrayIcon();
        }

        /// <summary>
        /// 初始化系统托盘图标
        /// </summary>
        private void InitializeTrayIcon()
        {
            try
            {
                _logger.LogInformation("初始化系统托盘图标");

                // 创建托盘图标
                _notifyIcon = new NotifyIcon();
                _notifyIcon.Icon = GetApplicationIcon();
                _notifyIcon.Text = "Audio Recorder";
                _notifyIcon.Visible = true;

                // 双击事件 - 显示主窗口
                _notifyIcon.DoubleClick += (sender, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);

                // 创建右键菜单
                CreateContextMenu();

                _logger.LogInformation("✅ 系统托盘图标初始化成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 初始化系统托盘图标失败");
            }
        }

        /// <summary>
        /// 创建右键上下文菜单
        /// </summary>
        private void CreateContextMenu()
        {
            _contextMenu = new ContextMenuStrip();

            // 用户信息区域
            var userInfoItem = new ToolStripMenuItem();
            userInfoItem.Name = "UserInfo";
            userInfoItem.Enabled = false; // 不可点击，仅显示信息
            _contextMenu.Items.Add(userInfoItem);

            // 用户信息后的分隔线
            var userSeparator = new ToolStripSeparator();
            userSeparator.Name = "UserSeparator";
            _contextMenu.Items.Add(userSeparator);

            // 退出登录菜单项（仅在登录时显示）
            var logoutItem = new ToolStripMenuItem("退出登录");
            logoutItem.Name = "Logout";
            logoutItem.Image = GetMenuIcon("logout");
            logoutItem.Click += (sender, e) => LogoutRequested?.Invoke(this, EventArgs.Empty);
            _contextMenu.Items.Add(logoutItem);

            // 退出应用菜单项
            var exitItem = new ToolStripMenuItem("退出应用");
            exitItem.Name = "Exit";
            exitItem.Image = GetMenuIcon("exit");
            exitItem.Click += (sender, e) => ExitApplicationRequested?.Invoke(this, EventArgs.Empty);
            _contextMenu.Items.Add(exitItem);

            // 设置右键菜单
            _notifyIcon.ContextMenuStrip = _contextMenu;

            // 初始更新菜单状态
            UpdateMenuState();
        }

        /// <summary>
        /// 更新用户登录状态
        /// </summary>
        /// <param name="tokenInfo">用户令牌信息，null表示未登录</param>
        public void UpdateUserStatus(TokenInfo? tokenInfo)
        {
            try
            {
                _currentUser = tokenInfo;
                UpdateMenuState();
                UpdateTrayTooltip();
                
                if (tokenInfo != null)
                {
                    _logger.LogInformation($"更新托盘用户状态: {tokenInfo.UserName} ({tokenInfo.Provider})");
                }
                else
                {
                    _logger.LogInformation("更新托盘用户状态: 未登录");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新用户状态失败");
            }
        }

        /// <summary>
        /// 更新菜单状态
        /// </summary>
        private void UpdateMenuState()
        {
            if (_contextMenu == null) return;

            try
            {
                // 更新用户信息显示
                var userInfoItem = _contextMenu.Items["UserInfo"] as ToolStripMenuItem;
                if (userInfoItem != null)
                {
                    if (_currentUser != null)
                    {
                        userInfoItem.Text = $"{_currentUser.UserName}";
                        userInfoItem.Image = GetUserAvatar(_currentUser);
                        userInfoItem.Visible = true;
                    }
                    else
                    {
                        userInfoItem.Text = "未登录";
                        userInfoItem.Image = GetMenuIcon("user");
                        userInfoItem.Visible = true;
                    }
                }

                // 根据登录状态显示/隐藏退出登录相关菜单项
                var logoutItem = _contextMenu.Items["Logout"] as ToolStripMenuItem;
                if (logoutItem != null)
                {
                    logoutItem.Visible = _currentUser != null; // 只有登录时才显示
                    logoutItem.Enabled = _currentUser != null;
                }

                // 更新用户信息分隔线的显示状态
                var userSeparator = _contextMenu.Items["UserSeparator"] as ToolStripSeparator;
                if (userSeparator != null)
                {
                    userSeparator.Visible = _currentUser != null; // 只有登录时才显示
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新菜单状态失败");
            }
        }

        /// <summary>
        /// 更新托盘提示文本
        /// </summary>
        private void UpdateTrayTooltip()
        {
            if (_notifyIcon == null) return;

            try
            {
                if (_currentUser != null)
                {
                    _notifyIcon.Text = $"AudioRecorder - {_currentUser.UserName}";
                }
                else
                {
                    _notifyIcon.Text = "AudioRecorder - 未登录";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新托盘提示失败");
            }
        }

        /// <summary>
        /// 显示托盘通知
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="message">消息内容</param>
        /// <param name="icon">图标类型</param>
        /// <param name="timeout">显示时间（毫秒）</param>
        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000)
        {
            try
            {
                _notifyIcon?.ShowBalloonTip(timeout, title, message, icon);
                _logger.LogInformation($"显示托盘通知: {title} - {message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "显示托盘通知失败");
            }
        }

        /// <summary>
        /// 获取应用程序图标
        /// </summary>
        /// <returns>应用程序图标</returns>
        private Icon GetApplicationIcon()
        {
            try
            {
                // 尝试从资源或文件加载图标
                var assembly = Assembly.GetExecutingAssembly();
                var iconStream = assembly.GetManifestResourceStream("AudioRecorder.Resources.app.ico");
                
                if (iconStream != null)
                {
                    return new Icon(iconStream);
                }

                // 如果没有找到资源图标，尝试从文件加载
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }

                // 使用系统默认图标
                return SystemIcons.Application;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "加载应用程序图标失败，使用默认图标");
                return SystemIcons.Application;
            }
        }

        /// <summary>
        /// 获取菜单图标
        /// </summary>
        /// <param name="iconName">图标名称</param>
        /// <returns>菜单图标</returns>
        private Image? GetMenuIcon(string iconName)
        {
            try
            {
                // 创建简单的16x16像素图标
                var bitmap = new Bitmap(16, 16);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Transparent);
                    
                    switch (iconName.ToLower())
                    {
                        case "record":
                            // 录制图标 - 红色圆点
                            using (var brush = new SolidBrush(Color.Red))
                            {
                                g.FillEllipse(brush, 2, 2, 12, 12);
                            }
                            break;
                            
                        case "logout":
                            // 退出登录图标 - 简单的箭头
                            using (var pen = new Pen(Color.Gray, 2))
                            {
                                g.DrawLine(pen, 2, 8, 10, 8);
                                g.DrawLine(pen, 7, 5, 10, 8);
                                g.DrawLine(pen, 7, 11, 10, 8);
                                g.DrawRectangle(pen, 11, 4, 3, 8);
                            }
                            break;
                            
                        case "exit":
                            // 退出应用图标 - X
                            using (var pen = new Pen(Color.Red, 2))
                            {
                                g.DrawLine(pen, 4, 4, 12, 12);
                                g.DrawLine(pen, 12, 4, 4, 12);
                            }
                            break;
                            
                        case "user":
                            // 用户图标 - 简单的人形
                            using (var pen = new Pen(Color.Gray, 1))
                            {
                                g.DrawEllipse(pen, 6, 2, 4, 4); // 头
                                g.DrawEllipse(pen, 4, 7, 8, 7); // 身体
                            }
                            break;
                    }
                }
                return bitmap;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"创建菜单图标失败: {iconName}");
                return null;
            }
        }

        /// <summary>
        /// 获取用户头像
        /// </summary>
        /// <param name="tokenInfo">用户令牌信息</param>
        /// <returns>用户头像图片</returns>
        private Image? GetUserAvatar(TokenInfo tokenInfo)
        {
            try
            {
                // 这里可以实现从URL下载头像的逻辑
                // 暂时返回默认用户图标
                return GetMenuIcon("user");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取用户头像失败");
                return GetMenuIcon("user");
            }
        }

        /// <summary>
        /// 隐藏托盘图标
        /// </summary>
        public void Hide()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
            }
        }

        /// <summary>
        /// 显示托盘图标
        /// </summary>
        public void Show()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = true;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                _logger.LogInformation("释放系统托盘资源");
                
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }

                if (_contextMenu != null)
                {
                    _contextMenu.Dispose();
                    _contextMenu = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "释放系统托盘资源失败");
            }
        }
    }
}
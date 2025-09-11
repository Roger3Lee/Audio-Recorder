using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using AudioRecorder.Services;
using AudioRecorder.Models;
using WpfMessageBox = System.Windows.MessageBox;
using WpfPoint = System.Windows.Point;
using System.Threading.Tasks; // Added for Task.Run
using Microsoft.Extensions.Logging;
using System.Drawing; // Added for Graphics.FromHwnd

namespace AudioRecorder
{
    public partial class RecorderWindow : Window
    {
        private SimpleAudioRecorder? recorder;
        private SimpleWebSocketServer? webSocketServer;
        private AudioFileUploadService? uploadService;
        private IPCManager? ipcManager;
        private SystemTrayManager? systemTrayManager;
        
        // OAuth相关字段
        private OAuthLoginService? oauthService;
        private bool isLoggedIn = false;
        private string? currentProvider = null;
        
        private bool isRecording = false;
        private bool isPaused = false;
        private bool isLargeWindow = false; // true=模态二(大窗口), false=模态一(小窗口)
        private bool isStopConfirming = false; // 控制停止确认覆盖层的显示状态
        
        // 日志记录器
        private readonly ILogger _logger;
        
        // 模态尺寸
        private readonly System.Windows.Size Modal1Size = new System.Windows.Size(200, 50);
        private readonly System.Windows.Size Modal2Size = new System.Windows.Size(200, 200);
        private readonly System.Windows.Size Modal3Size = new System.Windows.Size(200, 150);

        public RecorderWindow()
        {
            _logger = LoggingServiceManager.CreateLogger("RecorderWindow");
            
            InitializeComponent();
            
            // 订阅URL协议事件
            UrlProtocolHandler.ProtocolActionReceived += OnProtocolActionReceived;
            
            // 设置窗口属性
            this.Topmost = true;
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = System.Windows.Media.Brushes.Transparent;
            
            // 确保窗口可见
            this.ShowInTaskbar = false;
            this.Visibility = System.Windows.Visibility.Visible;
            
            // 延迟设置窗口位置，确保窗口完全初始化后再设置
            this.Loaded += (s, e) => SetWindowPosition();
            
            // 窗口状态变化事件 - 最小化时隐藏到托盘
            this.StateChanged += OnWindowStateChanged;
            
            // 初始化组件
            InitializeRecorder();
            InitializeOAuth();
            InitializeWebSocket();
            InitializeIPC();
            InitializeSystemTray();
            LoadIcons();
            
            // 根据OAuth认证状态和登录状态决定初始显示
            var config = ConfigurationService.Instance;
            if (!config.IsOAuthEnabled())
            {
                // OAuth未启用，直接显示模态1（录音状态）
                ShowModal1();
            }
            else
            {
                // OAuth已启用，尝试恢复登录状态
                _ = Task.Run(async () =>
                {
                    await RestoreLoginStateAsync();
                    
                    // 在UI线程上更新界面
                    Dispatcher.Invoke(() =>
                    {
                        if (isLoggedIn)
                        {
                            ShowModal1();
                            HideLoginPanel();
                        }
                        else
                        {
                            ShowLoginPanel();
                            ShowModal3(); // 显示模态3以显示登录状态
                        }
                    });
                });
                
                // 暂时显示模态3和登录面板，等待登录状态恢复
                ShowLoginPanel();
                ShowModal3();
            }
            
            // 设置拖拽
            this.MouseLeftButtonDown += (s, e) => this.DragMove();
            
            // 添加DPI测试快捷键 (Ctrl+D)
            this.KeyDown += (s, e) => 
            {
                if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    TestDpiDetection();
                }
            };
        }

        private void InitializeRecorder()
        {
            recorder = new SimpleAudioRecorder();
            recorder.StatusChanged += OnStatusChanged;
            recorder.ErrorOccurred += OnErrorOccurred;

            // 初始化上传服务
            try
            {
                var config = ConfigurationService.Instance;
                if (config.UploadSettings.EnableAutoUpload)
                {
                    uploadService = new AudioFileUploadService(config.UploadSettings);
                    uploadService.UploadProgressChanged += OnUploadProgressChanged;
                    uploadService.UploadErrorOccurred += OnUploadErrorOccurred;
                    uploadService.UploadCompleted += OnUploadCompleted;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "初始化上传服务失败");
            }
        }

        private void InitializeOAuth()
        {
            try
            {
                // 检查OAuth认证是否启用
                var config = ConfigurationService.Instance;
                if (!config.IsOAuthEnabled())
                {
                    _logger.LogInformation("OAuth认证已禁用，跳过OAuth初始化");
                    return;
                }

                // 初始化OAuth服务
                oauthService = new OAuthLoginService();
                
                // 订阅事件
                oauthService.LoginCompleted += OnOAuthLoginCompleted;
                oauthService.LoginFailed += OnOAuthLoginFailed;
                oauthService.LoginStateRestored += OnOAuthLoginStateRestored;
                
                _logger.LogInformation("OAuth授权系统初始化成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OAuth授权系统初始化失败");
            }
        }

        // OAuth事件处理方法
        private void OnOAuthLoginCompleted(object? sender, TokenInfo tokenInfo)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _logger.LogInformation($"✅ OAuth登录完成事件触发 - Provider: {tokenInfo.Provider}, User: {tokenInfo.UserName}");
                
                isLoggedIn = true;
                currentProvider = tokenInfo.Provider;
                
                _logger.LogDebug($"设置登录状态 - isLoggedIn: {isLoggedIn}, currentProvider: {currentProvider}");
                
                UpdateLoginUI(tokenInfo);
                UpdateTrayUserStatus();
                
                // 显示登录成功通知
                systemTrayManager?.ShowNotification("AudioRecorder", $"{tokenInfo.Provider} 登录成功", System.Windows.Forms.ToolTipIcon.Info);
                
                _logger.LogInformation($"✅ {tokenInfo.Provider}授权完成: {tokenInfo.UserName}");
            });
        }

        private void OnOAuthLoginFailed(object? sender, string error)
        {
            Dispatcher.InvokeAsync(() =>
            {
                var failedProvider = currentProvider; // 保存失败的提供商名称
                
                isLoggedIn = false;
                currentProvider = null;
                UpdateLoginUI(null);
                UpdateTrayUserStatus();
                
                // 显示登录失败通知
                systemTrayManager?.ShowNotification("AudioRecorder", "登录失败", System.Windows.Forms.ToolTipIcon.Error);
                
                _logger.LogInformation($"❌ {failedProvider ?? "未知提供商"}授权失败: {error}");
                WpfMessageBox.Show($"{failedProvider ?? "OAuth"}授权失败: {error}", "授权失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        private void OnOAuthLoginStateRestored(object? sender, TokenInfo tokenInfo)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _logger.LogInformation($"🔄 OAuth登录状态恢复事件触发 - Provider: {tokenInfo.Provider}, User: {tokenInfo.UserName}");
                
                isLoggedIn = true;
                currentProvider = tokenInfo.Provider;
                
                _logger.LogDebug($"恢复登录状态 - isLoggedIn: {isLoggedIn}, currentProvider: {currentProvider}");
                
                UpdateLoginUI(tokenInfo);
                UpdateTrayUserStatus();
                _logger.LogInformation($"🔄 登录状态已恢复: {tokenInfo.Provider} - {tokenInfo.UserName}");
            });
        }

        /// <summary>
        /// 登录按钮点击事件
        /// </summary>
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (oauthService != null)
                {
                    // 检查可用的OAuth提供商
                    var providers = oauthService.GetAvailableProviders();
                    if (providers.Count == 0)
                    {
                        WpfMessageBox.Show("没有可用的OAuth提供商", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 如果有多个提供商，可以选择，这里暂时使用第一个
                    var provider = providers[0];
                    // 注意：不要在这里设置 currentProvider，应该在登录成功后设置
                    
                    _logger.LogInformation($"🚀 开始{provider} OAuth登录流程");
                    var success = await oauthService.StartLoginAsync(provider);
                    if (!success)
                    {
                        WpfMessageBox.Show($"启动{provider}登录失败", "登录失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        // 登录失败时确保 currentProvider 为 null
                        currentProvider = null;
                    }
                }
                else
                {
                    WpfMessageBox.Show("OAuth系统未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"❌ 登录按钮点击事件处理失败: {ex.Message}");
                WpfMessageBox.Show($"登录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                // 异常时确保 currentProvider 为 null
                currentProvider = null;
            }
        }

        /// <summary>
        /// 更新登录UI状态
        /// </summary>
        private void UpdateLoginUI(TokenInfo? tokenInfo)
        {
            // 检查OAuth认证是否启用
            var config = ConfigurationService.Instance;
            if (!config.IsOAuthEnabled())
            {
                // OAuth未启用，直接显示模态1（录音状态）
                isLoggedIn = false;
                currentProvider = null;
                ShowModal1();
                return;
            }

            if (tokenInfo != null)
            {
                isLoggedIn = true;
                currentProvider = tokenInfo.Provider;
                // 已登录，隐藏登录面板，显示模态1（录音状态）
                HideLoginPanel();
                ShowModal1();
            }
            else
            {
                isLoggedIn = false;
                currentProvider = null;
                // 未登录，显示登录面板和模态3（登录状态）
                ShowLoginPanel();
                ShowModal3();
            }
        }

        /// <summary>
        /// 恢复登录状态
        /// </summary>
        private async Task RestoreLoginStateAsync()
        {
            try
            {
                if (oauthService != null)
                {
                    var restored = await oauthService.RestoreLoginStateAsync();
                    if (restored)
                    {
                        _logger.LogInformation("✅ 登录状态恢复成功");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"❌ 恢复登录状态失败: {ex.Message}");
            }
        }

        #region 系统托盘事件处理

        /// <summary>
        /// 托盘显示窗口请求
        /// </summary>
        private void OnTrayShowWindowRequested(object? sender, EventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // 显示并激活窗口
                    if (this.WindowState == System.Windows.WindowState.Minimized)
                    {
                        this.WindowState = System.Windows.WindowState.Normal;
                    }
                    
                    this.Show();
                    this.Activate();
                    this.Topmost = true;
                    this.Topmost = false; // 临时设置Topmost来确保窗口显示在前面
                    
                    _logger.LogInformation("从托盘恢复窗口显示");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从托盘恢复窗口失败");
            }
        }

        /// <summary>
        /// 托盘退出应用请求
        /// </summary>
        private void OnTrayExitApplicationRequested(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("用户从托盘请求退出应用");
                
                Dispatcher.Invoke(() =>
                {
                    // 如果正在录制，先停止录制
                    if (isRecording && recorder != null)
                    {
                        _logger.LogInformation("正在录音，先停止录制再退出");
                        ShowStopConfirmOverlay();
                    }
                    else
                    {
                        CloseApplication();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "托盘退出应用失败");
                CloseApplication();
            }
        }

        /// <summary>
        /// 托盘退出登录请求
        /// </summary>
        private void OnTrayLogoutRequested(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("用户从托盘请求退出登录");
                
                Dispatcher.Invoke(async () =>
                {
                    await PerformLogoutAsync();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "托盘退出登录失败");
            }
        }

        /// <summary>
        /// 执行退出登录
        /// </summary>
        private async Task PerformLogoutAsync()
        {
            try
            {
                var logoutProvider = currentProvider; // 保存要退出的提供商
                _logger.LogInformation($"开始执行退出登录 - Provider: {logoutProvider ?? "null"}");
                
                if (oauthService != null && !string.IsNullOrEmpty(logoutProvider))
                {
                    await oauthService.LogoutAsync(logoutProvider);
                    
                    // 更新UI状态
                    isLoggedIn = false;
                    currentProvider = null;
                    
                    _logger.LogDebug($"退出登录后状态 - isLoggedIn: {isLoggedIn}, currentProvider: {currentProvider ?? "null"}");
                    
                    // 确保窗口可见并激活
                    if (this.WindowState == System.Windows.WindowState.Minimized || !this.IsVisible)
                    {
                        this.Show();
                        this.WindowState = System.Windows.WindowState.Normal;
                        this.Activate();
                        this.ShowInTaskbar = true;
                    }
                    
                    // 更新UI状态 - 这会触发显示模态3
                    UpdateLoginUI(null);
                    UpdateTrayUserStatus();
                    
                    // 显示通知
                    systemTrayManager?.ShowNotification("AudioRecorder", "已退出登录", System.Windows.Forms.ToolTipIcon.Info);
                    
                    _logger.LogInformation($"✅ 退出登录成功，已切换到登录界面 - Provider: {logoutProvider}");
                }
                else
                {
                    _logger.LogWarning($"无法执行退出登录 - oauthService: {(oauthService != null ? "存在" : "null")}, currentProvider: {logoutProvider ?? "null"}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "退出登录失败");
                systemTrayManager?.ShowNotification("AudioRecorder", "退出登录失败", System.Windows.Forms.ToolTipIcon.Error);
            }
        }

        /// <summary>
        /// 更新托盘用户状态
        /// </summary>
        private void UpdateTrayUserStatus()
        {
            try
            {
                _logger.LogDebug($"开始更新托盘用户状态 - isLoggedIn: {isLoggedIn}, currentProvider: {currentProvider ?? "null"}");
                
                if (systemTrayManager != null)
                {
                    // 获取当前用户信息
                    TokenInfo? currentUser = null;
                    if (isLoggedIn && oauthService != null && !string.IsNullOrEmpty(currentProvider))
                    {
                        // 从OAuth服务获取当前用户令牌信息
                        currentUser = oauthService.GetToken(currentProvider);
                        _logger.LogDebug($"从OAuth服务获取到用户信息: {(currentUser != null ? $"{currentUser.UserName} ({currentUser.Provider})" : "null")}");
                    }
                    else
                    {
                        _logger.LogDebug($"跳过获取用户信息 - isLoggedIn: {isLoggedIn}, oauthService: {(oauthService != null ? "存在" : "null")}, currentProvider: {currentProvider ?? "null"}");
                    }
                    
                    systemTrayManager.UpdateUserStatus(currentUser);
                    _logger.LogDebug("托盘用户状态更新完成");
                }
                else
                {
                    _logger.LogWarning("systemTrayManager 为 null，无法更新托盘用户状态");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新托盘用户状态失败");
            }
        }

        #endregion

        /// <summary>
        /// 隐藏登录面板
        /// </summary>
        private void HideLoginPanel()
        {
            // 隐藏登录相关的UI元素
            if (LoginStatusPanel != null)
            {
                LoginStatusPanel.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 显示登录面板
        /// </summary>
        private void ShowLoginPanel()
        {
            // 显示登录相关的UI元素
            if (LoginStatusPanel != null)
            {
                LoginStatusPanel.Visibility = Visibility.Visible;
            }
        }

        private void InitializeWebSocket()
        {
            try
            {
                if (recorder != null)
                {
                    webSocketServer = new SimpleWebSocketServer(recorder, 8080);
                    webSocketServer.StatusChanged += OnWebSocketStatusChanged;
                    
                    // 启动WebSocket服务器
                    _ = webSocketServer.StartAsync();
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"WebSocket服务器启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void InitializeIPC()
        {
            try
            {
                _logger.LogInformation("初始化IPC服务器");
                
                // 创建IPC管理器
                ipcManager = new IPCManager();
                
                // 订阅命令接收事件
                ipcManager.CommandReceived += OnIPCCommandReceived;
                
                // 启动IPC服务器
                ipcManager.StartServer();
                
                _logger.LogInformation("IPC服务器启动成功");
            }
            catch (Exception ex)
            {
                _logger.LogError($"IPC服务器启动失败: {ex.Message}");
            }
        }

        private void InitializeSystemTray()
        {
            try
            {
                _logger.LogInformation("初始化系统托盘");
                
                // 创建系统托盘管理器
                systemTrayManager = new SystemTrayManager();
                
                // 订阅托盘事件
                systemTrayManager.ShowWindowRequested += OnTrayShowWindowRequested;
                systemTrayManager.ExitApplicationRequested += OnTrayExitApplicationRequested;
                systemTrayManager.LogoutRequested += OnTrayLogoutRequested;
                
                // 初始化用户状态
                UpdateTrayUserStatus();
                
                _logger.LogInformation("✅ 系统托盘初始化成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 系统托盘初始化失败");
            }
        }

        // 加载图标
        private void LoadIcons()
        {
            try
            {
                // 使用新的图标转换器加载所有图标
                SvgIconConverter.LoadIconsToWindow(this);
                _logger.LogInformation("✅ 图标加载完成");
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"❌ 加载图标失败: {ex.Message}");
                // fallback: 使用文字图标
                SetFallbackIcons();
            }
        }

        // 设置备用文字图标
        private void SetFallbackIcons()
        {
            RecordIcon1.Source = null;
            RecordIcon2.Source = null;
            StopIcon1.Source = null;
            StopIcon2.Source = null;
            ExpandIcon.Source = null;
            SharedMinimizeIcon.Source = null;
            SharedCloseIcon.Source = null;
        }

        // 显示模态一（小窗口）
        private void ShowModal1()
        {
            isLargeWindow = false;
            this.Width = Modal1Size.Width;
            this.Height = Modal1Size.Height;
            
            Modal1Grid.Visibility = Visibility.Visible;
            Modal2Grid.Visibility = Visibility.Collapsed;
            Modal3Grid.Visibility = Visibility.Collapsed;
            
            // 隐藏共用标题栏（模态1不需要）
            SharedTitleBar.Visibility = Visibility.Collapsed;
            
            UpdateUI();
        }

        // 显示模态二（大窗口）
        private void ShowModal2()
        {
            isLargeWindow = true;
            this.Width = Modal2Size.Width;
            this.Height = Modal2Size.Height;
            
            Modal1Grid.Visibility = Visibility.Collapsed;
            Modal2Grid.Visibility = Visibility.Visible;
            Modal3Grid.Visibility = Visibility.Collapsed;
            
            // 显示共用标题栏
            SharedTitleBar.Visibility = Visibility.Visible;
            
            UpdateUI();
        }

        // 显示模态三（中等窗口）
        private void ShowModal3()
        {
            isLargeWindow = false; // 模态三也是小窗口
            this.Width = Modal3Size.Width;
            this.Height = Modal3Size.Height;
            
            Modal1Grid.Visibility = Visibility.Collapsed;
            Modal2Grid.Visibility = Visibility.Collapsed;
            Modal3Grid.Visibility = Visibility.Visible;
            
            // 显示共用标题栏
            SharedTitleBar.Visibility = Visibility.Visible;
            
            UpdateUI();
        }

        // 更新界面状态
        private void UpdateUI()
        {
            string statusText;
            
            // 确定状态文字
            if (isRecording && !isPaused)
            {
                statusText = "记录中...";
            }
            else if (isPaused)
            {
                statusText = "已暂停";
            }
            else
            {
                statusText = "未开始";
            }

            // 更新状态标签和图标
            if (isLargeWindow)
            {
                StatusLabel2.Text = statusText;
                SvgIconConverter.UpdateRecordingIcon(RecordIcon2, isRecording, isPaused, 40, 40);
            }
            else
            {
                StatusLabel1.Text = statusText;
                SvgIconConverter.UpdateRecordingIcon(RecordIcon1, isRecording, isPaused, 24, 24);
            }
        }

        #region 事件处理

        // 录音按钮点击
        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (recorder == null) return;

            if (!isRecording && !isPaused)
            {
                // 开始录制
                recorder.StartRecording();
                isRecording = true;
                isPaused = false;
                UpdateUI();
                
                NotifyWebSocketClients("recording_started", new { IsRecording = true });
            }
            else if (isRecording && !isPaused)
            {
                // 暂停录制
                recorder.PauseRecording();
                isPaused = true;
                UpdateUI();
                
                NotifyWebSocketClients("recording_paused", new { IsRecording = true, IsPaused = true });
            }
            else if (isPaused)
            {
                // 恢复录制
                recorder.ResumeRecording();
                isPaused = false;
                UpdateUI();
                
                NotifyWebSocketClients("recording_resumed", new { IsRecording = true, IsPaused = false });
            }
        }

        // 停止按钮点击
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (recorder == null || !isRecording) return;

            // 如果正在录音，显示确认覆盖层
            if (isRecording && !isPaused)
            {
                ShowStopConfirmOverlay();
            }
            else if (isPaused)
            {
                // 如果已暂停，直接停止
                ExecuteStopRecording();
            }
        }

        // 确认停止录音按钮点击
        private void ConfirmStopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger?.LogInformation("用户确认停止录音");
                
                // 隐藏确认覆盖层
                HideStopConfirmOverlay();
                
                // 执行停止录音
                ExecuteStopRecording();
                
                // 上传在后台静默执行，不显示状态
                // 录音停止后，ExecuteStopRecording会自动调用AutoUploadRecordingFiles
                
                // 如果是从关闭按钮触发的，停止录音后关闭应用程序
                if (isStopConfirming)
                {
                    _logger?.LogInformation("录音已停止，现在关闭应用程序");
                    // 延迟一点时间确保录音完全停止
                    Dispatcher.BeginInvoke(() => CloseApplication(), 
                        System.Windows.Threading.DispatcherPriority.Normal);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "停止录音失败");
                // 即使停止录音失败，也要关闭应用程序
                CloseApplication();
            }
        }

        // 取消停止录音按钮点击
        private void CancelStopButton_Click(object sender, RoutedEventArgs e)
        {
            HideStopConfirmOverlay();
            // 保持录音状态，不做任何操作
        }

        // 执行停止录音
        private void ExecuteStopRecording()
        {
            if (recorder == null) return;

            if (isRecording || isPaused)
            {
                recorder.StopRecording();
                isRecording = false;
                isPaused = false;
                UpdateUI();
                
                NotifyWebSocketClients("recording_stopped", new { IsRecording = false });

                // 自动上传录音文件
                if (uploadService != null)
                {
                    AutoUploadRecordingFiles();
                }
            }
        }

        // 显示停止录音确认覆盖层
        private void ShowStopConfirmOverlay()
        {
            // 确保显示模态二
            if (!isLargeWindow)
            {
                ShowModal2();
            }
            
            // 显示确认覆盖层
            StopConfirmOverlay.Visibility = Visibility.Visible;
            isStopConfirming = true;
            
            // 禁用其他按钮，强制用户做出选择
            DisableOtherButtons(true);
        }

        // 隐藏停止录音确认覆盖层
        private void HideStopConfirmOverlay()
        {
            StopConfirmOverlay.Visibility = Visibility.Collapsed;
            isStopConfirming = false;
            
            // 重新启用其他按钮
            DisableOtherButtons(false);
        }

        // 禁用/启用其他按钮
        private void DisableOtherButtons(bool disable)
        {
            // 模态一按钮
            RecordButton1.IsEnabled = !disable;
            StopButton1.IsEnabled = !disable;
            ExpandButton.IsEnabled = !disable;
            
            // 模态二按钮
            RecordButton2.IsEnabled = !disable;
            StopButton2.IsEnabled = !disable;
            
            // 共用标题栏按钮
            SharedMinimizeButton.IsEnabled = !disable;
            SharedCloseButton.IsEnabled = !disable;
        }

        // 展开按钮点击
        private void ExpandButton_Click(object sender, RoutedEventArgs e)
        {
            ShowModal2();
        }

        // 最小化按钮点击
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            ShowModal1();
        }

        // 关闭按钮点击
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger?.LogInformation("用户点击关闭按钮");
                
                // 如果正在录制，先停止录制
                if (isRecording && recorder != null)
                {
                    _logger?.LogInformation("正在录音，显示停止确认对话框");
                    ShowStopConfirmOverlay();
                }
                else
                {
                    _logger?.LogInformation("开始关闭窗口");
                    CloseApplication();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "关闭按钮处理失败");
                // 强制关闭
                CloseApplication();
            }
        }
        
        /// <summary>
        /// 窗口状态变化事件处理
        /// </summary>
        private void OnWindowStateChanged(object? sender, EventArgs e)
        {
            try
            {
                if (this.WindowState == System.Windows.WindowState.Minimized)
                {
                    // 最小化时隐藏窗口到托盘
                    this.Hide();
                    this.ShowInTaskbar = false;
                    
                    // 显示托盘通知
                    systemTrayManager?.ShowNotification("AudioRecorder", "程序已最小化到系统托盘", System.Windows.Forms.ToolTipIcon.Info, 2000);
                    
                    _logger.LogInformation("窗口已最小化到系统托盘");
                }
                else if (this.WindowState == System.Windows.WindowState.Normal)
                {
                    // 恢复时显示在任务栏
                    this.ShowInTaskbar = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理窗口状态变化失败");
            }
        }

        /// <summary>
        /// 窗口关闭事件处理
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // 如果正在录制，阻止关闭并显示确认对话框
                if (isRecording && recorder != null)
                {
                    e.Cancel = true;
                    ShowStopConfirmOverlay();
                    return;
                }

                // 正常关闭时清理资源
                CleanupResources();
                
                base.OnClosing(e);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "窗口关闭事件处理失败");
                base.OnClosing(e);
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void CleanupResources()
        {
            try
            {
                _logger?.LogInformation("开始清理应用程序资源");

                // 保存窗口位置
                SaveWindowPosition();

                // 停止录制
                if (isRecording && recorder != null)
                {
                    recorder.StopRecording();
                }

                // 清理系统托盘
                systemTrayManager?.Dispose();

                // 清理其他服务
                webSocketServer?.Dispose();
                ipcManager?.Dispose();
                recorder?.Dispose();

                _logger?.LogInformation("✅ 应用程序资源清理完成");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "清理应用程序资源失败");
            }
        }

        /// <summary>
        /// 关闭应用程序
        /// </summary>
        private void CloseApplication()
        {
            try
            {
                _logger?.LogInformation("正在关闭应用程序...");
                this.Close();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "正常关闭失败，强制退出");
                try
                {
                    System.Windows.Application.Current?.Shutdown();
                }
                catch
                {
                    Environment.Exit(0);
                }
            }
        }

        // 窗口拖拽
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        #endregion

        #region 录音器事件处理

        private void OnStatusChanged(object? sender, string status)
        {
            if (Dispatcher.CheckAccess())
            {
                // 同步录音状态
                bool wasRecording = isRecording;
                
                if (status.Contains("录制已开始") || status.Contains("录制已在进行中") || 
                    status.Contains("Recording") || status.Contains("recording"))
                {
                    isRecording = true;
                }
                else if (status.Contains("录制已停止") || status.Contains("停止") || 
                         status.Contains("Stopped") || status.Contains("stopped"))
                {
                    isRecording = false;
                    isPaused = false;
                }
                else if (status.Contains("暂停") || status.Contains("paused"))
                {
                    isPaused = true;
                }
                
                if (wasRecording != isRecording)
                {
                    UpdateUI();
                }
            }
            else
            {
                Dispatcher.Invoke(() => OnStatusChanged(sender, status));
            }
        }

        private void OnErrorOccurred(object? sender, Exception error)
        {
            if (Dispatcher.CheckAccess())
            {
                WpfMessageBox.Show($"录音错误: {error.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                Dispatcher.Invoke(() => OnErrorOccurred(sender, error));
            }
        }

        private void OnWebSocketStatusChanged(object? sender, string message)
        {
            if (Dispatcher.CheckAccess())
            {
                System.Diagnostics.Debug.WriteLine($"WebSocket: {message}");
                
                if (message.Contains("WebSocket命令：开始录制"))
                {
                    if (!isRecording)
                    {
                        isRecording = true;
                        isPaused = false;
                        UpdateUI();
                    }
                }
                else if (message.Contains("WebSocket命令：停止录制"))
                {
                    if (isRecording)
                    {
                        isRecording = false;
                        isPaused = false;
                        UpdateUI();
                    }
                }
            }
            else
            {
                Dispatcher.Invoke(() => OnWebSocketStatusChanged(sender, message));
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 设置窗口位置（默认在桌面右中部分，或恢复上次位置）
        /// </summary>
        private void SetWindowPosition()
        {
            try
            {
                // 尝试从配置文件恢复上次的窗口位置
                var config = ConfigurationService.Instance;
                var savedPosition = config.GetWindowPosition();
                
                if (savedPosition != null)
                {
                    // 恢复上次位置，但需要验证是否在当前屏幕范围内
                    this.Left = savedPosition.X;
                    this.Top = savedPosition.Y;
                    
                    // 验证并调整窗口位置，确保在屏幕范围内
                    if (ValidateAndAdjustWindowPosition())
                    {
                        _logger.LogInformation($"🔄 恢复窗口位置: ({this.Left}, {this.Top})");
                    }
                    else
                    {
                        _logger.LogInformation($"📍 窗口位置超出屏幕范围，使用默认位置");
                        SetDefaultWindowPosition();
                    }
                }
                else
                {
                    // 设置默认位置：桌面右中部分
                    SetDefaultWindowPosition();
                    _logger.LogInformation($"📍 设置默认窗口位置: ({this.Left}, {this.Top})");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"⚠️ 设置窗口位置失败: {ex.Message}");
                // 如果出错，使用默认位置
                SetDefaultWindowPosition();
            }
        }

        /// <summary>
        /// 设置默认窗口位置（桌面右中部分）
        /// </summary>
        private void SetDefaultWindowPosition()
        {
            try
            {
                // 获取当前鼠标所在屏幕或主屏幕的工作区域
                var screen = GetCurrentScreen() ?? System.Windows.Forms.Screen.PrimaryScreen;
                if (screen != null)
                {
                    _logger.LogInformation($"📺 使用屏幕工作区域: {screen.WorkingArea}");
                    var workingArea = screen.WorkingArea;
                    
                    // 计算右中位置（考虑窗口尺寸和DPI缩放）
                    var windowWidth = this.Width > 0 ? this.Width : Modal2Size.Width;
                    var windowHeight = this.Height > 0 ? this.Height : Modal2Size.Height;
                    
                    // 转换为设备无关像素
                    var dpiScale = GetDpiScale();
                    var scaledWidth = windowWidth * dpiScale;
                    var scaledHeight = windowHeight * dpiScale;
                    
                    this.Left = (workingArea.Right - scaledWidth - 20) / dpiScale; // 距离右边缘20像素
                    this.Top = (workingArea.Top + (workingArea.Height - scaledHeight) / 2) / dpiScale; // 垂直居中
                    
                    _logger.LogInformation($"📍 计算位置: DPI缩放={dpiScale:F2}, 窗口尺寸=({windowWidth}x{windowHeight}), 位置=({this.Left:F0},{this.Top:F0})");
                }
                else
                {
                    _logger.LogInformation($"⚠️ 使用系统参数位置");
                    // 如果无法获取屏幕信息，使用系统参数
                    var workArea = System.Windows.SystemParameters.WorkArea;
                    this.Left = workArea.Width - 220; // 距离右边缘220像素
                    this.Top = workArea.Height / 2 - 100; // 垂直居中
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"⚠️ 设置默认窗口位置失败: {ex.Message}");
                // 使用系统默认位置
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = 100;
                this.Top = 100;
            }
        }

        /// <summary>
        /// 保存当前窗口位置到配置文件
        /// </summary>
        private void SaveWindowPosition()
        {
            try
            {
                var config = ConfigurationService.Instance;
                var position = new AudioRecorder.Services.WindowPosition
                {
                    X = this.Left,
                    Y = this.Top,
                    LastSaved = DateTime.Now
                };
                config.SaveWindowPosition(position);
                _logger.LogInformation($"💾 保存窗口位置: ({position.X}, {position.Y})");
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"⚠️ 保存窗口位置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查窗口是否在屏幕边界内
        /// </summary>
        private bool IsWindowInScreenBounds()
        {
            try
            {
                // 获取当前窗口所在的屏幕
                var screen = GetScreenFromWindow() ?? System.Windows.Forms.Screen.PrimaryScreen;
                if (screen != null)
                {
                    var workingArea = screen.WorkingArea;
                    var dpiScale = GetDpiScale();
                    
                    // 转换窗口坐标到屏幕坐标
                    var windowLeft = this.Left * dpiScale;
                    var windowTop = this.Top * dpiScale;
                    var windowRight = windowLeft + (this.Width * dpiScale);
                    var windowBottom = windowTop + (this.Height * dpiScale);
                    
                    // 检查窗口是否至少有一部分在屏幕范围内（允许部分超出）
                    bool isVisible = windowRight > workingArea.Left && 
                                   windowLeft < workingArea.Right && 
                                   windowBottom > workingArea.Top && 
                                   windowTop < workingArea.Bottom;
                    
                    _logger.LogInformation($"🔍 窗口边界检查: 窗口=({windowLeft:F0},{windowTop:F0},{windowRight:F0},{windowBottom:F0}), 屏幕={workingArea}, 可见={isVisible}");
                    return isVisible;
                }
                
                // 如果无法获取屏幕信息，使用系统参数
                var systemWorkingArea = System.Windows.SystemParameters.WorkArea;
                return this.Left >= -this.Width/2 && 
                       this.Top >= -this.Height/2 && 
                       this.Left <= systemWorkingArea.Width && 
                       this.Top <= systemWorkingArea.Height;
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"⚠️ 检查窗口边界失败: {ex.Message}");
                return false; // 如果出错，不保存位置
            }
        }

        /// <summary>
        /// 验证并调整窗口位置，确保窗口在屏幕范围内
        /// </summary>
        private bool ValidateAndAdjustWindowPosition()
        {
            try
            {
                var screen = GetScreenFromWindow() ?? System.Windows.Forms.Screen.PrimaryScreen;
                if (screen == null) return false;
                
                var workingArea = screen.WorkingArea;
                var dpiScale = GetDpiScale();
                
                // 转换到屏幕坐标
                var windowLeft = this.Left * dpiScale;
                var windowTop = this.Top * dpiScale;
                var windowWidth = this.Width * dpiScale;
                var windowHeight = this.Height * dpiScale;
                
                bool adjusted = false;
                
                // 调整水平位置
                if (windowLeft + windowWidth < workingArea.Left + 50) // 窗口几乎完全在左边界外
                {
                    windowLeft = workingArea.Left;
                    adjusted = true;
                }
                else if (windowLeft > workingArea.Right - 50) // 窗口几乎完全在右边界外
                {
                    windowLeft = workingArea.Right - windowWidth;
                    adjusted = true;
                }
                
                // 调整垂直位置
                if (windowTop + windowHeight < workingArea.Top + 50) // 窗口几乎完全在上边界外
                {
                    windowTop = workingArea.Top;
                    adjusted = true;
                }
                else if (windowTop > workingArea.Bottom - 50) // 窗口几乎完全在下边界外
                {
                    windowTop = workingArea.Bottom - windowHeight;
                    adjusted = true;
                }
                
                if (adjusted)
                {
                    // 转换回WPF坐标
                    this.Left = windowLeft / dpiScale;
                    this.Top = windowTop / dpiScale;
                    _logger.LogInformation($"🔧 调整窗口位置: ({this.Left:F0}, {this.Top:F0})");
                }
                
                return !adjusted; // 如果没有调整，说明原位置有效
            }
            catch (Exception ex)
            {
                _logger.LogError($"⚠️ 验证窗口位置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取当前DPI缩放比例
        /// </summary>
        private double GetDpiScale()
        {
            try
            {
                // 方法1: 尝试从当前窗口获取DPI
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    var dpiScale = source.CompositionTarget.TransformToDevice.M11;
                    if (dpiScale > 0.1) // 确保获取到有效值
                    {
                        _logger.LogInformation($"🔍 方法1获取DPI缩放: {dpiScale:F2} ({dpiScale * 100:F0}%)");
                        return dpiScale;
                    }
                }

                // 方法2: 使用系统DPI API (Windows 10+)
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    var dpi = GetDpiForWindow(hwnd);
                    if (dpi > 0)
                    {
                        var dpiScale = dpi / 96.0; // 96 DPI = 100% 缩放
                        _logger.LogInformation($"🔍 方法2获取DPI缩放: {dpiScale:F2} ({dpiScale * 100:F0}%), 原始DPI: {dpi}");
                        return dpiScale;
                    }
                }

                // 方法3: 使用系统参数获取主屏幕DPI
                using (var graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                {
                    var dpiX = graphics.DpiX;
                    if (dpiX > 0)
                    {
                        var dpiScale = dpiX / 96.0;
                        _logger.LogInformation($"🔍 方法3获取DPI缩放: {dpiScale:F2} ({dpiScale * 100:F0}%), 原始DPI: {dpiX}");
                        return dpiScale;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"获取DPI缩放失败: {ex.Message}");
            }
            
            // 默认返回1.0（100%缩放）
            _logger.LogWarning($"⚠️ 使用默认DPI缩放: 1.0 (100%)");
            return 1.0;
        }

        // Windows API 声明
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        /// <summary>
        /// 测试DPI检测功能 (快捷键: Ctrl+D)
        /// </summary>
        private void TestDpiDetection()
        {
            try
            {
                var dpiScale = GetDpiScale();
                var message = $"当前DPI缩放: {dpiScale:F2} ({dpiScale * 100:F0}%)\n" +
                             $"窗口位置: ({this.Left:F0}, {this.Top:F0})\n" +
                             $"窗口尺寸: {this.Width:F0} x {this.Height:F0}";
                
                _logger.LogInformation($"🧪 DPI测试结果: {message.Replace('\n', ' ')}");
                WpfMessageBox.Show(message, "DPI检测测试", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError($"DPI测试失败: {ex.Message}");
                WpfMessageBox.Show($"DPI测试失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取当前屏幕（基于鼠标位置）
        /// </summary>
        private System.Windows.Forms.Screen? GetCurrentScreen()
        {
            try
            {
                var mousePosition = System.Windows.Forms.Control.MousePosition;
                return System.Windows.Forms.Screen.FromPoint(mousePosition);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"获取当前屏幕失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取窗口所在的屏幕
        /// </summary>
        private System.Windows.Forms.Screen? GetScreenFromWindow()
        {
            try
            {
                var dpiScale = GetDpiScale();
                var screenPoint = new System.Drawing.Point(
                    (int)(this.Left * dpiScale + this.Width * dpiScale / 2),
                    (int)(this.Top * dpiScale + this.Height * dpiScale / 2)
                );
                return System.Windows.Forms.Screen.FromPoint(screenPoint);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"获取窗口屏幕失败: {ex.Message}");
                return null;
            }
        }

        private void NotifyWebSocketClients(string command, object data)
        {
            if (webSocketServer != null)
            {
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        var message = new SimpleWebSocketMessage
                        {
                            Command = command,
                            Success = true,
                            Message = isRecording ? "录音状态：录制中" : "录音状态：已停止",
                            Data = data
                        };
                        
                        System.Diagnostics.Debug.WriteLine($"广播消息: {command}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"广播失败: {ex.Message}");
                    }
                });
            }
        }

        private async void AutoUploadRecordingFiles()
        {
            if (uploadService == null || recorder == null) 
            {
                _logger.LogWarning("上传服务或录音器未初始化，跳过自动上传");
                return;
            }

            try
            {
                var systemAudioPath = recorder.GetCurrentSystemAudioPath();
                var microphonePath = recorder.GetCurrentMicrophonePath();

                _logger.LogInformation("准备自动上传录音文件: 系统音频={SystemPath}, 麦克风={MicPath}", 
                    systemAudioPath, microphonePath);

                if (!string.IsNullOrEmpty(systemAudioPath) && !string.IsNullOrEmpty(microphonePath))
                {
                    // 验证文件是否存在
                    if (!File.Exists(systemAudioPath))
                    {
                        _logger.LogError("系统音频文件不存在: {Path}", systemAudioPath);
                        return;
                    }
                    
                    if (!File.Exists(microphonePath))
                    {
                        _logger.LogError("麦克风音频文件不存在: {Path}", microphonePath);
                        return;
                    }

                    // 获取文件大小信息
                    var systemFileInfo = new FileInfo(systemAudioPath);
                    var micFileInfo = new FileInfo(microphonePath);
                    
                    _logger.LogInformation("文件验证通过，准备上传: 系统音频={SystemFile}({Size}字节), 麦克风={MicFile}({Size}字节)", 
                        Path.GetFileName(systemAudioPath), systemFileInfo.Length,
                        Path.GetFileName(microphonePath), micFileInfo.Length);

                    // 等待一秒确保文件写入完成
                    await System.Threading.Tasks.Task.Delay(1000);

                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            _logger.LogInformation("开始后台上传任务");
                            await uploadService.UploadAudioFilesAsync(systemAudioPath, microphonePath);
                            _logger.LogInformation("后台上传任务完成");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "后台上传任务失败");
                            _logger.LogInformation($"❌ 自动上传失败: {ex.Message}");
                        }
                    });
                }
                else
                {
                    _logger.LogWarning("录音文件路径为空，跳过上传: 系统音频={SystemPath}, 麦克风={MicPath}", 
                        systemAudioPath, microphonePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "准备上传文件时发生异常");
                _logger.LogInformation($"❌ 准备上传文件失败: {ex.Message}");
            }
        }

        private void OnUploadProgressChanged(object? sender, string message)
        {
            _logger.LogInformation($"📤 {message}");
        }

        private void OnUploadErrorOccurred(object? sender, Exception exception)
        {
            _logger.LogInformation($"❌ 上传错误: {exception.Message}");
        }

        private void OnUploadCompleted(object? sender, string message)
        {
            _logger.LogInformation($"✅ {message}");
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _logger?.LogInformation("🔄 窗口正在关闭，开始清理资源...");
                
                // 取消订阅URL协议事件
                UrlProtocolHandler.ProtocolActionReceived -= OnProtocolActionReceived;
                
                // 保存窗口位置（如果窗口在屏幕范围内）
                if (IsWindowInScreenBounds())
                {
                    SaveWindowPosition();
                }
                
                // 停止录音（如果正在录音）
                if (isRecording && recorder != null)
                {
                    _logger?.LogInformation("停止录音...");
                    recorder.StopRecording();
                }
                
                // 释放WebSocket服务器
                if (webSocketServer != null)
                {
                    _logger?.LogInformation("停止WebSocket服务器...");
                    webSocketServer.Stop();
                    webSocketServer.Dispose();
                    webSocketServer = null;
                }
                
                // 释放上传服务
                if (uploadService != null)
                {
                    _logger?.LogInformation("释放上传服务...");
                    uploadService.Dispose();
                    uploadService = null;
                }
                
                // 释放录音器
                if (recorder != null)
                {
                    _logger?.LogInformation("释放录音器...");
                    recorder.Dispose();
                    recorder = null;
                }
                
                // 释放OAuth服务
                if (oauthService != null)
                {
                    _logger?.LogInformation("释放OAuth服务...");
                    oauthService.Dispose();
                    oauthService = null;
                }
                
                // 释放IPC服务器
                if (ipcManager != null)
                {
                    _logger?.LogInformation("停止IPC服务器...");
                    ipcManager.Dispose();
                    ipcManager = null;
                }
                
                _logger?.LogInformation("✅ 资源清理完成");
                
                base.OnClosed(e);
                
                // 强制退出应用程序
                System.Windows.Application.Current?.Shutdown();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "窗口关闭时清理资源失败");
                
                // 即使出错也要强制退出
                try
                {
                    System.Windows.Application.Current?.Shutdown();
                }
                catch
                {
                    // 最后的手段 - 强制终止进程
                    Environment.Exit(0);
                }
            }
        }

        /// <summary>
        /// 处理URL协议事件
        /// </summary>
        private void OnProtocolActionReceived(object? sender, ProtocolActionEventArgs e)
        {
            try
            {
                _logger.LogInformation($"收到URL协议命令: {e.Action}");
                ExecuteCommand(e.Action);
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理URL协议命令失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理IPC命令事件
        /// </summary>
        private void OnIPCCommandReceived(object? sender, IPCCommandEventArgs e)
        {
            try
            {
                _logger.LogInformation($"收到IPC命令: {e.Command.Action}");
                ExecuteCommand(e.Command.Action);
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理IPC命令失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行命令（统一的命令处理逻辑）
        /// </summary>
        /// <param name="action">要执行的动作</param>
        private void ExecuteCommand(string action)
        {
            // 确保在UI线程中执行
            this.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    string actionLower = action.ToLower();
                    
                    // 根据不同的action决定是否显示界面
                    bool shouldShowWindow = actionLower == "show" || actionLower == "start" || 
                                          actionLower == "stop" || actionLower == "pause" || actionLower == "resume";
                    
                    if (shouldShowWindow)
                    {
                        // 确保窗口可见和激活
                        this.Show();
                        this.Activate();
                        this.WindowState = System.Windows.WindowState.Normal;
                        this.Topmost = true;
                        _logger.LogInformation($"窗口已显示，执行命令: {actionLower}");
                    }
                    
                    switch (actionLower)
                    {
                        case "start":
                            if (recorder != null && !isRecording)
                            {
                                recorder.StartRecording();
                                isRecording = true;
                                isPaused = false;
                                UpdateUI();
                                NotifyWebSocketClients("recording_started", new { IsRecording = true });
                                _logger.LogInformation("开始录音");
                            }
                            break;
                        case "stop":
                            if (recorder != null && (isRecording || isPaused))
                            {
                                recorder.StopRecording();
                                isRecording = false;
                                isPaused = false;
                                UpdateUI();
                                NotifyWebSocketClients("recording_stopped", new { IsRecording = false });
                                _logger.LogInformation("停止录音");
                            }
                            break;
                        case "pause":
                            if (recorder != null && isRecording && !isPaused)
                            {
                                recorder.PauseRecording();
                                isPaused = true;
                                UpdateUI();
                                NotifyWebSocketClients("recording_paused", new { IsRecording = true, IsPaused = true });
                                _logger.LogInformation("暂停录音");
                            }
                            break;
                        case "resume":
                            if (recorder != null && isPaused)
                            {
                                recorder.ResumeRecording();
                                isPaused = false;
                                UpdateUI();
                                NotifyWebSocketClients("recording_resumed", new { IsRecording = true, IsPaused = false });
                                _logger.LogInformation("恢复录音");
                            }
                            break;
                        case "show":
                            _logger.LogInformation("显示窗口");
                            break;
                        default:
                            _logger.LogWarning($"未知的命令: {action}");
                            break;
                    }
                    
                    _logger.LogInformation($"命令 '{action}' 执行完成");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"执行命令失败: {ex.Message}");
                }
            }, System.Windows.Threading.DispatcherPriority.Normal);
        }
    }
}


using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AudioRecorder.Services;
using AudioRecorder.Models;
using WpfMessageBox = System.Windows.MessageBox;
using WpfPoint = System.Windows.Point;
using System.Threading.Tasks; // Added for Task.Run
using Microsoft.Extensions.Logging;

namespace AudioRecorder
{
    public partial class RecorderWindow : Window
    {
        private SimpleAudioRecorder? recorder;
        private SimpleWebSocketServer? webSocketServer;
        private AudioFileUploadService? uploadService;
        
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
            this.ShowInTaskbar = true;
            this.Visibility = System.Windows.Visibility.Visible;
            
            // 设置窗口位置（默认在桌面右中部分，或恢复上次位置）
            SetWindowPosition();
            
            // 初始化组件
            InitializeRecorder();
            InitializeOAuth();
            InitializeWebSocket();
            LoadIcons();
            
            // 根据OAuth认证状态和登录状态决定初始显示
            var config = ConfigurationService.Instance;
            if (!config.IsOAuthEnabled())
            {
                // OAuth未启用，直接显示模态1（录音状态）
                ShowModal1();
            }
            else if (isLoggedIn)
            {
                ShowModal1();
                HideLoginPanel();
            }
            else
            {
                ShowModal3(); // 显示模态3以显示登录状态
            }
            
            // 设置拖拽
            this.MouseLeftButtonDown += (s, e) => this.DragMove();
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
                isLoggedIn = true;
                currentProvider = tokenInfo.Provider;
                UpdateLoginUI(tokenInfo);
                _logger.LogInformation($"✅ {tokenInfo.Provider}授权完成: {tokenInfo.UserName}");
            });
        }

        private void OnOAuthLoginFailed(object? sender, string error)
        {
            Dispatcher.InvokeAsync(() =>
            {
                isLoggedIn = false;
                currentProvider = null;
                UpdateLoginUI(null);
                _logger.LogInformation($"❌ {currentProvider}授权失败: {error}");
                WpfMessageBox.Show($"{currentProvider}授权失败: {error}", "授权失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        private void OnOAuthLoginStateRestored(object? sender, TokenInfo tokenInfo)
        {
            Dispatcher.InvokeAsync(() =>
            {
                isLoggedIn = true;
                currentProvider = tokenInfo.Provider;
                UpdateLoginUI(tokenInfo);
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
                    currentProvider = provider;
                    
                    _logger.LogInformation($"🚀 开始{provider} OAuth登录流程");
                    var success = await oauthService.StartLoginAsync(provider);
                    if (!success)
                    {
                        WpfMessageBox.Show($"启动{provider}登录失败", "登录失败", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                // 未登录，显示模态3（登录状态）
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
            MinimizeIcon.Source = null;
            CloseIcon.Source = null;
        }

        // 显示模态一（小窗口）
        private void ShowModal1()
        {
            isLargeWindow = false;
            this.Width = Modal1Size.Width;
            this.Height = Modal1Size.Height;
            
            Modal1Grid.Visibility = Visibility.Visible;
            Modal2Grid.Visibility = Visibility.Collapsed;
            
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
                // 隐藏确认覆盖层
                HideStopConfirmOverlay();
                
                // 执行停止录音
                ExecuteStopRecording();
                
                // 上传在后台静默执行，不显示状态
                // 录音停止后，ExecuteStopRecording会自动调用AutoUploadRecordingFiles
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"❌ 停止录音失败: {ex.Message}");
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
            MinimizeButton.IsEnabled = !disable;
            CloseButton.IsEnabled = !disable;
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
            // 如果正在录制，先停止录制
            if (isRecording && recorder != null)
            {
                ShowStopConfirmOverlay();
            }
            else
            {
                this.Close();
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
                
                //if (savedPosition != null)
                //{
                //    // 恢复上次位置
                //    this.Left = savedPosition.X;
                //    this.Top = savedPosition.Y;
                //    _logger.LogInformation($"🔄 恢复窗口位置: ({this.Left}, {this.Top})");
                //}
                //else
                //{
                    // 设置默认位置：桌面右中部分
                    SetDefaultWindowPosition();
                    _logger.LogInformation($"📍 设置默认窗口位置: ({this.Left}, {this.Top})");
                //}
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
                // 获取主屏幕的工作区域
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                if (screen != null)
                {
                    _logger.LogInformation($"⚠️ 设置主屏幕的工作区域");
                    var workingArea = screen.WorkingArea;
                    
                    // 计算右中位置（考虑窗口尺寸）
                    var windowWidth = this.Width > 0 ? this.Width : Modal2Size.Width;
                    var windowHeight = this.Height > 0 ? this.Height : Modal2Size.Height;
                    
                    this.Left = workingArea.Right - windowWidth - 20; // 距离右边缘20像素
                    this.Top = workingArea.Top + (workingArea.Height - windowHeight) / 2; // 垂直居中
                }
                else
                {
                    _logger.LogInformation($"⚠️ 使用固定位置");
                    // 如果无法获取屏幕信息，使用固定位置
                    this.Left = System.Windows.SystemParameters.WorkArea.Width - 220; // 距离右边缘220像素
                    this.Top = System.Windows.SystemParameters.WorkArea.Height / 2 - 100; // 垂直居中
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"⚠️ 设置默认窗口位置失败: {ex.Message}");
                // 使用系统默认位置
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
                // 获取主屏幕的工作区域
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                if (screen != null)
                {
                    var workingArea = screen.WorkingArea;
                    
                    // 检查窗口是否完全在屏幕范围内
                    return this.Left >= workingArea.Left && 
                           this.Top >= workingArea.Top && 
                           this.Left + this.Width <= workingArea.Right && 
                           this.Top + this.Height <= workingArea.Bottom;
                }
                
                // 如果无法获取屏幕信息，使用系统参数
                var systemWorkingArea = System.Windows.SystemParameters.WorkArea;
                return this.Left >= 0 && 
                       this.Top >= 0 && 
                       this.Left + this.Width <= systemWorkingArea.Width && 
                       this.Top + this.Height <= systemWorkingArea.Height;
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"⚠️ 检查窗口边界失败: {ex.Message}");
                return false; // 如果出错，不保存位置
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
            // 取消订阅URL协议事件
            UrlProtocolHandler.ProtocolActionReceived -= OnProtocolActionReceived;
            
            // 保存窗口位置（如果窗口在屏幕范围内）
            if (IsWindowInScreenBounds())
            {
                SaveWindowPosition();
            }
            
            webSocketServer?.Stop();
            webSocketServer?.Dispose();
            uploadService?.Dispose();
            recorder?.Dispose();
            base.OnClosed(e);
        }

        /// <summary>
        /// 处理URL协议事件
        /// </summary>
        private void OnProtocolActionReceived(object? sender, ProtocolActionEventArgs e)
        {
            try
            {
                _logger.LogInformation($"收到URL协议命令: {e.Action}");
                
                // 确保在UI线程中执行
                this.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        // 首先确保窗口可见和激活
                        this.Show();
                        this.Activate();
                        this.WindowState = System.Windows.WindowState.Normal;
                        this.Topmost = true;
                        
                        switch (e.Action.ToLower())
                        {
                            case "start":
                                if (recorder != null)
                                    recorder.StartRecording();
                                break;
                            case "stop":
                                if (recorder != null)
                                    recorder.StopRecording();
                                break;
                            case "pause":
                                if (recorder != null)
                                    recorder.PauseRecording();
                                break;
                            case "resume":
                                if (recorder != null)
                                    recorder.ResumeRecording();
                                break;
                            case "show":
                                // 窗口已经显示和激活
                                break;
                            default:
                                _logger.LogWarning($"未知的URL协议命令: {e.Action}");
                                break;
                        }
                        
                        _logger.LogInformation($"URL协议命令 '{e.Action}' 执行完成");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"执行URL协议命令失败: {ex.Message}");
                    }
                }, System.Windows.Threading.DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理URL协议命令失败: {ex.Message}");
            }
        }
    }
}


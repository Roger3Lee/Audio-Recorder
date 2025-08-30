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

namespace AudioRecorder
{
    public partial class RecorderWindow : Window
    {
        private SimpleAudioRecorder? recorder;
        private SimpleWebSocketServer? webSocketServer;
        private AudioFileUploadService? uploadService;
        
        private bool isRecording = false;
        private bool isPaused = false;
        private bool isLargeWindow = false; // true=模态二(大窗口), false=模态一(小窗口)
        private bool isStopConfirming = false; // 控制停止确认覆盖层的显示状态
        
        // 模态尺寸
        private readonly System.Windows.Size Modal1Size = new System.Windows.Size(200, 50);
        private readonly System.Windows.Size Modal2Size = new System.Windows.Size(200, 200);

        public RecorderWindow()
        {
            InitializeComponent();
            InitializeRecorder();
            InitializeWebSocket();
            LoadIcons();
            UpdateUI();
            
            // 初始显示模态一
            ShowModal1();
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
                    uploadService = new AudioFileUploadService(config.UploadSettings, new ConsoleLogger());
                    uploadService.UploadProgressChanged += OnUploadProgressChanged;
                    uploadService.UploadErrorOccurred += OnUploadErrorOccurred;
                    uploadService.UploadCompleted += OnUploadCompleted;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 初始化上传服务失败: {ex.Message}");
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
                Console.WriteLine("✅ 图标加载完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 加载图标失败: {ex.Message}");
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
            HideStopConfirmOverlay();
            ExecuteStopRecording();
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
                recorder.StopRecording();
                isRecording = false;
                
                // 自动上传录音文件
                if (uploadService != null)
                {
                    AutoUploadRecordingFiles();
                }
            }
            
            this.Close();
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
            if (uploadService == null || recorder == null) return;

            try
            {
                var systemAudioPath = recorder.GetCurrentSystemAudioPath();
                var microphonePath = recorder.GetCurrentMicrophonePath();

                if (!string.IsNullOrEmpty(systemAudioPath) && !string.IsNullOrEmpty(microphonePath))
                {
                    await System.Threading.Tasks.Task.Delay(1000);

                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            await uploadService.UploadAudioFilesAsync(systemAudioPath, microphonePath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ 自动上传失败: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 准备上传文件失败: {ex.Message}");
            }
        }

        private void OnUploadProgressChanged(object? sender, string message)
        {
            Console.WriteLine($"📤 {message}");
        }

        private void OnUploadErrorOccurred(object? sender, Exception exception)
        {
            Console.WriteLine($"❌ 上传错误: {exception.Message}");
        }

        private void OnUploadCompleted(object? sender, string message)
        {
            Console.WriteLine($"✅ {message}");
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            webSocketServer?.Stop();
            webSocketServer?.Dispose();
            uploadService?.Dispose();
            recorder?.Dispose();
            base.OnClosed(e);
        }
    }
}

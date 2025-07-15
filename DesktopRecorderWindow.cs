using System;
using System.Drawing;
using System.Windows.Forms;

namespace AudioRecorder
{
    public partial class DesktopRecorderWindow : Form
    {
        private SimpleAudioRecorder? recorder;
        private SimpleWebSocketServer? webSocketServer;
        private Button btnRecord = null!;
        private Panel iconPanel = null!;
        private ProgressBar pbSystemLevel = null!;
        private ProgressBar pbMicLevel = null!;
        private Label lblSystemLabel = null!;
        private Label lblMicLabel = null!;
        private System.Windows.Forms.Timer uiUpdateTimer = null!;
        private System.Windows.Forms.Timer animationTimer = null!;
        private bool isRecording = false;
        private bool isExpanded = false;
        private Point collapsedLocation; // 记录收缩状态的位置
        
        // 窗口尺寸常量 - 调整宽度为150px
        private readonly Size CollapsedSize = new Size(30, 30);
        private readonly Size ExpandedSize = new Size(150, 120); // 宽度改为150px，高度增加以容纳新布局
        
        // 动画相关变量 - 优化动画参数
        private bool isAnimating = false;
        private bool isExpandingAnimation = false;
        private int animationStep = 0;
        private const int AnimationSteps = 8; // 减少动画步数提高流畅度
        private const int AnimationInterval = 16; // 约60fps，提高流畅度
        
        // 用于窗口拖动
        private bool isDragging = false;
        private Point lastCursor;
        private Point lastForm;
        
        // 鼠标离开检测优化
        private System.Windows.Forms.Timer hoverCheckTimer = null!;
        private const int HoverCheckInterval = 100; // 检查间隔

        public DesktopRecorderWindow()
        {
            InitializeComponent();
            InitializeRecorder();
            InitializeWebSocket();
            InitializeTimer();
        }

        private void InitializeComponent()
        {
            // 窗体设置 - 置顶小窗口
            this.Text = "录音控制";
            this.Size = CollapsedSize; // 初始为收缩状态
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            
            // 设置收缩状态位置（右上角）
            collapsedLocation = new Point(Screen.PrimaryScreen.WorkingArea.Width - CollapsedSize.Width - 10, 10);
            this.Location = collapsedLocation;
            
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.ShowInTaskbar = false;
            this.AllowTransparency = true;
            this.Opacity = 0.96;

            // 创建圆角效果
            UpdateWindowRegion();

            // 图标面板（收缩状态显示）
            iconPanel = new Panel
            {
                Size = new Size(30, 30),
                Location = new Point(0, 0),
                BackColor = Color.Transparent,
                BorderStyle = BorderStyle.None
            };
            iconPanel.Paint += IconPanel_Paint;
            iconPanel.Click += BtnRecord_Click;
            iconPanel.Cursor = Cursors.Hand;

            // 主录音按钮（展开状态显示）- 重新设计为占满一行，包含图标和状态文字
            btnRecord = new Button
            {
                Size = new Size(130, 40), // 调整大小适应150px宽度
                Location = new Point(10, 15),
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Visible = false,
                Font = new Font("Microsoft YaHei", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnRecord.FlatAppearance.BorderSize = 0;
            btnRecord.FlatAppearance.MouseOverBackColor = Color.FromArgb(69, 160, 73);
            btnRecord.FlatAppearance.MouseDownBackColor = Color.FromArgb(56, 142, 60);
            btnRecord.Click += BtnRecord_Click;
            UpdateButtonText(); // 设置初始文本

            // 系统音频标签
            lblSystemLabel = new Label
            {
                Text = "系统",
                Size = new Size(30, 15),
                Location = new Point(10, 65),
                Font = new Font("Microsoft YaHei", 8, FontStyle.Regular),
                ForeColor = Color.FromArgb(108, 117, 125),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };

            // 系统音频电平
            pbSystemLevel = new ProgressBar
            {
                Size = new Size(100, 8), // 调整宽度适应150px窗口
                Location = new Point(40, 67),
                Style = ProgressBarStyle.Continuous,
                ForeColor = Color.FromArgb(33, 150, 243),
                BackColor = Color.FromArgb(222, 226, 230),
                Maximum = 100,
                Value = 0,
                Visible = false
            };

            // 麦克风标签
            lblMicLabel = new Label
            {
                Text = "麦克",
                Size = new Size(30, 15),
                Location = new Point(10, 85),
                Font = new Font("Microsoft YaHei", 8, FontStyle.Regular),
                ForeColor = Color.FromArgb(108, 117, 125),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };

            // 麦克风电平
            pbMicLevel = new ProgressBar
            {
                Size = new Size(100, 8), // 调整宽度适应150px窗口
                Location = new Point(40, 87),
                Style = ProgressBarStyle.Continuous,
                ForeColor = Color.FromArgb(255, 152, 0),
                BackColor = Color.FromArgb(222, 226, 230),
                Maximum = 100,
                Value = 0,
                Visible = false
            };

            // 添加控件
            this.Controls.AddRange(new Control[] { iconPanel, btnRecord, lblSystemLabel, pbSystemLevel, lblMicLabel, pbMicLevel });

            // 窗口hover事件
            this.MouseEnter += DesktopRecorderWindow_MouseEnter;
            this.MouseLeave += DesktopRecorderWindow_MouseLeave;
            
            // 所有控件的hover事件
            foreach (Control control in this.Controls)
            {
                control.MouseEnter += DesktopRecorderWindow_MouseEnter;
                control.MouseLeave += DesktopRecorderWindow_MouseLeave;
            }

            // 允许拖动窗口
            this.MouseDown += DesktopRecorderWindow_MouseDown;
            this.MouseMove += DesktopRecorderWindow_MouseMove;
            this.MouseUp += DesktopRecorderWindow_MouseUp;
            iconPanel.MouseDown += DesktopRecorderWindow_MouseDown;
            iconPanel.MouseMove += DesktopRecorderWindow_MouseMove;
            iconPanel.MouseUp += DesktopRecorderWindow_MouseUp;
            
            // 双击关闭窗口
            this.DoubleClick += (s, e) => this.Close();
            iconPanel.DoubleClick += (s, e) => this.Close();
        }
        
        // 更新按钮文字，包含图标和状态
        private void UpdateButtonText()
        {
            if (isRecording)
            {
                btnRecord.Text = "⏹ 录制中";
                btnRecord.BackColor = Color.FromArgb(220, 53, 69);
                btnRecord.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 48, 62);
                btnRecord.FlatAppearance.MouseDownBackColor = Color.FromArgb(180, 43, 56);
            }
            else
            {
                btnRecord.Text = "⏺ 开始录音";
                btnRecord.BackColor = Color.FromArgb(76, 175, 80);
                btnRecord.FlatAppearance.MouseOverBackColor = Color.FromArgb(69, 160, 73);
                btnRecord.FlatAppearance.MouseDownBackColor = Color.FromArgb(56, 142, 60);
            }
        }

        private void InitializeRecorder()
        {
            recorder = new SimpleAudioRecorder();
            recorder.StatusChanged += OnStatusChanged;
            recorder.ErrorOccurred += OnErrorOccurred;
            recorder.AudioLevelChanged += OnAudioLevelChanged;
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
                MessageBox.Show($"WebSocket服务器启动失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void InitializeTimer()
        {
            uiUpdateTimer = new System.Windows.Forms.Timer();
            uiUpdateTimer.Interval = 50; // 20Hz更新频率
            uiUpdateTimer.Tick += UiUpdateTimer_Tick;
            uiUpdateTimer.Start();
            
            // 初始化动画Timer
            animationTimer = new System.Windows.Forms.Timer();
            animationTimer.Interval = AnimationInterval;
            animationTimer.Tick += AnimationTimer_Tick;
            
            // 初始化鼠标离开检测Timer
            hoverCheckTimer = new System.Windows.Forms.Timer();
            hoverCheckTimer.Interval = HoverCheckInterval;
            hoverCheckTimer.Tick += HoverCheckTimer_Tick;
        }

        private void UiUpdateTimer_Tick(object? sender, EventArgs e)
        {
            // 这个方法可以保留为空，或者用于其他界面更新
        }
        
        // 鼠标离开检测Timer
        private void HoverCheckTimer_Tick(object? sender, EventArgs e)
        {
            if (!IsMouseOverWindow() && isExpanded && !isAnimating)
            {
                hoverCheckTimer.Stop();
                CollapseWindow();
            }
        }
        
        // 检查鼠标是否在窗口上方
        private bool IsMouseOverWindow()
        {
            Point mousePos = Control.MousePosition;
            Rectangle windowBounds = new Rectangle(this.Location, this.Size);
            return windowBounds.Contains(mousePos);
        }
        
        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            if (!isAnimating) return;
            
            animationStep++;
            
            // 使用更流畅的缓动函数
            float progress = (float)animationStep / AnimationSteps;
            float easedProgress = EaseInOutCubic(progress);
            
            if (!isExpandingAnimation)
            {
                easedProgress = 1.0f - easedProgress;
            }
            
            // 计算当前窗口大小
            int currentWidth = (int)(CollapsedSize.Width + (ExpandedSize.Width - CollapsedSize.Width) * easedProgress);
            int currentHeight = (int)(CollapsedSize.Height + (ExpandedSize.Height - CollapsedSize.Height) * easedProgress);
            
            // 计算当前位置（向左展开）
            int currentX = (int)(collapsedLocation.X - (ExpandedSize.Width - CollapsedSize.Width) * easedProgress);
            int currentY = collapsedLocation.Y;
            
            // 应用大小和位置
            this.Location = new Point(currentX, currentY);
            this.Size = new Size(currentWidth, currentHeight);
            UpdateWindowRegion();
            
            // 检查动画是否完成
            if (animationStep >= AnimationSteps)
            {
                // 动画完成
                animationTimer.Stop();
                isAnimating = false;
                animationStep = 0;
                
                // 根据动画类型显示/隐藏控件
                if (isExpandingAnimation)
                {
                    // 展开完成 - 显示详细控件
                    btnRecord.Visible = true;
                    lblSystemLabel.Visible = true;
                    pbSystemLevel.Visible = true;
                    lblMicLabel.Visible = true;
                    pbMicLevel.Visible = true;
                    iconPanel.Visible = false;
                    isExpanded = true;
                }
                else
                {
                    // 收缩完成 - 显示图标
                    btnRecord.Visible = false;
                    lblSystemLabel.Visible = false;
                    pbSystemLevel.Visible = false;
                    lblMicLabel.Visible = false;
                    pbMicLevel.Visible = false;
                    iconPanel.Visible = true;
                    isExpanded = false;
                }
            }
        }
        
        // 更流畅的缓动函数：EaseInOutCubic
        private float EaseInOutCubic(float t)
        {
            if (t < 0.5f)
                return 4 * t * t * t;
            else
            {
                float f = (2 * t) - 2;
                return 1 + f * f * f / 2;
            }
        }

        private void UpdateWindowRegion()
        {
            // 根据窗口大小调整圆角半径
            int radius = this.Width == CollapsedSize.Width ? 15 : 8; // 展开状态使用较小的圆角
            var region = CreateRoundRectRgn(0, 0, this.Width, this.Height, radius, radius);
            if (region != IntPtr.Zero)
            {
                this.Region = System.Drawing.Region.FromHrgn(region);
            }
        }

        private void IconPanel_Paint(object? sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            Rectangle rect = new Rectangle(4, 4, 22, 22);
            
            if (isRecording)
            {
                // 录音状态：红色方形图标
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(220, 53, 69)))
                {
                    using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                    {
                        int radius = 4;
                        path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
                        path.AddArc(rect.X + rect.Width - radius, rect.Y, radius, radius, 270, 90);
                        path.AddArc(rect.X + rect.Width - radius, rect.Y + rect.Height - radius, radius, radius, 0, 90);
                        path.AddArc(rect.X, rect.Y + rect.Height - radius, radius, radius, 90, 90);
                        path.CloseAllFigures();
                        g.FillPath(brush, path);
                    }
                }
                using (SolidBrush innerBrush = new SolidBrush(Color.White))
                {
                    Rectangle innerRect = new Rectangle(rect.X + 6, rect.Y + 6, 10, 10);
                    g.FillRectangle(innerBrush, innerRect);
                }
            }
            else
            {
                // 就绪状态：绿色圆形图标
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(76, 175, 80)))
                {
                    g.FillEllipse(brush, rect);
                }
                using (SolidBrush innerBrush = new SolidBrush(Color.White))
                {
                    Rectangle innerRect = new Rectangle(rect.X + 7, rect.Y + 7, 8, 8);
                    g.FillEllipse(innerBrush, innerRect);
                }
            }
        }

        private void DesktopRecorderWindow_MouseEnter(object? sender, EventArgs e)
        {
            hoverCheckTimer.Stop(); // 停止检查Timer
            if (!isExpanded && !isAnimating)
            {
                ExpandWindow();
            }
        }

        private void DesktopRecorderWindow_MouseLeave(object? sender, EventArgs e)
        {
            // 启动延迟检查Timer，避免鼠标快速移动导致的误触发
            if (isExpanded && !isAnimating)
            {
                hoverCheckTimer.Start();
            }
        }

        private void ExpandWindow()
        {
            if (isExpanded || isAnimating) return;
            
            isAnimating = true;
            isExpandingAnimation = true;
            animationStep = 0;
            
            iconPanel.Visible = false;
            animationTimer.Start();
        }

        private void CollapseWindow()
        {
            if (!isExpanded || isAnimating) return;
            
            isAnimating = true;
            isExpandingAnimation = false;
            animationStep = 0;
            
            // 隐藏详细控件
            btnRecord.Visible = false;
            lblSystemLabel.Visible = false;
            pbSystemLevel.Visible = false;
            lblMicLabel.Visible = false;
            pbMicLevel.Visible = false;
            
            animationTimer.Start();
        }

        private void BtnRecord_Click(object? sender, EventArgs e)
        {
            if (recorder == null) return;

            if (!isRecording)
            {
                // 开始录制
                recorder.StartRecording();
                isRecording = true;
                UpdateButtonText();
                iconPanel.Invalidate();
                
                // 通知WebSocket服务器状态变化
                NotifyWebSocketClients("recording_started", new { IsRecording = true });
            }
            else
            {
                // 停止录制
                recorder.StopRecording();
                isRecording = false;
                UpdateButtonText();
                iconPanel.Invalidate();
                
                // 通知WebSocket服务器状态变化
                NotifyWebSocketClients("recording_stopped", new { IsRecording = false });
            }
        }

        private void OnStatusChanged(object? sender, string status)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnStatusChanged(sender, status)));
                return;
            }

            // 同步录音状态
            bool wasRecording = isRecording;
            isRecording = status.Contains("录制") || status.Contains("Recording");
            
            if (wasRecording != isRecording)
            {
                UpdateButtonText();
                iconPanel.Invalidate();
                
                // 通知WebSocket客户端状态变化
                string command = isRecording ? "recording_started" : "recording_stopped";
                NotifyWebSocketClients(command, new { IsRecording = isRecording, Status = status });
            }
        }

        private void OnErrorOccurred(object? sender, Exception error)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnErrorOccurred(sender, error)));
                return;
            }

            // 错误处理
            MessageBox.Show($"录音错误: {error.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void OnWebSocketStatusChanged(object? sender, string message)
        {
            System.Diagnostics.Debug.WriteLine($"WebSocket: {message}");
        }

        private void NotifyWebSocketClients(string command, object data)
        {
            if (webSocketServer != null)
            {
                _ = Task.Run(async () =>
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
                        
                        System.Diagnostics.Debug.WriteLine($"广播消息: {command} - {System.Text.Json.JsonSerializer.Serialize(data)}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"广播失败: {ex.Message}");
                    }
                });
            }
        }

        private void OnAudioLevelChanged(object? sender, AudioLevelEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnAudioLevelChanged(sender, e)));
                return;
            }

            // 转换为分贝并映射到进度条
            float systemDb = e.SystemLevel > 0 ? 20 * (float)Math.Log10(e.SystemLevel) : -80;
            float micDb = e.MicrophoneLevel > 0 ? 20 * (float)Math.Log10(e.MicrophoneLevel) : -80;

            // 将-60dB到0dB映射到0-100
            int systemProgress = Math.Max(0, Math.Min(100, (int)((systemDb + 60) / 60 * 100)));
            int micProgress = Math.Max(0, Math.Min(100, (int)((micDb + 60) / 60 * 100)));

            pbSystemLevel.Value = systemProgress;
            pbMicLevel.Value = micProgress;

            // 根据电平调整颜色
            if (systemProgress > 90)
                pbSystemLevel.ForeColor = Color.FromArgb(220, 53, 69);
            else if (systemProgress > 70)
                pbSystemLevel.ForeColor = Color.FromArgb(255, 193, 7);
            else
                pbSystemLevel.ForeColor = Color.FromArgb(33, 150, 243);

            if (micProgress > 90)
                pbMicLevel.ForeColor = Color.FromArgb(220, 53, 69);
            else if (micProgress > 70)
                pbMicLevel.ForeColor = Color.FromArgb(255, 193, 7);
            else
                pbMicLevel.ForeColor = Color.FromArgb(255, 152, 0);
        }

        // 窗口拖动功能
        private void DesktopRecorderWindow_MouseDown(object? sender, MouseEventArgs e)
        {
            isDragging = true;
            lastCursor = Cursor.Position;
            lastForm = this.Location;
        }

        private void DesktopRecorderWindow_MouseMove(object? sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point diff = Point.Subtract(Cursor.Position, new Size(lastCursor));
                this.Location = Point.Add(lastForm, new Size(diff));
            }
        }

        private void DesktopRecorderWindow_MouseUp(object? sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                
                // 更新收缩位置
                if (!isExpanded && !isAnimating)
                {
                    collapsedLocation = this.Location;
                }
                else if (isExpanded && !isAnimating)
                {
                    collapsedLocation = new Point(
                        this.Location.X + (ExpandedSize.Width - CollapsedSize.Width),
                        this.Location.Y
                    );
                }
            }
        }

        // Windows API for rounded corners
        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            uiUpdateTimer?.Stop();
            uiUpdateTimer?.Dispose();
            animationTimer?.Stop();
            animationTimer?.Dispose();
            hoverCheckTimer?.Stop();
            hoverCheckTimer?.Dispose();
            webSocketServer?.Stop();
            webSocketServer?.Dispose();
            recorder?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
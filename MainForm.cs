using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AudioRecorder
{
    public partial class MainForm : Form
    {
        private AudioRecorderEngine audioEngine;
        private WebSocketServer webSocketServer;
        private Button btnStartRecord = null!;
        private Button btnStopRecord = null!;
        private CheckBox chkSystemAudio = null!;
        private CheckBox chkMicrophone = null!;
        private ComboBox cmbMicrophones = null!;
        private TextBox txtStatus = null!;
        private Label lblStatus = null!;
        private Label lblMicrophone = null!;
        private RadioButton rbMixed = null!;
        private RadioButton rbSeparate = null!;
        private GroupBox gbRecordingMode = null!;
        private Label lblModeDescription = null!;
        private TrackBar tbMicGain = null!;
        private Label lblMicGain = null!;
        private Label lblMicGainValue = null!;
        private Button btnStartWebSocket = null!;
        private Button btnStopWebSocket = null!;
        private Label lblWebSocketStatus = null!;
        private NumericUpDown nudPort = null!;
        private Label lblPort = null!;
        
        // 新增高质量音频控件
        private ComboBox cmbAudioQuality = null!;
        private Label lblAudioQuality = null!;
        private CheckBox chkNoiseReduction = null!;
        private CheckBox chkDynamicCompression = null!;
        private GroupBox gbAudioProcessing = null!;

        public MainForm()
        {
            audioEngine = new AudioRecorderEngine();
            webSocketServer = new WebSocketServer(audioEngine);
            InitializeComponent();
            SetupEventHandlers();
            LoadMicrophones();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form设置
            this.Text = "NAudio 高质量音频录制器 - 防失真优化版 + WebSocket";
            this.Size = new Size(540, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // 系统音频复选框
            chkSystemAudio = new CheckBox();
            chkSystemAudio.Text = "录制系统音频（立体声混音）";
            chkSystemAudio.Location = new Point(20, 20);
            chkSystemAudio.Size = new Size(200, 20);
            chkSystemAudio.Checked = true;
            this.Controls.Add(chkSystemAudio);

            // 麦克风复选框
            chkMicrophone = new CheckBox();
            chkMicrophone.Text = "录制麦克风音频";
            chkMicrophone.Location = new Point(20, 50);
            chkMicrophone.Size = new Size(150, 20);
            chkMicrophone.Checked = true;
            this.Controls.Add(chkMicrophone);

            // 录制模式群组框
            gbRecordingMode = new GroupBox();
            gbRecordingMode.Text = "录制模式";
            gbRecordingMode.Location = new Point(20, 80);
            gbRecordingMode.Size = new Size(480, 80);
            this.Controls.Add(gbRecordingMode);

            // 混合模式单选按钮
            rbMixed = new RadioButton();
            rbMixed.Text = "混合模式 - 将系统音频和麦克风合并到单个高质量文件";
            rbMixed.Location = new Point(10, 20);
            rbMixed.Size = new Size(350, 20);
            rbMixed.Checked = true;
            gbRecordingMode.Controls.Add(rbMixed);

            // 分离模式单选按钮
            rbSeparate = new RadioButton();
            rbSeparate.Text = "分离模式 - 系统音频和麦克风分别保存";
            rbSeparate.Location = new Point(10, 45);
            rbSeparate.Size = new Size(280, 20);
            gbRecordingMode.Controls.Add(rbSeparate);

            // 音质设置标签
            lblAudioQuality = new Label();
            lblAudioQuality.Text = "音频质量:";
            lblAudioQuality.Location = new Point(20, 175);
            lblAudioQuality.Size = new Size(80, 20);
            this.Controls.Add(lblAudioQuality);

            // 音质选择下拉框
            cmbAudioQuality = new ComboBox();
            cmbAudioQuality.Location = new Point(110, 173);
            cmbAudioQuality.Size = new Size(200, 25);
            cmbAudioQuality.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbAudioQuality.Items.AddRange(new string[] {
                "标准质量 (44.1kHz/16bit)",
                "高质量 (48kHz/24bit) - 推荐",
                "录音棚质量 (96kHz/32bit)"
            });
            cmbAudioQuality.SelectedIndex = 1; // 默认选择高质量
            this.Controls.Add(cmbAudioQuality);

            // 音频处理群组框
            gbAudioProcessing = new GroupBox();
            gbAudioProcessing.Text = "音频处理增强";
            gbAudioProcessing.Location = new Point(20, 210);
            gbAudioProcessing.Size = new Size(480, 80);
            this.Controls.Add(gbAudioProcessing);

            // 降噪复选框
            chkNoiseReduction = new CheckBox();
            chkNoiseReduction.Text = "启用降噪处理 (减少背景噪音)";
            chkNoiseReduction.Location = new Point(10, 20);
            chkNoiseReduction.Size = new Size(200, 20);
            chkNoiseReduction.Checked = true;
            gbAudioProcessing.Controls.Add(chkNoiseReduction);

            // 动态范围压缩复选框
            chkDynamicCompression = new CheckBox();
            chkDynamicCompression.Text = "启用动态范围压缩 (平衡音量)";
            chkDynamicCompression.Location = new Point(10, 45);
            chkDynamicCompression.Size = new Size(200, 20);
            chkDynamicCompression.Checked = true;
            gbAudioProcessing.Controls.Add(chkDynamicCompression);

            // 模式描述标签
            lblModeDescription = new Label();
            lblModeDescription.Text = "混合模式：使用48kHz/24bit高质量录制，已优化音频处理算法";
            lblModeDescription.Location = new Point(20, 300);
            lblModeDescription.Size = new Size(480, 20);
            lblModeDescription.ForeColor = Color.Blue;
            lblModeDescription.Font = new Font("Microsoft YaHei", 8.5F, FontStyle.Italic);
            this.Controls.Add(lblModeDescription);

            // 麦克风选择标签
            lblMicrophone = new Label();
            lblMicrophone.Text = "选择麦克风:";
            lblMicrophone.Location = new Point(20, 330);
            lblMicrophone.Size = new Size(80, 20);
            this.Controls.Add(lblMicrophone);

            // 麦克风下拉框
            cmbMicrophones = new ComboBox();
            cmbMicrophones.Location = new Point(110, 328);
            cmbMicrophones.Size = new Size(320, 25);
            cmbMicrophones.DropDownStyle = ComboBoxStyle.DropDownList;
            this.Controls.Add(cmbMicrophones);

            // 麦克风增益标签
            lblMicGain = new Label();
            lblMicGain.Text = "麦克风增益:";
            lblMicGain.Location = new Point(20, 365);
            lblMicGain.Size = new Size(80, 20);
            this.Controls.Add(lblMicGain);

            // 麦克风增益值标签
            lblMicGainValue = new Label();
            lblMicGainValue.Text = "4.0x";
            lblMicGainValue.Location = new Point(450, 365);
            lblMicGainValue.Size = new Size(50, 20);
            lblMicGainValue.TextAlign = ContentAlignment.MiddleRight;
            lblMicGainValue.Font = new Font("Microsoft YaHei", 9F, FontStyle.Bold);
            lblMicGainValue.ForeColor = Color.DarkOrange;
            this.Controls.Add(lblMicGainValue);

            // 麦克风增益滑块
            tbMicGain = new TrackBar();
            tbMicGain.Location = new Point(110, 360);
            tbMicGain.Size = new Size(330, 45);
            tbMicGain.Minimum = 1;  // 0.1x
            tbMicGain.Maximum = 80; // 8.0x
            tbMicGain.Value = 40;   // 4.0x
            tbMicGain.TickFrequency = 10;
            tbMicGain.SmallChange = 1;
            tbMicGain.LargeChange = 10;
            this.Controls.Add(tbMicGain);

            // WebSocket端口标签
            lblPort = new Label();
            lblPort.Text = "WebSocket端口:";
            lblPort.Location = new Point(20, 415);
            lblPort.Size = new Size(100, 20);
            this.Controls.Add(lblPort);

            // WebSocket端口输入框
            nudPort = new NumericUpDown();
            nudPort.Location = new Point(130, 413);
            nudPort.Size = new Size(80, 25);
            nudPort.Minimum = 1000;
            nudPort.Maximum = 65535;
            nudPort.Value = 8080;
            this.Controls.Add(nudPort);

            // 启动WebSocket按钮
            btnStartWebSocket = new Button();
            btnStartWebSocket.Text = "启动WebSocket";
            btnStartWebSocket.Location = new Point(220, 410);
            btnStartWebSocket.Size = new Size(110, 30);
            btnStartWebSocket.BackColor = Color.LightBlue;
            btnStartWebSocket.Font = new Font("Microsoft YaHei", 8F, FontStyle.Bold);
            this.Controls.Add(btnStartWebSocket);

            // 停止WebSocket按钮
            btnStopWebSocket = new Button();
            btnStopWebSocket.Text = "停止WebSocket";
            btnStopWebSocket.Location = new Point(340, 410);
            btnStopWebSocket.Size = new Size(110, 30);
            btnStopWebSocket.BackColor = Color.LightCoral;
            btnStopWebSocket.Font = new Font("Microsoft YaHei", 8F, FontStyle.Bold);
            btnStopWebSocket.Enabled = false;
            this.Controls.Add(btnStopWebSocket);

            // WebSocket状态标签
            lblWebSocketStatus = new Label();
            lblWebSocketStatus.Text = "WebSocket: 未启动";
            lblWebSocketStatus.Location = new Point(20, 450);
            lblWebSocketStatus.Size = new Size(480, 20);
            lblWebSocketStatus.ForeColor = Color.Gray;
            lblWebSocketStatus.Font = new Font("Microsoft YaHei", 8.5F, FontStyle.Bold);
            this.Controls.Add(lblWebSocketStatus);

            // 开始录制按钮
            btnStartRecord = new Button();
            btnStartRecord.Text = "🎵 开始高质量录制";
            btnStartRecord.Location = new Point(20, 480);
            btnStartRecord.Size = new Size(150, 40);
            btnStartRecord.BackColor = Color.LightGreen;
            btnStartRecord.Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold);
            this.Controls.Add(btnStartRecord);

            // 停止录制按钮
            btnStopRecord = new Button();
            btnStopRecord.Text = "⏹️ 停止录制";
            btnStopRecord.Location = new Point(190, 480);
            btnStopRecord.Size = new Size(120, 40);
            btnStopRecord.BackColor = Color.LightCoral;
            btnStopRecord.Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold);
            btnStopRecord.Enabled = false;
            this.Controls.Add(btnStopRecord);

            // 状态标签
            lblStatus = new Label();
            lblStatus.Text = "状态信息:";
            lblStatus.Location = new Point(20, 535);
            lblStatus.Size = new Size(70, 20);
            this.Controls.Add(lblStatus);

            // 状态文本框
            txtStatus = new TextBox();
            txtStatus.Location = new Point(20, 560);
            txtStatus.Size = new Size(480, 160);
            txtStatus.Multiline = true;
            txtStatus.ScrollBars = ScrollBars.Vertical;
            txtStatus.ReadOnly = true;
            txtStatus.BackColor = Color.WhiteSmoke;
            this.Controls.Add(txtStatus);

            this.ResumeLayout(false);
        }

        private void SetupEventHandlers()
        {
            btnStartRecord.Click += BtnStartRecord_Click;
            btnStopRecord.Click += BtnStopRecord_Click;
            btnStartWebSocket.Click += BtnStartWebSocket_Click;
            btnStopWebSocket.Click += BtnStopWebSocket_Click;
            cmbMicrophones.SelectedIndexChanged += CmbMicrophones_SelectedIndexChanged;
            rbMixed.CheckedChanged += RbMixed_CheckedChanged;
            rbSeparate.CheckedChanged += RbSeparate_CheckedChanged;
            tbMicGain.ValueChanged += TbMicGain_ValueChanged;
            cmbAudioQuality.SelectedIndexChanged += CmbAudioQuality_SelectedIndexChanged;
            chkNoiseReduction.CheckedChanged += ChkNoiseReduction_CheckedChanged;
            chkDynamicCompression.CheckedChanged += ChkDynamicCompression_CheckedChanged;
            
            audioEngine.StatusChanged += AudioEngine_StatusChanged;
            audioEngine.ErrorOccurred += AudioEngine_ErrorOccurred;
            webSocketServer.StatusChanged += WebSocketServer_StatusChanged;

            this.FormClosing += MainForm_FormClosing;
        }

        private void CmbAudioQuality_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (!audioEngine.IsRecording)
            {
                AudioQuality quality = (AudioQuality)cmbAudioQuality.SelectedIndex;
                audioEngine.Quality = quality;
                
                string qualityDesc = quality switch
                {
                    AudioQuality.Standard => "标准CD质量",
                    AudioQuality.High => "专业级高质量 (推荐)",
                    AudioQuality.Studio => "录音棚级别质量",
                    _ => "高质量"
                };
                
                AppendStatus($"音频质量已设置为: {qualityDesc}");
                UpdateModeDescription();
            }
        }

        private void ChkNoiseReduction_CheckedChanged(object? sender, EventArgs e)
        {
            audioEngine.EnableNoiseReduction = chkNoiseReduction.Checked;
            AppendStatus($"降噪处理: {(chkNoiseReduction.Checked ? "已启用" : "已禁用")}");
        }

        private void ChkDynamicCompression_CheckedChanged(object? sender, EventArgs e)
        {
            audioEngine.EnableDynamicRangeCompression = chkDynamicCompression.Checked;
            AppendStatus($"动态范围压缩: {(chkDynamicCompression.Checked ? "已启用" : "已禁用")}");
        }

        private async void BtnStartWebSocket_Click(object? sender, EventArgs e)
        {
            try
            {
                webSocketServer.Dispose();
                webSocketServer = new WebSocketServer(audioEngine, (int)nudPort.Value);
                webSocketServer.StatusChanged += WebSocketServer_StatusChanged;
                
                await webSocketServer.StartAsync();
                
                btnStartWebSocket.Enabled = false;
                btnStopWebSocket.Enabled = true;
                nudPort.Enabled = false;
                lblWebSocketStatus.Text = $"WebSocket: 运行中 (端口 {nudPort.Value})";
                lblWebSocketStatus.ForeColor = Color.Green;
            }
            catch (Exception ex)
            {
                AppendStatus($"启动WebSocket服务器失败: {ex.Message}");
                lblWebSocketStatus.Text = "WebSocket: 启动失败";
                lblWebSocketStatus.ForeColor = Color.Red;
            }
        }

        private void BtnStopWebSocket_Click(object? sender, EventArgs e)
        {
            try
            {
                webSocketServer.Stop();
                btnStartWebSocket.Enabled = true;
                btnStopWebSocket.Enabled = false;
                nudPort.Enabled = true;
                lblWebSocketStatus.Text = "WebSocket: 已停止";
                lblWebSocketStatus.ForeColor = Color.Gray;
            }
            catch (Exception ex)
            {
                AppendStatus($"停止WebSocket服务器失败: {ex.Message}");
            }
        }

        private void WebSocketServer_StatusChanged(object? sender, string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AppendStatus($"[WebSocket] {message}")));
            }
            else
            {
                AppendStatus($"[WebSocket] {message}");
            }
        }

        private void TbMicGain_ValueChanged(object? sender, EventArgs e)
        {
            float gainValue = tbMicGain.Value / 10.0f;
            audioEngine.MicrophoneGain = gainValue;
            lblMicGainValue.Text = $"{gainValue:F1}x";
            
            if (gainValue < 1.0f)
            {
                lblMicGainValue.ForeColor = Color.DarkRed;
            }
            else if (gainValue <= 2.5f)
            {
                lblMicGainValue.ForeColor = Color.DarkGreen;
            }
            else if (gainValue <= 4.0f)
            {
                lblMicGainValue.ForeColor = Color.DarkOrange;
            }
            else
            {
                lblMicGainValue.ForeColor = Color.Red;
            }

            if (!audioEngine.IsRecording)
            {
                string qualityTip = "";
                if (gainValue <= 2.5f)
                    qualityTip = " (推荐范围，音质最佳)";
                else if (gainValue <= 4.0f)
                    qualityTip = " (注意音质)";
                else
                    qualityTip = " (可能失真，建议降低)";
                    
                AppendStatus($"麦克风增益已调整为: {gainValue:F1}x{qualityTip}");
            }
        }

        private void RbMixed_CheckedChanged(object? sender, EventArgs e)
        {
            if (rbMixed.Checked)
            {
                UpdateModeDescription();
                btnStartRecord.Text = "🎵 开始高质量混合录制";
            }
        }

        private void RbSeparate_CheckedChanged(object? sender, EventArgs e)
        {
            if (rbSeparate.Checked)
            {
                lblModeDescription.Text = "分离模式：系统音频和麦克风分别保存为独立的高质量WAV文件";
                btnStartRecord.Text = "🎵 开始高质量分离录制";
            }
        }

        private void UpdateModeDescription()
        {
            if (rbMixed.Checked)
            {
                string qualityText = cmbAudioQuality.SelectedIndex switch
                {
                    0 => "44.1kHz/16bit",
                    1 => "48kHz/24bit",
                    2 => "96kHz/32bit",
                    _ => "48kHz/24bit"
                };
                lblModeDescription.Text = $"混合模式：使用{qualityText}高质量录制，已优化音频处理算法";
            }
        }

        private void LoadMicrophones()
        {
            try
            {
                var microphones = audioEngine.GetAvailableMicrophones();
                cmbMicrophones.Items.AddRange(microphones);
                if (cmbMicrophones.Items.Count > 0)
                {
                    cmbMicrophones.SelectedIndex = 0;
                    AppendStatus("🎵 高质量音频录制器已启动");
                    AppendStatus("✨ 默认使用48kHz/24bit专业级质量");
                    AppendStatus("🔧 已启用降噪和动态范围压缩");
                    AppendStatus("🌐 支持WebSocket远程控制功能");
                    AppendStatus($"🎚️ 当前麦克风增益: {audioEngine.MicrophoneGain:F1}x (已优化)");
                    AppendStatus("🎯 麦克风增益已提高至4.0x，确保清晰录音");
                }
                else
                {
                    AppendStatus("⚠️ 警告: 未找到可用的麦克风设备");
                    chkMicrophone.Enabled = false;
                    tbMicGain.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                AppendStatus($"❌ 加载麦克风设备时出错: {ex.Message}");
            }
        }

        private void BtnStartRecord_Click(object? sender, EventArgs e)
        {
            if (!chkSystemAudio.Checked && !chkMicrophone.Checked)
            {
                MessageBox.Show("请至少选择一种录制源（系统音频或麦克风）", "提示", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                RecordingMode mode = rbMixed.Checked ? RecordingMode.Mixed : RecordingMode.Separate;
                
                string sources = "";
                if (chkSystemAudio.Checked && chkMicrophone.Checked)
                    sources = "系统音频 + 麦克风";
                else if (chkSystemAudio.Checked)
                    sources = "仅系统音频";
                else
                    sources = "仅麦克风";

                string modeText = rbMixed.Checked ? "混合" : "分离";
                string qualityText = cmbAudioQuality.Text;
                
                AppendStatus($"🎵 开始录制 - 模式: {modeText}, 音源: {sources}");
                AppendStatus($"🎧 音频质量: {qualityText}");
                
                if (chkMicrophone.Checked)
                {
                    AppendStatus($"🎚️ 麦克风增益: {audioEngine.MicrophoneGain:F1}x");
                }

                audioEngine.StartRecording(chkSystemAudio.Checked, chkMicrophone.Checked, mode);
                
                btnStartRecord.Enabled = false;
                btnStopRecord.Enabled = true;
                chkSystemAudio.Enabled = false;
                chkMicrophone.Enabled = false;
                cmbMicrophones.Enabled = false;
                rbMixed.Enabled = false;
                rbSeparate.Enabled = false;
                tbMicGain.Enabled = false;
                cmbAudioQuality.Enabled = false;
                chkNoiseReduction.Enabled = false;
                chkDynamicCompression.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动录制时出错: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnStopRecord_Click(object? sender, EventArgs e)
        {
            try
            {
                audioEngine.StopRecording();
                btnStartRecord.Enabled = true;
                btnStopRecord.Enabled = false;
                chkSystemAudio.Enabled = true;
                chkMicrophone.Enabled = true;
                cmbMicrophones.Enabled = true;
                rbMixed.Enabled = true;
                rbSeparate.Enabled = true;
                tbMicGain.Enabled = true;
                cmbAudioQuality.Enabled = true;
                chkNoiseReduction.Enabled = true;
                chkDynamicCompression.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"停止录制时出错: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CmbMicrophones_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbMicrophones.SelectedIndex >= 0 && !audioEngine.IsRecording)
            {
                audioEngine.SetMicrophoneDevice(cmbMicrophones.SelectedIndex);
                AppendStatus($"🎤 已选择麦克风: {cmbMicrophones.SelectedItem}");
            }
        }

        private void AudioEngine_StatusChanged(object? sender, string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AppendStatus(message)));
            }
            else
            {
                AppendStatus(message);
            }
        }

        private void AudioEngine_ErrorOccurred(object? sender, Exception ex)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AppendStatus($"❌ 错误: {ex.Message}")));
            }
            else
            {
                AppendStatus($"❌ 错误: {ex.Message}");
            }
        }

        private void AppendStatus(string message)
        {
            string timeStamp = DateTime.Now.ToString("HH:mm:ss");
            txtStatus.AppendText($"[{timeStamp}] {message}\r\n");
            txtStatus.ScrollToCaret();
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            webSocketServer?.Dispose();
            audioEngine?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                webSocketServer?.Dispose();
                audioEngine?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
} 
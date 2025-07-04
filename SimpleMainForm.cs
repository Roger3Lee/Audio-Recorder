using System;
using System.Drawing;
using System.Windows.Forms;

namespace AudioRecorder
{
    public partial class SimpleMainForm : Form
    {
        private SimpleAudioRecorder? recorder;
        private Button btnStartStop = null!;
        private TextBox txtStatus = null!;
        
        public SimpleMainForm()
        {
            InitializeComponent();
            InitializeRecorder();
        }
        
        private void InitializeComponent()
        {
            // 窗体设置
            this.Text = "简易音频录制器";
            this.Size = new Size(400, 300);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            
            // 开始/停止按钮
            btnStartStop = new Button
            {
                Text = "开始录制",
                Size = new Size(200, 60),
                Location = new Point(100, 30),
                Font = new Font("Microsoft YaHei", 14, FontStyle.Bold),
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnStartStop.FlatAppearance.BorderSize = 0;
            btnStartStop.Click += BtnStartStop_Click;
            
            // 状态文本框
            txtStatus = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Size = new Size(360, 150),
                Location = new Point(20, 110),
                Font = new Font("Consolas", 9),
                BackColor = Color.FromArgb(240, 240, 240),
                BorderStyle = BorderStyle.FixedSingle
            };
            
            // 添加控件
            this.Controls.AddRange(new Control[] { btnStartStop, txtStatus });
        }
        
        private void InitializeRecorder()
        {
            recorder = new SimpleAudioRecorder();
            recorder.StatusChanged += OnStatusChanged;
            recorder.ErrorOccurred += OnErrorOccurred;
            
            AddStatus("录制器已初始化");
            AddStatus("点击按钮开始录制系统音频和麦克风");
        }
        
        private void BtnStartStop_Click(object? sender, EventArgs e)
        {
            if (recorder == null) return;
            
            if (!recorder.IsRecording)
            {
                // 开始录制
                recorder.StartRecording();
                btnStartStop.Text = "停止录制";
                btnStartStop.BackColor = Color.FromArgb(244, 67, 54);
            }
            else
            {
                // 停止录制
                recorder.StopRecording();
                btnStartStop.Text = "开始录制";
                btnStartStop.BackColor = Color.FromArgb(76, 175, 80);
            }
        }
        
        private void OnStatusChanged(object? sender, string status)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AddStatus(status)));
            }
            else
            {
                AddStatus(status);
            }
        }
        
        private void OnErrorOccurred(object? sender, Exception error)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AddStatus($"错误: {error.Message}")));
            }
            else
            {
                AddStatus($"错误: {error.Message}");
                MessageBox.Show(error.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void AddStatus(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            txtStatus.AppendText($"[{timestamp}] {message}\r\n");
            txtStatus.ScrollToCaret();
        }
        
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            recorder?.Dispose();
            base.OnFormClosed(e);
        }
    }
} 
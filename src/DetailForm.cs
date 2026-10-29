using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MouseBatteryDisplay
{
    internal sealed class BatteryGauge : Control
    {
        private int? _percent;
        private bool _charging;
        private bool _connected;
        private bool _stale;

        internal BatteryGauge()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);
            Height = 14;
        }

        internal void SetValue(int? percent, bool charging, bool connected, bool stale)
        {
            _percent = percent;
            _charging = charging;
            _connected = connected;
            _stale = stale;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle track = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = DetailForm.RoundedRectangle(track, Height / 2f))
            using (SolidBrush background = new SolidBrush(Color.FromArgb(255, 49, 51, 58)))
            {
                graphics.FillPath(background, path);
            }

            if (_percent.HasValue)
            {
                int fillWidth = (int)Math.Round((Width - 2) * (_percent.Value / 100.0));
                if (_percent.Value > 0 && fillWidth < 1) fillWidth = 1;
                if (fillWidth > 0)
                {
                    Rectangle fill = new Rectangle(1, 1, Math.Max(0, fillWidth - 1), Height - 3);
                    using (GraphicsPath path = DetailForm.RoundedRectangle(fill, Math.Max(1f, fill.Height / 2f)))
                    using (SolidBrush brush = new SolidBrush(DetailForm.AccentColor(_percent, _charging, _connected, _stale)))
                    {
                        graphics.FillPath(brush, path);
                    }
                }
            }

            using (Pen border = new Pen(Color.FromArgb(255, 75, 78, 88), 1f))
            using (GraphicsPath path = DetailForm.RoundedRectangle(track, Height / 2f))
            {
                graphics.DrawPath(border, path);
            }
        }
    }

    internal sealed class DetailForm : Form
    {
        private readonly BatteryMonitor _monitor;
        private readonly Action _refreshAction;
        private readonly Action _exitAction;
        private readonly Label _deviceLabel;
        private readonly Label _statusLabel;
        private readonly Label _percentLabel;
        private readonly Label _modeLabel;
        private readonly Label _updatedLabel;
        private readonly Label _footerLabel;
        private readonly Label _exitLabel;
        private readonly Button _refreshButton;
        private readonly CheckBox _autoStartCheckBox;
        private readonly BatteryGauge _gauge;
        private readonly float _uiScale;
        private bool _updatingAutoStart;

        internal DetailForm(BatteryMonitor monitor, Action refreshAction, Action exitAction)
        {
            _monitor = monitor;
            _refreshAction = refreshAction;
            _exitAction = exitAction;

            _uiScale = GetUiScale();
            Text = "鼠标电量";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(344, 252);
            BackColor = Color.FromArgb(255, 24, 25, 29);
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            DoubleBuffered = true;
            KeyPreview = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            _deviceLabel = new Label();
            _deviceLabel.Text = "MCHOSE A7 V2 Pro";
            _deviceLabel.ForeColor = Color.FromArgb(255, 248, 249, 251);
            _deviceLabel.Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold, GraphicsUnit.Point);
            _deviceLabel.AutoSize = true;
            _deviceLabel.Location = new Point(20, 16);

            _statusLabel = new Label();
            _statusLabel.Text = "正在检测...";
            _statusLabel.ForeColor = Color.FromArgb(255, 156, 163, 175);
            _statusLabel.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            _statusLabel.AutoSize = true;
            _statusLabel.Location = new Point(20, 40);

            _percentLabel = new Label();
            _percentLabel.Text = "--";
            _percentLabel.ForeColor = Color.FromArgb(255, 156, 163, 175);
            _percentLabel.Font = new Font("Segoe UI Semibold", 32f, FontStyle.Bold, GraphicsUnit.Point);
            _percentLabel.AutoSize = false;
            _percentLabel.Size = new Size(130, 58);
            _percentLabel.Location = new Point(18, 68);
            _percentLabel.TextAlign = ContentAlignment.MiddleLeft;

            _modeLabel = new Label();
            _modeLabel.Text = "等待数据";
            _modeLabel.ForeColor = Color.FromArgb(255, 209, 213, 219);
            _modeLabel.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            _modeLabel.AutoSize = true;
            _modeLabel.Location = new Point(154, 76);

            _gauge = new BatteryGauge();
            _gauge.Location = new Point(154, 102);
            _gauge.Size = new Size(170, 14);

            _updatedLabel = new Label();
            _updatedLabel.Text = "尚未读取到数据";
            _updatedLabel.ForeColor = Color.FromArgb(255, 156, 163, 175);
            _updatedLabel.Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Regular, GraphicsUnit.Point);
            _updatedLabel.AutoSize = true;
            _updatedLabel.Location = new Point(20, 144);

            Panel separator = new Panel();
            separator.BackColor = Color.FromArgb(255, 45, 47, 53);
            separator.Location = new Point(20, 170);
            separator.Size = new Size(304, 1);

            _refreshButton = new Button();
            _refreshButton.Text = "立即刷新";
            _refreshButton.FlatStyle = FlatStyle.Flat;
            _refreshButton.FlatAppearance.BorderSize = 0;
            _refreshButton.BackColor = Color.FromArgb(255, 37, 99, 235);
            _refreshButton.ForeColor = Color.White;
            _refreshButton.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
            _refreshButton.Location = new Point(20, 184);
            _refreshButton.Size = new Size(96, 32);
            _refreshButton.Cursor = Cursors.Hand;
            _refreshButton.Click += OnRefreshClick;
            _refreshButton.MouseEnter += delegate { _refreshButton.BackColor = Color.FromArgb(255, 59, 130, 246); };
            _refreshButton.MouseLeave += delegate { _refreshButton.BackColor = Color.FromArgb(255, 37, 99, 235); };

            _autoStartCheckBox = new CheckBox();
            _autoStartCheckBox.Text = "开机自启";
            _autoStartCheckBox.ForeColor = Color.FromArgb(255, 209, 213, 219);
            _autoStartCheckBox.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            _autoStartCheckBox.AutoSize = true;
            _autoStartCheckBox.Location = new Point(136, 190);
            _autoStartCheckBox.Cursor = Cursors.Hand;
            _autoStartCheckBox.CheckedChanged += OnAutoStartChanged;

            _exitLabel = new Label();
            _exitLabel.Text = "退出";
            _exitLabel.ForeColor = Color.FromArgb(255, 156, 163, 175);
            _exitLabel.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            _exitLabel.AutoSize = true;
            _exitLabel.Location = new Point(298, 195);
            _exitLabel.Cursor = Cursors.Hand;
            _exitLabel.Click += delegate { if (_exitAction != null) _exitAction(); };
            _exitLabel.MouseEnter += delegate { _exitLabel.ForeColor = Color.White; };
            _exitLabel.MouseLeave += delegate { _exitLabel.ForeColor = Color.FromArgb(255, 156, 163, 175); };

            _footerLabel = new Label();
            _footerLabel.Text = "安全只读 · 每 10 分钟自动刷新";
            _footerLabel.ForeColor = Color.FromArgb(255, 107, 114, 128);
            _footerLabel.Font = new Font("Microsoft YaHei UI", 7.5f, FontStyle.Regular, GraphicsUnit.Point);
            _footerLabel.AutoSize = true;
            _footerLabel.Location = new Point(20, 227);

            Controls.Add(_deviceLabel);
            Controls.Add(_statusLabel);
            Controls.Add(_percentLabel);
            Controls.Add(_modeLabel);
            Controls.Add(_gauge);
            Controls.Add(_updatedLabel);
            Controls.Add(separator);
            Controls.Add(_refreshButton);
            Controls.Add(_autoStartCheckBox);
            Controls.Add(_exitLabel);
            Controls.Add(_footerLabel);

            ApplyDpiScale();
            SetStatus(monitor.Current);
        }

        private static float GetUiScale()
        {
            try
            {
                uint dpi = NativeMethods.GetDpiForSystem();
                if (dpi >= 96) return dpi / 96f;
            }
            catch
            {
            }

            try
            {
                using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
                {
                    return Math.Max(1f, graphics.DpiX / 96f);
                }
            }
            catch
            {
                return 1f;
            }
        }

        private void ApplyDpiScale()
        {
            if (_uiScale <= 1.01f) return;

            ClientSize = new Size(Scaled(ClientSize.Width), Scaled(ClientSize.Height));
            foreach (Control control in Controls)
            {
                control.Location = new Point(Scaled(control.Left), Scaled(control.Top));
                if (!control.AutoSize)
                {
                    control.Size = new Size(Scaled(control.Width), Scaled(control.Height));
                }
            }
        }

        private int Scaled(int value)
        {
            return (int)Math.Round(value * _uiScale);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ClassStyle |= 0x20000; // CS_DROPSHADOW
                return parameters;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            using (GraphicsPath path = RoundedRectangle(new Rectangle(0, 0, Width, Height), 12f * _uiScale))
            {
                Region = new Region(path);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(Color.FromArgb(255, 58, 61, 70), 1f))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        }

        internal void SetStatus(BatteryStatus status)
        {
            if (IsDisposed) return;

            _deviceLabel.Text = status.DeviceName;
            _percentLabel.Text = status.Percent.HasValue ? status.Percent.Value + "%" : "--";
            _percentLabel.ForeColor = status.Percent.HasValue
                ? AccentColor(status.Percent, status.Charging, status.Connected, status.Stale)
                : Color.FromArgb(255, 107, 114, 128);

            if (status.Connected)
            {
                _statusLabel.Text = status.Charging ? "充电中" : "无线连接正常";
                _statusLabel.ForeColor = status.Charging
                    ? Color.FromArgb(255, 56, 189, 248)
                    : Color.FromArgb(255, 74, 222, 128);
            }
            else if (status.Stale)
            {
                _statusLabel.Text = "未检测到鼠标 · 显示上次数据";
                _statusLabel.ForeColor = Color.FromArgb(255, 251, 191, 36);
            }
            else
            {
                _statusLabel.Text = string.IsNullOrEmpty(status.Error) ? "未检测到鼠标" : status.Error;
                _statusLabel.ForeColor = Color.FromArgb(255, 156, 163, 175);
            }

            if (_monitor.IsRefreshing)
            {
                _modeLabel.Text = "正在读取...";
                _refreshButton.Enabled = false;
            }
            else
            {
                _modeLabel.Text = status.Connected
                    ? (status.PollingRate > 0 ? "轮询率 " + status.PollingRate + " Hz" : "无线连接正常")
                    : "请移动鼠标将其唤醒";
                _refreshButton.Enabled = true;
            }

            _gauge.SetValue(status.Percent, status.Charging, status.Connected, status.Stale);
            _updatedLabel.Text = status.UpdatedText;

            _updatingAutoStart = true;
            try
            {
                _autoStartCheckBox.Checked = AutoStart.IsEnabled();
            }
            finally
            {
                _updatingAutoStart = false;
            }
        }

        internal void PositionNearTaskbar()
        {
            Screen screen = Screen.FromPoint(Cursor.Position);
            if (screen == null) screen = Screen.PrimaryScreen;
            Rectangle area = screen.WorkingArea;
            int x = area.Right - Width - 10;
            int y = area.Bottom - Height - 10;
            if (x < area.Left + 10) x = area.Left + 10;
            if (y < area.Top + 10) y = area.Top + 10;
            Location = new Point(x, y);
            Logger.Info("详情面板: dpi=" + DeviceDpi + " size=" + Width + "x" + Height
                + " screen=" + area.Width + "x" + area.Height + " location=" + x + "," + y);
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            _modeLabel.Text = "正在读取...";
            _refreshButton.Enabled = false;
            if (_refreshAction != null) _refreshAction();
        }

        private void OnAutoStartChanged(object sender, EventArgs e)
        {
            if (_updatingAutoStart) return;
            bool enabled = _autoStartCheckBox.Checked;
            if (!AutoStart.SetEnabled(enabled))
            {
                MessageBox.Show(
                    this,
                    "无法修改开机自启设置。\n请检查当前用户是否有写入注册表的权限。",
                    "鼠标电量",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _updatingAutoStart = true;
                try
                {
                    _autoStartCheckBox.Checked = AutoStart.IsEnabled();
                }
                finally
                {
                    _updatingAutoStart = false;
                }
            }
        }

        internal static Color AccentColor(int? percent, bool charging, bool connected, bool stale)
        {
            if (charging) return Color.FromArgb(255, 56, 189, 248);
            if (!connected || stale || !percent.HasValue) return Color.FromArgb(255, 156, 163, 175);
            if (percent.Value <= 10) return Color.FromArgb(255, 239, 68, 68);
            if (percent.Value <= 25) return Color.FromArgb(255, 245, 158, 11);
            return Color.FromArgb(255, 74, 222, 128);
        }

        internal static GraphicsPath RoundedRectangle(Rectangle rectangle, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius * 2f;
            if (diameter > rectangle.Width) diameter = rectangle.Width;
            if (diameter > rectangle.Height) diameter = rectangle.Height;
            path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}

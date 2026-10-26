using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MouseBatteryDisplay
{
    internal sealed class TrayAppContext : ApplicationContext
    {
        private readonly bool _startedAtLogin;
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _deviceItem;
        private readonly ToolStripMenuItem _statusItem;
        private readonly ToolStripMenuItem _refreshItem;
        private readonly ToolStripMenuItem _detailItem;
        private readonly ToolStripMenuItem _autoStartItem;
        private readonly ToolStripMenuItem _exitItem;
        private readonly BatteryMonitor _monitor;
        private readonly Form _dispatcher;
        private readonly EventWaitHandle _showEvent;
        private readonly Thread _showThread;
        private readonly System.Windows.Forms.Timer _resumeTimer;
        private Icon _currentIcon;
        private DetailForm _detailForm;
        private bool _firstUpdateShown;
        private bool _disposed;
        private DateTime _lastLowBatteryAlert = DateTime.MinValue;

        internal TrayAppContext(EventWaitHandle showEvent)
        {
            _showEvent = showEvent;
            _startedAtLogin = HasArgument("--startup");

            _dispatcher = new Form();
            _dispatcher.ShowInTaskbar = false;
            _dispatcher.FormBorderStyle = FormBorderStyle.None;
            _dispatcher.StartPosition = FormStartPosition.Manual;
            _dispatcher.Location = new Point(-32000, -32000);
            _dispatcher.Size = new Size(1, 1);
            IntPtr unused = _dispatcher.Handle;

            _menu = new ContextMenuStrip();
            _menu.ShowImageMargin = false;
            _menu.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point);

            _deviceItem = new ToolStripMenuItem("MCHOSE A7 V2 Pro");
            _deviceItem.Enabled = false;
            _deviceItem.Font = new Font(_menu.Font, FontStyle.Bold);

            _statusItem = new ToolStripMenuItem("正在检测电量...");
            _statusItem.Enabled = false;

            _refreshItem = new ToolStripMenuItem("刷新电量");
            _refreshItem.Click += delegate { RefreshBattery(); };

            _detailItem = new ToolStripMenuItem("显示详情");
            _detailItem.Click += delegate { ShowDetail(); };

            _autoStartItem = new ToolStripMenuItem("开机自启");
            _autoStartItem.CheckOnClick = true;
            _autoStartItem.Click += OnAutoStartMenuClick;

            _exitItem = new ToolStripMenuItem("退出");
            _exitItem.Click += delegate { ExitApplication(); };

            _menu.Items.Add(_deviceItem);
            _menu.Items.Add(_statusItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_refreshItem);
            _menu.Items.Add(_detailItem);
            _menu.Items.Add(_autoStartItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_exitItem);
            _menu.Opening += delegate
            {
                _autoStartItem.Checked = AutoStart.IsEnabled();
            };

            _trayIcon = new NotifyIcon();
            _trayIcon.ContextMenuStrip = _menu;
            _trayIcon.Visible = true;
            _trayIcon.MouseClick += OnTrayMouseClick;
            _trayIcon.DoubleClick += delegate { RefreshBattery(); };
            _trayIcon.BalloonTipClicked += delegate { ShowDetail(); };
            SetIcon(TrayIconFactory.CreateIcon(null, false, false, false));
            _trayIcon.Text = "MCHOSE A7 V2 Pro · 正在检测电量";

            _monitor = new BatteryMonitor();
            _monitor.Updated += OnMonitorUpdated;

            _resumeTimer = new System.Windows.Forms.Timer();
            _resumeTimer.Interval = 2500;
            _resumeTimer.Tick += delegate
            {
                _resumeTimer.Stop();
                RefreshBattery();
            };

            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;

            _showThread = new Thread(ShowEventLoop);
            _showThread.IsBackground = true;
            _showThread.Name = "MouseBatteryDisplay.ShowWindow";
            _showThread.Start();

            _autoStartItem.Checked = AutoStart.IsEnabled();
            Logger.Info("程序启动" + (_startedAtLogin ? "（开机自启）" : ""));
        }

        private void ShowEventLoop()
        {
            while (true)
            {
                try
                {
                    if (!_showEvent.WaitOne()) break;
                }
                catch
                {
                    break;
                }
                if (_disposed) break;
                try
                {
                    _dispatcher.BeginInvoke(new MethodInvoker(ShowDetail));
                }
                catch
                {
                    break;
                }
            }
        }

        private void OnTrayMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowDetail();
            }
        }

        private void OnMonitorUpdated(BatteryStatus status)
        {
            if (_disposed) return;
            try
            {
                if (_dispatcher.IsHandleCreated)
                {
                    _dispatcher.BeginInvoke(new Action<BatteryStatus>(ApplyStatus), status);
                }
            }
            catch
            {
            }
        }

        private void ApplyStatus(BatteryStatus status)
        {
            if (_disposed) return;

            try
            {
                SetIcon(TrayIconFactory.CreateIcon(
                    status.Percent,
                    status.Charging,
                    status.Connected,
                    status.Stale));

                string tooltip = status.TooltipText;
                if (tooltip.Length > 63) tooltip = tooltip.Substring(0, 63);
                _trayIcon.Text = tooltip;
                _deviceItem.Text = status.DeviceName;
                _statusItem.Text = status.Percent.HasValue
                    ? "电量 " + status.Percent.Value + "% · " + status.ModeText
                    : status.ModeText;

                if (_detailForm != null && !_detailForm.IsDisposed)
                {
                    _detailForm.SetStatus(status);
                }

                if (!_firstUpdateShown && status.Connected && status.Percent.HasValue)
                {
                    _firstUpdateShown = true;
                    if (!_startedAtLogin)
                    {
                        _trayIcon.ShowBalloonTip(
                            3500,
                            "鼠标电量",
                            status.Percent.Value + "% · " + (status.Charging ? "充电中" : "使用中"),
                            ToolTipIcon.Info);
                    }
                }

                if (status.Connected && status.Percent.HasValue && !status.Charging
                    && status.Percent.Value <= 20
                    && (DateTime.Now - _lastLowBatteryAlert).TotalHours >= 6)
                {
                    _lastLowBatteryAlert = DateTime.Now;
                    _trayIcon.ShowBalloonTip(
                        6000,
                        "鼠标电量偏低",
                        "当前 " + status.Percent.Value + "%，请及时充电。",
                        ToolTipIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("更新托盘状态失败", ex);
            }
        }

        private void SetIcon(Icon icon)
        {
            Icon previous = _currentIcon;
            _currentIcon = icon;
            _trayIcon.Icon = icon;
            if (previous != null)
            {
                previous.Dispose();
            }
        }

        private void RefreshBattery()
        {
            if (_disposed) return;
            _statusItem.Text = "正在读取电量...";
            _monitor.RefreshAsync(true);
            if (_detailForm != null && !_detailForm.IsDisposed && !_detailForm.Visible)
            {
                _detailForm.SetStatus(_monitor.Current);
            }
        }

        private void ShowDetail()
        {
            if (_disposed) return;
            try
            {
                if (_detailForm != null && !_detailForm.IsDisposed)
                {
                    if (_detailForm.Visible)
                    {
                        _detailForm.Activate();
                        return;
                    }
                    _detailForm.SetStatus(_monitor.Current);
                    _detailForm.PositionNearTaskbar();
                    _detailForm.Show();
                    _detailForm.Activate();
                    return;
                }

                _detailForm = new DetailForm(_monitor, RefreshBattery, ExitApplication);
                _detailForm.FormClosed += delegate { _detailForm = null; };
                _detailForm.SetStatus(_monitor.Current);
                _detailForm.PositionNearTaskbar();
                _detailForm.Show();
                _detailForm.Activate();
            }
            catch (Exception ex)
            {
                Logger.Error("显示详情窗口失败", ex);
            }
        }

        private void OnAutoStartMenuClick(object sender, EventArgs e)
        {
            bool enabled = _autoStartItem.Checked;
            if (!AutoStart.SetEnabled(enabled))
            {
                _autoStartItem.Checked = AutoStart.IsEnabled();
                MessageBox.Show(
                    "无法修改开机自启设置。\n请检查当前用户是否有写入注册表的权限。",
                    "鼠标电量",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume && !_disposed)
            {
                StartResumeTimer();
            }
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock && !_disposed)
            {
                StartResumeTimer();
            }
        }

        private void StartResumeTimer()
        {
            try
            {
                if (_dispatcher != null && _dispatcher.IsHandleCreated)
                {
                    _dispatcher.BeginInvoke(new MethodInvoker(delegate
                    {
                        if (_disposed) return;
                        _resumeTimer.Stop();
                        _resumeTimer.Start();
                    }));
                }
            }
            catch
            {
            }
        }

        private void ExitApplication()
        {
            if (_disposed) return;
            _trayIcon.Visible = false;
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                SystemEvents.SessionSwitch -= OnSessionSwitch;

                try
                {
                    if (_showEvent != null) _showEvent.Set();
                }
                catch
                {
                }

                if (_detailForm != null && !_detailForm.IsDisposed)
                {
                    _detailForm.Close();
                    _detailForm.Dispose();
                    _detailForm = null;
                }

                if (_monitor != null) _monitor.Dispose();
                if (_resumeTimer != null) _resumeTimer.Dispose();
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
                if (_currentIcon != null)
                {
                    _currentIcon.Dispose();
                    _currentIcon = null;
                }
                if (_menu != null) _menu.Dispose();
                if (_dispatcher != null) _dispatcher.Dispose();
                Logger.Info("程序退出");
            }
            base.Dispose(disposing);
        }

        private static bool HasArgument(string argument)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}

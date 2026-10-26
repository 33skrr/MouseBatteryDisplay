using System;
using System.Threading;

namespace MouseBatteryDisplay
{
    internal delegate void BatteryUpdatedHandler(BatteryStatus status);

    internal sealed class BatteryMonitor : IDisposable
    {
        private const int PollIntervalMs = 600000;
        private readonly object _sync = new object();
        private readonly Timer _timer;
        private BatteryStatus _current;
        private int _busy;
        private int _failureCount;
        private bool _disposed;
        private bool _firstSuccessLogged;
        private DateTime _lastSuccessUtc = DateTime.MinValue;

        internal event BatteryUpdatedHandler Updated;

        internal BatteryMonitor()
        {
            _current = new BatteryStatus();
            _current.Error = "正在检测鼠标...";
            _timer = new Timer(OnTimer, null, 1500, PollIntervalMs);
        }

        internal BatteryStatus Current
        {
            get
            {
                lock (_sync)
                {
                    return _current.Clone();
                }
            }
        }

        internal bool IsRefreshing
        {
            get { return Interlocked.CompareExchange(ref _busy, 0, 0) != 0; }
        }

        internal void RefreshAsync(bool allowFallback)
        {
            if (_disposed) return;
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;
            ThreadPool.QueueUserWorkItem(new WaitCallback(DoRefresh), allowFallback);
        }

        private void OnTimer(object state)
        {
            bool allowFallback;
            lock (_sync)
            {
                allowFallback = !_current.Percent.HasValue;
            }
            if (!allowFallback)
            {
                allowFallback = (DateTime.UtcNow - _lastSuccessUtc).TotalMinutes >= 30;
            }
            RefreshAsync(allowFallback);
        }

        private void DoRefresh(object state)
        {
            bool allowFallback = state is bool && (bool)state;
            BatteryStatus status;
            try
            {
                BatteryReading reading = MchoseBatteryReader.Read(allowFallback);
                status = new BatteryStatus();
                status.Percent = reading.Percent;
                status.Charging = reading.Charging;
                status.Connected = true;
                status.Stale = false;
                status.DeviceName = string.IsNullOrEmpty(reading.DeviceName) ? "MCHOSE A7 V2 Pro" : reading.DeviceName;
                status.PollingRate = reading.PollingRate;
                status.LastUpdated = DateTime.Now;
                _failureCount = 0;
                _lastSuccessUtc = DateTime.UtcNow;

                if (!_firstSuccessLogged)
                {
                    _firstSuccessLogged = true;
                    Logger.Info("读取成功: " + status.DeviceName + " " + status.Percent + "%"
                        + (status.Charging ? " 充电中" : "") + " " + status.PollingRate + "Hz");
                }
            }
            catch (Exception ex)
            {
                _failureCount++;
                BatteryStatus previous;
                lock (_sync)
                {
                    previous = _current.Clone();
                }

                status = previous.Clone();
                status.Error = ex.Message;
                if (allowFallback && (_failureCount >= 2 || !previous.Percent.HasValue))
                {
                    status.Connected = false;
                    status.Charging = false;
                    status.Stale = previous.Percent.HasValue;
                }
                else if (!allowFallback && _failureCount >= 3)
                {
                    status.Connected = false;
                    status.Charging = false;
                    status.Stale = previous.Percent.HasValue;
                }
                if (_failureCount == 1 || _failureCount % 10 == 0)
                {
                    Logger.Warn("读取电量失败(" + _failureCount + "): " + ex.Message);
                }
            }

            lock (_sync)
            {
                if (_disposed) return;
                _current = status;
            }

            Interlocked.Exchange(ref _busy, 0);
            BatteryUpdatedHandler handler = Updated;
            if (handler != null)
            {
                try
                {
                    handler(status);
                }
                catch (Exception ex)
                {
                    Logger.Error("刷新界面失败", ex);
                }
            }
        }

        public void Dispose()
        {
            _disposed = true;
            if (_timer != null)
            {
                _timer.Dispose();
            }
        }
    }
}

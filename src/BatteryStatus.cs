using System;

namespace MouseBatteryDisplay
{
    internal sealed class BatteryStatus
    {
        internal int? Percent;
        internal bool Charging;
        internal bool Connected;
        internal bool Stale;
        internal string DeviceName;
        internal DateTime? LastUpdated;
        internal string Error;
        internal int PollingRate;

        internal BatteryStatus()
        {
            DeviceName = "MCHOSE A7 V2 Pro";
        }

        internal BatteryStatus Clone()
        {
            BatteryStatus copy = new BatteryStatus();
            copy.Percent = Percent;
            copy.Charging = Charging;
            copy.Connected = Connected;
            copy.Stale = Stale;
            copy.DeviceName = DeviceName;
            copy.LastUpdated = LastUpdated;
            copy.Error = Error;
            copy.PollingRate = PollingRate;
            return copy;
        }

        internal string ModeText
        {
            get
            {
                if (Connected && Charging) return "充电中";
                if (Connected) return "使用中";
                if (Stale && Percent.HasValue) return "未连接 · 显示上次电量";
                return "未检测到鼠标";
            }
        }

        internal string ShortText
        {
            get
            {
                if (Percent.HasValue)
                {
                    if (Connected) return Percent.Value + "% · " + (Charging ? "充电中" : "使用中");
                    return Percent.Value + "% · 上次";
                }
                return Connected ? "等待电量数据" : "未连接";
            }
        }

        internal string TooltipText
        {
            get
            {
                if (!Percent.HasValue)
                {
                    return "MCHOSE A7 V2 Pro · 正在检测电量";
                }
                if (Connected)
                {
                    return "MCHOSE A7 V2 Pro · " + Percent.Value + "% · " + (Charging ? "充电中" : "使用中");
                }
                return "MCHOSE A7 V2 Pro · 未连接 · 上次 " + Percent.Value + "%";
            }
        }

        internal string UpdatedText
        {
            get
            {
                if (!LastUpdated.HasValue) return "尚未读取到数据";
                TimeSpan age = DateTime.Now - LastUpdated.Value;
                string time = LastUpdated.Value.ToString("HH:mm:ss");
                if (age.TotalDays >= 1)
                {
                    return "上次更新 " + LastUpdated.Value.ToString("MM-dd HH:mm") + "（" + ((int)age.TotalDays) + " 天前）";
                }
                if (age.TotalHours >= 1)
                {
                    return "上次更新 " + time + "（" + ((int)age.TotalHours) + " 小时前）";
                }
                if (age.TotalMinutes >= 1)
                {
                    return "上次更新 " + time + "（" + ((int)age.TotalMinutes) + " 分钟前）";
                }
                return "刚刚更新 " + time;
            }
        }
    }
}

using System;
using System.Reflection;
using Microsoft.Win32;

namespace MouseBatteryDisplay
{
    internal static class AutoStart
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "MouseBatteryDisplay";

        internal static string ExecutablePath
        {
            get
            {
                try
                {
                    return Assembly.GetExecutingAssembly().Location;
                }
                catch
                {
                    return System.Windows.Forms.Application.ExecutablePath;
                }
            }
        }

        internal static string Command
        {
            get { return "\"" + ExecutablePath + "\" --startup"; }
        }

        internal static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    object value = key.GetValue(ValueName);
                    if (value == null) return false;
                    return string.Equals(value.ToString(), Command, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("读取开机自启设置失败: " + ex.Message);
                return false;
            }
        }

        internal static bool SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null)
                    {
                        using (RegistryKey created = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                        {
                            if (created == null) return false;
                            if (enabled) created.SetValue(ValueName, Command, RegistryValueKind.String);
                            else created.DeleteValue(ValueName, false);
                        }
                    }
                    else
                    {
                        if (enabled) key.SetValue(ValueName, Command, RegistryValueKind.String);
                        else key.DeleteValue(ValueName, false);
                    }
                }
                Logger.Info("开机自启已" + (enabled ? "开启" : "关闭"));
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("设置开机自启失败", ex);
                return false;
            }
        }
    }
}

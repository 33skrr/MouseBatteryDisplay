using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace MouseBatteryDisplay
{
    internal sealed class BatteryReading
    {
        internal int Percent;
        internal bool Charging;
        internal string DeviceName;
        internal int PollingRate;
    }

    internal sealed class DeviceUnavailableException : Exception
    {
        internal DeviceUnavailableException(string message) : base(message)
        {
        }
    }

    internal static class MchoseBatteryReader
    {
        private const ushort VendorId = 0x3837;
        private const ushort ProductId = 0x100A;
        private const ushort ConfigUsagePage = 0xFF01;
        private const byte CommandReportId = 0x11;
        private const byte InputReportId = 0x13;

        internal static BatteryReading Read(bool allowFallback)
        {
            List<string> paths = EnumerateHidPaths();
            bool sawMatchingPath = false;
            bool sawConfigInterface = false;
            int lastError = 0;
            string lastProduct = null;

            foreach (string path in paths)
            {
                if (path.IndexOf("vid_3837&pid_100a", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                sawMatchingPath = true;

                using (SafeFileHandle handle = NativeMethods.CreateFile(
                    path,
                    NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
                    NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    NativeMethods.OPEN_EXISTING,
                    NativeMethods.FILE_FLAG_OVERLAPPED,
                    IntPtr.Zero))
                {
                    if (handle.IsInvalid)
                    {
                        lastError = Marshal.GetLastWin32Error();
                        continue;
                    }

                    NativeMethods.HIDP_CAPS caps;
                    if (!TryGetCaps(handle, out caps))
                    {
                        continue;
                    }
                    if (caps.UsagePage != ConfigUsagePage)
                    {
                        continue;
                    }

                    sawConfigInterface = true;
                    lastProduct = ReadProduct(handle);
                    BatteryReading reading = QueryBattery(handle, caps, allowFallback);
                    if (reading != null)
                    {
                        if (string.IsNullOrEmpty(reading.DeviceName))
                        {
                            reading.DeviceName = string.IsNullOrEmpty(lastProduct)
                                ? "MCHOSE A7 V2 Pro"
                                : lastProduct;
                        }
                        return reading;
                    }
                }
            }

            if (!sawMatchingPath)
            {
                throw new DeviceUnavailableException("未检测到 MCHOSE A7 V2 Pro 接收器");
            }
            if (lastError != 0 && lastError != 5)
            {
                throw new DeviceUnavailableException("无法打开鼠标配置通道（错误 " + lastError + "）");
            }
            if (sawConfigInterface && !allowFallback)
            {
                throw new DeviceUnavailableException("鼠标休眠中，保留上次电量");
            }
            throw new DeviceUnavailableException("鼠标未返回电量数据（可能正在休眠）");
        }

        private static BatteryReading QueryBattery(SafeFileHandle handle, NativeMethods.HIDP_CAPS caps, bool allowFallback)
        {
            if (caps.FeatureReportByteLength <= 1 || caps.InputReportByteLength <= 1)
            {
                return null;
            }

            // 0x06 = macro/device info read. It is a read-only command and makes the
            // A7 V2 Pro push its current status (battery, charging, polling, name).
            SendCommand(handle, caps.FeatureReportByteLength, new byte[] { 0x06, 0x00 });
            BatteryReading reading = WaitForStatus(handle, caps.InputReportByteLength, 1200);
            if (reading != null)
            {
                return reading;
            }

            // The first query after wake-up can be lost while the receiver is still
            // waking; retry the same read-only query once before using the fallback.
            SendCommand(handle, caps.FeatureReportByteLength, new byte[] { 0x06, 0x00 });
            reading = WaitForStatus(handle, caps.InputReportByteLength, 1200);
            if (reading != null)
            {
                return reading;
            }

            if (!allowFallback)
            {
                return null;
            }

            // A small fallback for firmware that does not answer the info query while
            // the mouse is idle. 0x0B 0xAA asks the mouse to reload its saved profile,
            // which also causes a status push. It is only used for the initial read,
            // a manual refresh, or a scheduled recovery attempt, so the mouse is not
            // woken every few minutes.
            Logger.Warn("状态查询超时，使用兼容模式重试");
            SendCommand(handle, caps.FeatureReportByteLength, new byte[] { 0x0B, 0xAA });
            return WaitForStatus(handle, caps.InputReportByteLength, 2200);
        }

        private static void SendCommand(SafeFileHandle handle, int featureLength, byte[] command)
        {
            byte[] report = new byte[featureLength];
            report[0] = CommandReportId;

            // The vendor protocol XORs every payload byte with 0xFF. Padding is 0xFF,
            // which becomes 0x00 after encoding.
            for (int i = 1; i < report.Length; i++)
            {
                report[i] = 0xFF;
            }
            for (int i = 0; i < command.Length && i + 1 < report.Length; i++)
            {
                report[i + 1] = (byte)(command[i] ^ 0xFF);
            }

            if (!NativeMethods.HidD_SetFeature(handle, report, report.Length))
            {
                int error = Marshal.GetLastWin32Error();
                Logger.Warn("HID SetFeature 失败，错误 " + error);
            }
            Thread.Sleep(30);
        }

        private static BatteryReading WaitForStatus(SafeFileHandle handle, int inputLength, int timeoutMs)
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                int remaining = timeoutMs - (int)watch.ElapsedMilliseconds;
                if (remaining <= 0) break;
                byte[] report = ReadInputReport(handle, inputLength, Math.Min(remaining, 700));
                if (report == null || report.Length < 2 || report[0] != InputReportId)
                {
                    continue;
                }

                byte[] decoded = new byte[report.Length - 1];
                for (int i = 1; i < report.Length; i++)
                {
                    decoded[i - 1] = (byte)(report[i] ^ 0xFF);
                }

                if (decoded.Length >= 17 && decoded[0] == 0xE2)
                {
                    BatteryReading reading = ParseStatus(decoded);
                    if (reading != null)
                    {
                        return reading;
                    }
                }
            }
            return null;
        }

        private static BatteryReading ParseStatus(byte[] data)
        {
            int battery = data[4];
            if (battery < 1 || battery > 100)
            {
                return null;
            }

            string name = ExtractName(data, 9, 12);
            if (string.IsNullOrEmpty(name))
            {
                // Garbled wake-up packets have an empty name. Keep waiting for a valid one.
                return null;
            }

            BatteryReading reading = new BatteryReading();
            reading.Percent = battery;
            reading.Charging = data[3] != 0;
            reading.DeviceName = name;
            int pollingIndex = data[6];
            int[] rates = new int[] { 125, 500, 1000, 2000, 4000, 8000 };
            reading.PollingRate = (pollingIndex >= 0 && pollingIndex < rates.Length) ? rates[pollingIndex] : 0;
            return reading;
        }

        private static string ExtractName(byte[] data, int offset, int maxLength)
        {
            StringBuilder sb = new StringBuilder();
            int end = Math.Min(data.Length, offset + maxLength);
            for (int i = offset; i < end; i++)
            {
                byte b = data[i];
                if (b == 0) break;
                if (b >= 0x20 && b <= 0x7E) sb.Append((char)b);
            }
            return sb.ToString().Trim();
        }

        private static byte[] ReadInputReport(SafeFileHandle handle, int length, int timeoutMs)
        {
            IntPtr ev = NativeMethods.CreateEvent(IntPtr.Zero, true, false, null);
            if (ev == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                NativeMethods.OVERLAPPED overlapped = new NativeMethods.OVERLAPPED();
                overlapped.hEvent = ev;
                byte[] buffer = new byte[length];
                int read;

                bool completed = NativeMethods.ReadFile(handle, buffer, buffer.Length, out read, ref overlapped);
                if (!completed)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error != NativeMethods.ERROR_IO_PENDING)
                    {
                        Logger.Warn("HID ReadFile 失败，错误 " + error);
                        return null;
                    }

                    uint wait = NativeMethods.WaitForSingleObject(ev, (uint)timeoutMs);
                    if (wait != NativeMethods.WAIT_OBJECT_0)
                    {
                        NativeMethods.CancelIoEx(handle, IntPtr.Zero);
                        NativeMethods.WaitForSingleObject(ev, 150);
                        return null;
                    }

                    int transferred;
                    if (!NativeMethods.GetOverlappedResult(handle, ref overlapped, out transferred, false) || transferred <= 0)
                    {
                        return null;
                    }
                    read = transferred;
                }

                if (read <= 0) return null;
                if (read == buffer.Length) return buffer;
                byte[] trimmed = new byte[read];
                Array.Copy(buffer, trimmed, read);
                return trimmed;
            }
            finally
            {
                NativeMethods.CloseHandle(ev);
            }
        }

        private static bool TryGetCaps(SafeFileHandle handle, out NativeMethods.HIDP_CAPS caps)
        {
            caps = new NativeMethods.HIDP_CAPS();
            caps.Reserved = new ushort[17];
            IntPtr preparsed;
            if (!NativeMethods.HidD_GetPreparsedData(handle, out preparsed))
            {
                return false;
            }
            try
            {
                return NativeMethods.HidP_GetCaps(preparsed, ref caps) == 0x00110000;
            }
            finally
            {
                NativeMethods.HidD_FreePreparsedData(preparsed);
            }
        }

        private static string ReadProduct(SafeFileHandle handle)
        {
            StringBuilder sb = new StringBuilder(256);
            if (NativeMethods.HidD_GetProductString(handle, sb, sb.Capacity))
            {
                return sb.ToString();
            }
            return "";
        }

        private static List<string> EnumerateHidPaths()
        {
            List<string> result = new List<string>();
            Guid guid = NativeMethods.HidDeviceInterfaceGuid;
            IntPtr set = NativeMethods.SetupDiGetClassDevs(
                ref guid,
                IntPtr.Zero,
                IntPtr.Zero,
                NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE);
            if (set == new IntPtr(-1))
            {
                return result;
            }

            try
            {
                for (int index = 0; ; index++)
                {
                    NativeMethods.SP_DEVICE_INTERFACE_DATA did = new NativeMethods.SP_DEVICE_INTERFACE_DATA();
                    did.cbSize = Marshal.SizeOf(typeof(NativeMethods.SP_DEVICE_INTERFACE_DATA));
                    if (!NativeMethods.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, index, ref did))
                    {
                        break;
                    }

                    int required;
                    NativeMethods.SetupDiGetDeviceInterfaceDetail(set, ref did, IntPtr.Zero, 0, out required, IntPtr.Zero);
                    if (required <= 0)
                    {
                        continue;
                    }

                    IntPtr detail = Marshal.AllocHGlobal(required);
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        int required2;
                        if (NativeMethods.SetupDiGetDeviceInterfaceDetail(set, ref did, detail, required, out required2, IntPtr.Zero))
                        {
                            string path = Marshal.PtrToStringUni(new IntPtr(detail.ToInt64() + 4));
                            if (!string.IsNullOrEmpty(path))
                            {
                                result.Add(path);
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detail);
                    }
                }
            }
            finally
            {
                NativeMethods.SetupDiDestroyDeviceInfoList(set);
            }

            return result;
        }
    }
}

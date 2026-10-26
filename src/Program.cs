using System;
using System.Threading;
using System.Windows.Forms;

namespace MouseBatteryDisplay
{
    internal static class Program
    {
        private const string MutexName = "MouseBatteryDisplay.MCHOSE.A7V2Pro";
        private const string ShowEventName = "MouseBatteryDisplay.ShowWindow";

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            Mutex mutex = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                SignalExistingInstance();
                return;
            }

            EventWaitHandle showEvent = null;
            TrayAppContext context = null;
            try
            {
                EnableDpiAwareness();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += OnThreadException;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
                context = new TrayAppContext(showEvent);
                Application.ApplicationExit += delegate
                {
                    try
                    {
                        context.Dispose();
                    }
                    catch
                    {
                    }
                };
                Application.Run(context);
            }
            catch (Exception ex)
            {
                Logger.Error("程序启动失败", ex);
                MessageBox.Show(
                    "鼠标电量程序启动失败。\n\n" + ex.Message,
                    "鼠标电量",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (context != null)
                {
                    try
                    {
                        context.Dispose();
                    }
                    catch
                    {
                    }
                }
                if (showEvent != null)
                {
                    showEvent.Close();
                }
                try
                {
                    mutex.ReleaseMutex();
                }
                catch
                {
                }
                mutex.Close();
            }
        }

        private static void EnableDpiAwareness()
        {
            try
            {
                if (NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
                {
                    return;
                }
            }
            catch
            {
            }

            try
            {
                NativeMethods.SetProcessDPIAware();
            }
            catch
            {
            }
        }

        private static void SignalExistingInstance()
        {
            try
            {
                using (EventWaitHandle handle = EventWaitHandle.OpenExisting(ShowEventName))
                {
                    handle.Set();
                }
            }
            catch
            {
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Logger.Error("未处理的界面异常", e.Exception);
            MessageBox.Show(
                "鼠标电量程序遇到一个错误，但仍会继续运行。\n\n" + e.Exception.Message,
                "鼠标电量",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception exception = e.ExceptionObject as Exception;
            Logger.Error("未处理的程序异常", exception);
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TwentyTwentyTwenty.Properties;

namespace TwentyTwentyTwenty
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MyCustomApplicationContext());
        }
    }

    public class MyCustomApplicationContext : ApplicationContext
    {
        private static readonly TimeSpan HideTime = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan ShowTime = TimeSpan.FromSeconds(20);

        private readonly NotifyIcon trayIcon;
        private readonly Form1 form = new Form1();

        public MyCustomApplicationContext()
        {
            trayIcon = new NotifyIcon
            {
                Icon = Resources.AppIcon,
                ContextMenu = new ContextMenu(new[] { new MenuItem("Exit", Exit) }),
                Visible = true
            };

            var uiThreadSyncContext = SynchronizationContext.Current;

            Task.Run(() =>
            {
                while (true)
                {
                    Thread.Sleep(HideTime);
                    uiThreadSyncContext.Post(state => form.Visible = true, null);
                    Thread.Sleep(ShowTime);
                    uiThreadSyncContext.Post(state => form.Visible = false, null);
                }
                // ReSharper disable once FunctionNeverReturns
            });
        }

        private void Exit(object sender, EventArgs e)
        {
            // Hide tray icon, otherwise it will remain shown until user mouses over it
            trayIcon.Visible = false;

            Application.Exit();
        }
    }
}
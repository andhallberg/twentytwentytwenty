using Microsoft.Win32;
using System;
using System.IO;
using System.Media;
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
        private const string SoundPath = @"C:\Windows\Media\Windows Proximity Notification.wav";
        private static readonly TimeSpan HideTime = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan ShowTime = TimeSpan.FromSeconds(20);

        private readonly NotifyIcon trayIcon;
        private readonly Form1 form = new Form1();
        private bool _isScreenUnlocked = true;

        public MyCustomApplicationContext()
        {
            trayIcon = new NotifyIcon
            {
                Icon = Resources.AppIcon,
                Text = "Twenty x 3",
                ContextMenu = new ContextMenu(new[] { new MenuItem("Exit", Exit) }),
                Visible = true
            };

            SoundPlayer soundPlayer = InitAudioPlayer();
            InitLockScreenAwareness(soundPlayer);

            var uiThreadSyncContext = SynchronizationContext.Current;

            Task.Run(() =>
            {
                while (true)
                {
                    Thread.Sleep(HideTime);
                    uiThreadSyncContext.Post(state => form.Visible = true, null);
                    Thread.Sleep(ShowTime);
                    uiThreadSyncContext.Post(state => form.Visible = false, null);

                    PlayAudio(soundPlayer);
                }
                // ReSharper disable once FunctionNeverReturns
            });
        }

        SoundPlayer InitAudioPlayer()
        {
            var soundPlayer = new SoundPlayer(SoundPath);
            try
            {
                soundPlayer.Load();
            }
            catch (FileNotFoundException)
            {
                HandleError($"audio file not found");
            }
            catch (TimeoutException)
            {
                HandleError($"timeout reading audio file");
            }
            return soundPlayer;

            void HandleError(string error)
            {
                soundPlayer = null;
                trayIcon.Text += $"\n{error}";
            }
        }

        private void InitLockScreenAwareness(SoundPlayer soundPlayer)
        {
            if (soundPlayer == null)
            {
                // only listen about lock screen if we're playing audio
                return;
            }
            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        }

        private void PlayAudio(SoundPlayer soundPlayer)
        {
            if (!_isScreenUnlocked)
            {
                return;
            }

            soundPlayer?.Play();
        }

        private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            // deliberately set to false on all other states
            _isScreenUnlocked = e.Reason == SessionSwitchReason.SessionUnlock;
        }

        private void Exit(object sender, EventArgs e)
        {
            // Hide tray icon, otherwise it will remain shown until user mouses over it
            trayIcon.Visible = false;

            Application.Exit();
        }
    }
}
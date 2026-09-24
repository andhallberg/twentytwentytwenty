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
            ApplicationConfiguration.Initialize();
            Application.Run(new MyCustomApplicationContext());
        }
    }

    public class MyCustomApplicationContext : ApplicationContext
    {
        private const string SoundPath = @"C:\Windows\Media\Windows Proximity Notification.wav";
        private static readonly TimeSpan HideTime = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan ShowTime = TimeSpan.FromSeconds(20);

        // 0 means not suspended, long.MaxValue means suspended indefinitely
        private const long NotSuspended = 0;
        private const long SuspendedIndefinitely = long.MaxValue;

        private readonly NotifyIcon trayIcon;
        private readonly Form1 form = new Form1();
        private readonly SoundPlayer soundPlayer;
        private readonly SynchronizationContext uiThreadSyncContext;
        private bool _isScreenUnlocked = true;

        // UTC ticks at which the suspension ends, written from the UI thread and read from the timer thread
        private long _suspendedUntilTicks = NotSuspended;

        // 1 while a reminder is on screen, so a manual one cannot stack on top of a scheduled one
        private int _isShowingReminder;

        private ToolStripMenuItem unsuspendMenuItem;

        public MyCustomApplicationContext()
        {
            trayIcon = new NotifyIcon
            {
                Icon = Resources.AppIcon,
                Text = "Twenty x 3",
                ContextMenuStrip = CreateTrayMenu(),
                Visible = true
            };

            soundPlayer = InitAudioPlayer();
            InitLockScreenAwareness(soundPlayer);

            uiThreadSyncContext = SynchronizationContext.Current;

            Task.Run(() =>
            {
                while (true)
                {
                    Thread.Sleep(HideTime);

                    if (IsSuspended())
                    {
                        continue;
                    }

                    ShowReminder();
                }
                // ReSharper disable once FunctionNeverReturns
            });
        }

        private ContextMenuStrip CreateTrayMenu()
        {
            var suspendMenuItem = new ToolStripMenuItem("Suspend for...");
            suspendMenuItem.DropDownItems.Add(CreateSuspendPreset("20 minutes", TimeSpan.FromMinutes(20)));
            suspendMenuItem.DropDownItems.Add(CreateSuspendPreset("30 minutes", TimeSpan.FromMinutes(30)));
            suspendMenuItem.DropDownItems.Add(CreateSuspendPreset("1 hour", TimeSpan.FromHours(1)));
            suspendMenuItem.DropDownItems.Add(new ToolStripMenuItem("Indefinitely", null, (_, _) => Suspend(null)));

            unsuspendMenuItem = new ToolStripMenuItem("Unsuspend", null, (_, _) => Unsuspend())
            {
                Enabled = false
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem("Show now", null, (_, _) => Task.Run(ShowReminder)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(suspendMenuItem);
            menu.Items.Add(unsuspendMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Exit", null, Exit));

            // the suspension can expire on its own, so refresh the state every time the menu is shown
            menu.Opening += (_, _) => unsuspendMenuItem.Enabled = IsSuspended();

            return menu;

            ToolStripMenuItem CreateSuspendPreset(string text, TimeSpan duration) =>
                new ToolStripMenuItem(text, null, (_, _) => Suspend(duration));
        }

        /// <summary>
        /// Shows the reminder for <see cref="ShowTime"/> and then plays the sound.
        /// Must not be called on the UI thread, it blocks while the reminder is up.
        /// </summary>
        private void ShowReminder()
        {
            if (Interlocked.Exchange(ref _isShowingReminder, 1) == 1)
            {
                // already on screen, nothing to do
                return;
            }

            try
            {
                uiThreadSyncContext.Post(state => form.Visible = true, null);
                Thread.Sleep(ShowTime);
                uiThreadSyncContext.Post(state => form.Visible = false, null);
            }
            finally
            {
                Interlocked.Exchange(ref _isShowingReminder, 0);
            }

            PlayAudio();
        }

        /// <param name="duration">null suspends indefinitely</param>
        private void Suspend(TimeSpan? duration)
        {
            var until = duration.HasValue
                ? (DateTime.UtcNow + duration.Value).Ticks
                : SuspendedIndefinitely;

            Interlocked.Exchange(ref _suspendedUntilTicks, until);
            unsuspendMenuItem.Enabled = true;
        }

        private void Unsuspend()
        {
            Interlocked.Exchange(ref _suspendedUntilTicks, NotSuspended);
            unsuspendMenuItem.Enabled = false;
        }

        private bool IsSuspended()
        {
            var until = Interlocked.Read(ref _suspendedUntilTicks);
            return until == SuspendedIndefinitely || until > DateTime.UtcNow.Ticks;
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

        private void PlayAudio()
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

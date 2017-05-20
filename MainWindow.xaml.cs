using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;

using TCD.System.ApplicationExtensions;
using TCD.System.TouchInjection;
using TCD.System.TUIO;

namespace WatsonTouch
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Backend Variables
        TuioChannel channel;
        private uint maxTouchPoints = 24; // 256
        private int areaRadius = 10;
        private const int pressure = 32000;
        Rectangle targetArea;
        #endregion

        public const string APP_NAME = "Thoth Touch";
        public const string APP_DESCRIPTION = "Monitoring TUIO data coming through radar control and generate touch events.";
        System.Windows.Forms.NotifyIcon notifyIcon;

        public MainWindow()
        {
            InitializeComponent();
            ConnectTUIO();
            AutoStartCheck();
        }

        private void AutoStartCheck()
        {
            var AutoStart = ApplicationAutostart.IsAutostartAsync(APP_NAME);
            var isAutoStart = AutoStart.Result;
            AutoStartCheckBox.IsChecked = isAutoStart;
            SetAutoStart(isAutoStart);
            this.WindowState = WindowState.Minimized;
            this.ResizeMode = ResizeMode.CanMinimize;
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Visible = true;
            notifyIcon.Icon = new Icon("favicon.ico");
            notifyIcon.ShowBalloonTip(100, APP_NAME, APP_DESCRIPTION, System.Windows.Forms.ToolTipIcon.Info);
            notifyIcon.MouseDoubleClick += NotifyIcon_MouseDoubleClick;
            System.Windows.Forms.MenuItem[] menuItems = new System.Windows.Forms.MenuItem[] {
            new System.Windows.Forms.MenuItem("Open", (sender, args) =>
            {
                ShowApplication();
            }),
            new System.Windows.Forms.MenuItem("Exit", (sender, args) =>
            {
                Close();
            })};

            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(menuItems);
            this.Hide();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if(this.WindowState == WindowState.Minimized)
            {
                this.Hide();
                notifyIcon.Visible = true;
            }
        }

        private void NotifyIcon_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            ShowApplication();
        }

        private void ShowApplication()
        {
            notifyIcon.Visible = false;
            this.WindowState = WindowState.Normal;
            this.Show();
        }

        public void SetAutoStart(bool isAutoStart)
        {
            var x = ApplicationAutostart.SetAutostartAsync(isAutoStart, APP_NAME, APP_DESCRIPTION);
            if (x.Result)
            {
                AutoStartCheckBox.Content = isAutoStart ? "Auto start (Yes)" : "Auto start (No)";
            }
            else
            {
                AutoStartCheckBox.Content = "Auto start (No)";
            }
        }

        private void AutoStartCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            SetAutoStart(true);
        }

        private void AutoStartCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SetAutoStart(false);
        }

        private async void ConnectTUIO()
        {
            channel = new TuioChannel();
            TuioChannel.OnTuioRefresh += TuioChannel_OnTuioRefresh;
            bool touch = await InitTouch();
            bool tuio = await ConnectChannel();
            targetArea = System.Windows.Forms.Screen.AllScreens[0].Bounds;
        }

        private async Task<bool> ConnectChannel()
        {
            await Task.Delay(0);
            return channel.Connect();
        }

        private async Task<bool> InitTouch()
        {
            await Task.Delay(0);
            return TouchInjector.InitializeTouchInjection(maxTouchPoints, TouchFeedback.DEFAULT);
        }

        private void TuioChannel_OnTuioRefresh(TuioTime t)
        {
            //TODO: re-enable frequent screen monitoring
            //if (frameCount % checkScreenEvery == 0)
            //    ScanScreens();
            //loop through the TuioObjects
            List<PointerTouchInfo> toFire = new List<PointerTouchInfo>();
            if (channel.CursorList.Count > 0)
            {
                foreach (var kvp in channel.CursorList)
                {
                    TuioCursor cur = kvp.Value.TuioCursor;
                    IncomingType type = kvp.Value.Type;
                    int[] injectionCoordinates = ToInjectionCoordinates(cur.getX(), cur.getY());

                    //make a new pointertouchinfo with all neccessary information
                    PointerTouchInfo contact = new PointerTouchInfo();
                    contact.PointerInfo.pointerType = PointerInputType.TOUCH;
                    contact.TouchFlags = TouchFlags.NONE;
                    //contact.Orientation = (uint)cur.getAngleDegrees();//this is only valid for TuioObjects
                    contact.Pressure = pressure;
                    contact.TouchMasks = TouchMask.CONTACTAREA | TouchMask.ORIENTATION | TouchMask.PRESSURE;
                    contact.PointerInfo.PtPixelLocation.X = injectionCoordinates[0];
                    contact.PointerInfo.PtPixelLocation.Y = injectionCoordinates[1];
                    contact.PointerInfo.PointerId = SessionIDToTouchID(cur.getSessionID());
                    contact.ContactArea.left = injectionCoordinates[0] - areaRadius;
                    contact.ContactArea.right = injectionCoordinates[0] + areaRadius;
                    contact.ContactArea.top = injectionCoordinates[1] - areaRadius;
                    contact.ContactArea.bottom = injectionCoordinates[1] + areaRadius;
                    //set the right flag
                    if (type == IncomingType.New)
                        contact.PointerInfo.PointerFlags = PointerFlags.DOWN | PointerFlags.INRANGE | PointerFlags.INCONTACT;
                    else if (type == IncomingType.Update)
                        contact.PointerInfo.PointerFlags = PointerFlags.UPDATE | PointerFlags.INRANGE | PointerFlags.INCONTACT;
                    else if (type == IncomingType.Remove)
                        contact.PointerInfo.PointerFlags = PointerFlags.UP;
                    //add it to 'toFire'
                    toFire.Add(contact);
                }
            }

            //fire the events
            bool success = TouchInjector.InjectTouchInput(toFire.Count, toFire.ToArray());

            //remove those with type == IncomingType.Remove
            List<long> removeList = new List<long>();
            foreach (var kvp in channel.CursorList)
                if (kvp.Value.Type == IncomingType.Remove)
                    removeList.Add(kvp.Key);
            foreach (long key in removeList)
                channel.CursorList.Remove(key);//remove from the tuio channel

        }
        private int[] ToInjectionCoordinates(float x, float y)
        {
            int[] result = new int[2];
            result[0] = targetArea.X + (int)Math.Round(x * targetArea.Width);
            result[1] = targetArea.Y + (int)Math.Round(y * targetArea.Height);
            return result;
        }

        private uint SessionIDToTouchID(long sessionID)
        {
            return (uint)(sessionID % maxTouchPoints);
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            areaRadius = (int)e.NewValue;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}

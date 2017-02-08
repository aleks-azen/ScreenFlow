using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using IWshRuntimeLibrary;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using Supremes;
using System.IO;
using System.Timers;
using System.ComponentModel;
using System.Net;

namespace ScreenFlow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public enum timestate
    {
        Night, Day
    }
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern Int32 SystemParametersInfo(
    UInt32 action, UInt32 uParam, String vParam, UInt32 winIni);
        private static readonly UInt32 SPI_SETDESKWALLPAPER = 0x14;
        private static readonly UInt32 SPIF_UPDATEINIFILE = 0x01;
        private static readonly UInt32 SPIF_SENDWININICHANGE = 0x02;
        private static readonly string sunPrefix = "http://www.timeanddate.com/sun/usa/";
        public string sunSufix = "New-York";
        public System.Timers.Timer webReloadTimer = new System.Timers.Timer();
        public System.Timers.Timer wallpaperUpdateTimer = new System.Timers.Timer();
        public NotifyIcon ni = new NotifyIcon();
        public System.Windows.Forms.ContextMenuStrip iconMenu;
        public DateTime updateTime = new DateTime(1, 1, 1);
        public string streamData = "sssssss,";

        public MainWindow()
        {
            //Prevents multiple versions from running
            if (System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > 1)
            {
                System.Windows.MessageBox.Show("Application already Running - Look in your system tray");
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            }

            //initializations
            UpdateWallpaper();
            //StateMapper(Properties.Settings.Default.zipCode);
            wallpaperUpdateTimer.Elapsed += new ElapsedEventHandler(WallpaperTimerElapsed);
            wallpaperUpdateTimer.AutoReset = false;
            wallpaperUpdateTimer.Interval = IntervalUntilNextChange().TotalMilliseconds;
            wallpaperUpdateTimer.Start();

            webReloadTimer.Elapsed += new ElapsedEventHandler(WebTimerElapsed);
            webReloadTimer.AutoReset = false;
            webReloadTimer.Interval = TimeSpan.FromSeconds(5).TotalMilliseconds;
            webReloadTimer.Start();
            InitializeComponent();

        //Rechecks appstatus on wake from sleep because update timers are thrown off sometimes
        Microsoft.Win32.SystemEvents.PowerModeChanged += delegate (object s, Microsoft.Win32.PowerModeChangedEventArgs e)
            {
                if (e.Mode == Microsoft.Win32.PowerModes.Resume)
                {
                    UpdateWallpaper();
                    wallpaperUpdateTimer.Interval = IntervalUntilNextChange().TotalMilliseconds;
                    DateTime wakeTime = System.DateTime.Now;
                    TimeSpan timePassed = wakeTime.Subtract(updateTime);
                    if (webReloadTimer.Interval > TimeSpan.FromMinutes(5).TotalMilliseconds)
                    {
                        if (webReloadTimer.Interval - timePassed.TotalMilliseconds > 0)
                            webReloadTimer.Interval = webReloadTimer.Interval - timePassed.TotalMilliseconds;
                        else webReloadTimer.Interval = 1;
                    }
                }
            };

            Microsoft.Win32.SystemEvents.SessionSwitch += delegate (object sender, Microsoft.Win32.SessionSwitchEventArgs e)
             {
                 if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionUnlock)
                 {
                     UpdateWallpaper();
                     wallpaperUpdateTimer.Interval = IntervalUntilNextChange().TotalMilliseconds;
                     DateTime wakeTime = System.DateTime.Now;
                     TimeSpan timePassed = wakeTime.Subtract(updateTime);
                     if (webReloadTimer.Interval > TimeSpan.FromMinutes(5).TotalMilliseconds)
                     {
                         if (webReloadTimer.Interval - timePassed.TotalMilliseconds > 0)
                             webReloadTimer.Interval = webReloadTimer.Interval - timePassed.TotalMilliseconds;
                         else webReloadTimer.Interval = 1;
                     }
                 }
             };


            //set initial values
            StartupBox.IsChecked = Properties.Settings.Default.runAtStart;
            Image1Box.Text = Properties.Settings.Default.dayImage;
            Image2Box.Text = Properties.Settings.Default.nightImage;

            iconMenu = new System.Windows.Forms.ContextMenuStrip();
            System.Windows.Forms.ToolStripMenuItem showOption = new System.Windows.Forms.ToolStripMenuItem();
            showOption.Text = "Show";
            showOption.Click += new EventHandler(ShowApp);
            iconMenu.Items.Add(showOption);

            System.Windows.Forms.ToolStripMenuItem exitOption = new System.Windows.Forms.ToolStripMenuItem();
            exitOption.Text = "Exit";
            exitOption.Click += delegate (object sender, EventArgs args)
            {
                ni.Visible = false;
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            };
            iconMenu.Items.Add(exitOption);

            //System tray settings
            ni.Icon = Properties.Resources._1466070757_My_Computer;
            ni.ContextMenuStrip = iconMenu;
            ni.DoubleClick += new EventHandler(ShowApp);

            //Opens settings menu on startup if any settings are invalid, else minimizes app to system tray
            if (System.IO.File.Exists(Properties.Settings.Default.nightImage) && System.IO.File.Exists(Properties.Settings.Default.dayImage))
            {
                ni.Visible = true;
                this.Hide();
            }
            else
            {
                ni.Visible = false;
            }
        }


        //sets behavior for minimizing to system tray
        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == System.Windows.WindowState.Minimized)
            {
                this.Hide();
                ni.Visible = true;
            }

            base.OnStateChanged(e);
        }
        #region helpers
        
        public Boolean UpdateWallpaper()
        {
            if (CheckTime() == timestate.Night)
            {
                if (System.IO.File.Exists(Properties.Settings.Default.nightImage))
                {
                    SetWallpaper(Properties.Settings.Default.nightImage);
                    if (System.IO.File.Exists(Properties.Settings.Default.dayImage))
                        return true;
                }
            }
            else
            {
                if (System.IO.File.Exists(Properties.Settings.Default.dayImage))
                {
                    SetWallpaper(Properties.Settings.Default.dayImage);
                    if (System.IO.File.Exists(Properties.Settings.Default.nightImage))
                        return true;
                }
            }
            return false;
        }

        public static void SetWallpaper(String path)
        {
            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path,
                SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
        }

        //checks whether it is day or night
        public timestate CheckTime()
        {
            if (Properties.Settings.Default.sunrise == Properties.Settings.Default.sunset) return timestate.Day;
            if (DateTime.Now.TimeOfDay.CompareTo(Properties.Settings.Default.sunrise) < 0 || DateTime.Now.TimeOfDay.CompareTo(Properties.Settings.Default.sunset) > 0) return timestate.Night;
            else return timestate.Day;
        }

        //calculates interval until the next time wallpaper should be changed
        public TimeSpan IntervalUntilNextChange()
        {

            if (CheckTime() == timestate.Day)
            {
                return Properties.Settings.Default.sunset - DateTime.Now.TimeOfDay;
            }
            else
            {
                TimeSpan timeNow = DateTime.Now.TimeOfDay;
                if (timeNow > Properties.Settings.Default.sunset)
                {
                    return (new TimeSpan(23, 59, 59) - DateTime.Now.TimeOfDay + Properties.Settings.Default.sunrise);
                }
                else return (Properties.Settings.Default.sunrise - DateTime.Now.TimeOfDay);
            }
        }

        //checks connection
        public static bool CheckForInternetConnection()
        {
            try
            {
                using (var client = new WebClient())
                {
                    using (var stream = client.OpenRead("http://www.google.com"))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        //Run at startup helpers
        private static void CreateShortcut()
        {
            WshShell shell = new WshShell();
            string shortcutAddress = Environment.GetFolderPath(Environment.SpecialFolder.Startup) + @"\ScreenFlow.lnk";
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutAddress);
            shortcut.Description = "Shortcut";
            shortcut.TargetPath = System.Windows.Forms.Application.StartupPath + @"\ScreenFlow.exe";
            shortcut.Save();
        }
        public static void DeleteShortcut()
        {
            if (System.IO.File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Startup) + @"\ScreenFlow.lnk"))
            {
                System.IO.File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Startup) + @"\ScreenFlow.lnk");
            }

        }

        //Parses internet data into a TimeSpan
        //static TimeSpan StringToTimeSpan(String time)
        //{

        //    int hours = int.Parse(time.Substring(0, time.IndexOf(':')));
        //    time = time.Substring(time.IndexOf(':') + 1);
        //    int minutes = int.Parse(time.Substring(0, time.IndexOf(' ')));
        //    if (time.Contains("PM")) hours += 12;
        //    TimeSpan t = new TimeSpan(hours, minutes, 0);
        //    return t;
        //}
        static TimeSpan StringToTimeSpan(String time)
        {

            int hours = int.Parse(time.Substring(0, time.IndexOf(':')));
            time = time.Substring(time.IndexOf(':') + 1);
            int minutes = int.Parse(time.Substring(0, time.IndexOf(':')));
            time = time.Substring(time.IndexOf(':') + 1);
            int seconds = int.Parse(time.Substring(0, time.IndexOf(' ')));
            if (hours == 12) hours -= 12;
            if (time.Contains("PM")) hours += 12;
            
            TimeSpan t = new TimeSpan(hours, minutes, seconds);
            return t;
        }

        public void ShowApp(object sender, EventArgs args)
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            Thread.Sleep(100);
            ni.Visible = false;
        }

        #endregion

        #region timerTrigers

        //updates the wallpaper at sunrise/sunset
        private void WallpaperTimerElapsed(object source, ElapsedEventArgs e)
        {
            try
            {
                UpdateWallpaper();
                wallpaperUpdateTimer.Interval = IntervalUntilNextChange().TotalMilliseconds;
            }
            catch (Exception ex)
            {

            }

        }

        //Updates sunrise/sunset times once a day, attempts to update every 5 minutes if there is no internet connection
        private void WebTimerElapsed(object source, ElapsedEventArgs e)
        {
            if (CheckForInternetConnection())
            {
                try
                {

                    string data;
                    string queryPrefix = String.Format("http://api.sunrise-sunset.org/json?lat={0}&lng={1}&formatted=1", Properties.Settings.Default.lat, Properties.Settings.Default.lng);
                    using (var client = new WebClient())
                    {
                        data = client.DownloadString(queryPrefix);
                    }
                    data = data.Substring(data.IndexOf("sunrise") + "sunrise".Length + 3);
                    string sunriseTime = data.Substring(0, data.IndexOf("\""));
                    data = data.Substring(data.IndexOf("sunset") + "sunset".Length + 3);
                    string sunsetTime = data.Substring(0, data.IndexOf("\""));
                    if (StringToTimeSpan(sunriseTime) + System.TimeZone.CurrentTimeZone.GetUtcOffset(System.DateTime.Now) != Properties.Settings.Default.sunrise)
                    {
                        Properties.Settings.Default.sunrise = StringToTimeSpan(sunriseTime) + System.TimeZone.CurrentTimeZone.GetUtcOffset(System.DateTime.Now);
                        if (Properties.Settings.Default.sunrise < new TimeSpan(0, 0, 0)) Properties.Settings.Default.sunrise = new TimeSpan(24, 0, 0) + Properties.Settings.Default.sunrise;
                        Properties.Settings.Default.sunset = StringToTimeSpan(sunsetTime) + System.TimeZone.CurrentTimeZone.GetUtcOffset(System.DateTime.Now);
                        if (Properties.Settings.Default.sunset < new TimeSpan(0, 0, 0)) Properties.Settings.Default.sunset = new TimeSpan(24, 0, 0) + Properties.Settings.Default.sunset;
                        Properties.Settings.Default.Save();
                        wallpaperUpdateTimer.Interval = IntervalUntilNextChange().TotalMilliseconds;
                        UpdateWallpaper();
                    }
                    updateTime = System.DateTime.Now;
                    webReloadTimer.Interval = TimeSpan.FromDays(1).TotalMilliseconds;

                }
                catch (Exception ex)
                {
                    webReloadTimer.Interval = TimeSpan.FromMinutes(5).TotalMilliseconds;
                }
            }

            else
            {
                webReloadTimer.Interval = TimeSpan.FromMinutes(5).TotalMilliseconds;
            }
        }
        #endregion

        #region buttons

        //Save Settings
        private void SaveChangesButton_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.nightImage = Image2Box.Text;
            Properties.Settings.Default.dayImage = Image1Box.Text;

            //state validation
            if (Properties.Settings.Default.zipCode != Image3Box.Text)
            {
                using (var sr = new StringReader(Properties.Resources.completezip))
                {
                    string ziptest = streamData.Substring(0, streamData.IndexOf(','));
                    for (int i = 0; i <= 5 - ziptest.Length; i++)
                    {
                        ziptest = '0' + ziptest;
                    }
                    while (ziptest != Image3Box.Text)
                    {
                        streamData = sr.ReadLine();
                        if (streamData == null)
                        {
                            System.Windows.MessageBox.Show("Invalid Zip Code");
                            streamData = "sssssss,";
                            return;
                        }
                        ziptest = streamData.Substring(0, streamData.IndexOf(','));

                        for (int i = 0; i < 5 - ziptest.Length; i++)
                        {
                            ziptest = '0' + ziptest;
                        }

                    }
                    Properties.Settings.Default.zipCode = Image3Box.Text;
                    streamData = streamData.Substring(streamData.IndexOf(',') + 1);
                    Properties.Settings.Default.lat = streamData.Substring(0, streamData.IndexOf(','));
                    streamData = streamData.Substring(streamData.IndexOf(',') + 1);
                    Properties.Settings.Default.lng = streamData;
                    streamData = "sssssss,";
                    Properties.Settings.Default.Save();
                    webReloadTimer.Interval = 1;
                }

            }

            Properties.Settings.Default.zipCode = Image3Box.Text;
            Properties.Settings.Default.Save();

            //Controls running application on startup
            if (StartupBox.IsChecked.Value)
            {
                CreateShortcut();
                Properties.Settings.Default.runAtStart = true;
            }
            else
            {
                DeleteShortcut();
                Properties.Settings.Default.runAtStart = false;
            }

            //Makes sure image paths are valid
            if (UpdateWallpaper())
            {
                //Copies images to application folder
                try
                {
                    if (!System.IO.Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\ScreenFlow\"))
                    {
                        System.IO.Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\ScreenFlow\");
                    }
                    if (Properties.Settings.Default.dayImage != Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\ScreenFlow\" + System.IO.Path.GetFileName(Properties.Settings.Default.dayImage))
                    {
                        System.IO.File.Copy(Properties.Settings.Default.dayImage, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\ScreenFlow\" + System.IO.Path.GetFileName(Properties.Settings.Default.dayImage), true);
                        Properties.Settings.Default.dayImage = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\ScreenFlow\" + System.IO.Path.GetFileName(Properties.Settings.Default.dayImage);
                    }
                    if (Properties.Settings.Default.nightImage != Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\ScreenFlow\" + System.IO.Path.GetFileName(Properties.Settings.Default.nightImage))
                    {
                        System.IO.File.Copy(Properties.Settings.Default.nightImage, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\ScreenFlow\" + System.IO.Path.GetFileName(Properties.Settings.Default.nightImage), true);
                        Properties.Settings.Default.nightImage = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\ScreenFlow\" + System.IO.Path.GetFileName(Properties.Settings.Default.nightImage);
                    }
                    Properties.Settings.Default.Save();
                    Image2Box.Text = Properties.Settings.Default.nightImage;
                    Image1Box.Text = Properties.Settings.Default.dayImage;
                }
                catch (Exception ex)
                {

                }
            }
            else
            {
                System.Windows.MessageBox.Show("One of the paths you entered was invalid");
            }
        }

        private void Image2Button_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog 
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 
            dlg.Filter = "Image Files |*.jpg;*.jpeg;*.png;*.bmp";


            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();


            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                string filename = dlg.FileName;
                Image2Box.Text = @filename;
            }

        }

        private void Image1Button_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog 
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 
            dlg.Filter = "Image Files |*.jpg;*.jpeg;*.png;*.bmp";

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                string filename = dlg.FileName;
                Image1Box.Text = @filename;
            }
        }
    }

}
    #endregion

//    static class StateArray
//    {

//        public static List<US_State> states;

//        static StateArray()
//        {
//            states = new List<US_State>(50);
//            states.Add(new US_State("AL", "Alabama"));
//            states.Add(new US_State("AK", "Alaska"));
//            states.Add(new US_State("AZ", "Arizona"));
//            states.Add(new US_State("AR", "Arkansas"));
//            states.Add(new US_State("CA", "California"));
//            states.Add(new US_State("CO", "Colorado"));
//            states.Add(new US_State("CT", "Connecticut"));
//            states.Add(new US_State("DE", "Delaware"));
//            states.Add(new US_State("DC", "District Of Columbia"));
//            states.Add(new US_State("FL", "Florida"));
//            states.Add(new US_State("GA", "Georgia"));
//            states.Add(new US_State("HI", "Hawaii"));
//            states.Add(new US_State("ID", "Idaho"));
//            states.Add(new US_State("IL", "Illinois"));
//            states.Add(new US_State("IN", "Indiana"));
//            states.Add(new US_State("IA", "Iowa"));
//            states.Add(new US_State("KS", "Kansas"));
//            states.Add(new US_State("KY", "Kentucky"));
//            states.Add(new US_State("LA", "Louisiana"));
//            states.Add(new US_State("ME", "Maine"));
//            states.Add(new US_State("MD", "Maryland"));
//            states.Add(new US_State("MA", "Massachusetts"));
//            states.Add(new US_State("MI", "Michigan"));
//            states.Add(new US_State("MN", "Minnesota"));
//            states.Add(new US_State("MS", "Mississippi"));
//            states.Add(new US_State("MO", "Missouri"));
//            states.Add(new US_State("MT", "Montana"));
//            states.Add(new US_State("NE", "Nebraska"));
//            states.Add(new US_State("NV", "Nevada"));
//            states.Add(new US_State("NH", "New Hampshire"));
//            states.Add(new US_State("NJ", "New Jersey"));
//            states.Add(new US_State("NM", "New Mexico"));
//            states.Add(new US_State("NY", "New York"));
//            states.Add(new US_State("NC", "North Carolina"));
//            states.Add(new US_State("ND", "North Dakota"));
//            states.Add(new US_State("OH", "Ohio"));
//            states.Add(new US_State("OK", "Oklahoma"));
//            states.Add(new US_State("OR", "Oregon"));
//            states.Add(new US_State("PA", "Pennsylvania"));
//            states.Add(new US_State("RI", "Rhode Island"));
//            states.Add(new US_State("SC", "South Carolina"));
//            states.Add(new US_State("SD", "South Dakota"));
//            states.Add(new US_State("TN", "Tennessee"));
//            states.Add(new US_State("TX", "Texas"));
//            states.Add(new US_State("UT", "Utah"));
//            states.Add(new US_State("VT", "Vermont"));
//            states.Add(new US_State("VA", "Virginia"));
//            states.Add(new US_State("WA", "Washington"));
//            states.Add(new US_State("WV", "West Virginia"));
//            states.Add(new US_State("WI", "Wisconsin"));
//            states.Add(new US_State("WY", "Wyoming"));
//        }

//        public static List<string> Abbreviations()
//        {
//            List<string> abbrevList = new List<string>(states.Count);
//            foreach (var state in states)
//            {
//                abbrevList.Add(state.Abbreviations);
//            }
//            return abbrevList;
//        }

//        public static string[] Names()
//        {
//            List<string> nameList = new List<string>(states.Count);
//            foreach (var state in states)
//            {
//                nameList.Add(state.Name);
//            }
//            return nameList.ToArray();
//        }

//        public static US_State[] States()
//        {
//            return states.ToArray();
//        }

//    }

//    class US_State
//    {

//        public US_State()
//        {
//            Name = null;
//            Abbreviations = null;
//        }

//        public US_State(string ab, string name)
//        {
//            Name = name;
//            Abbreviations = ab;
//        }

//        public string Name { get; set; }

//        public string Abbreviations { get; set; }



//    }
//}

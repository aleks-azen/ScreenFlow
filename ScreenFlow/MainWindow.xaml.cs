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
        public System.Timers.Timer webReloadTimer = new System.Timers.Timer();
        public System.Timers.Timer wallpaperUpdateTimer = new System.Timers.Timer();
        public System.Windows.Forms.NotifyIcon ni = new System.Windows.Forms.NotifyIcon();


        public MainWindow()
        {
            //Thread main = new Thread(new ThreadStart(mainLoop));
            UpdateWallpaper();
            CreateShortcut();
            
            wallpaperUpdateTimer.Elapsed += new ElapsedEventHandler(WallpaperTimerElapsed);
            wallpaperUpdateTimer.AutoReset = false;
            wallpaperUpdateTimer.Interval = IntervalUntilNextChange().TotalMilliseconds;
            wallpaperUpdateTimer.Start();
            webReloadTimer.Elapsed += new ElapsedEventHandler(WebTimerElapsed);
            webReloadTimer.AutoReset = false;
            webReloadTimer.Interval = TimeSpan.FromSeconds(5).TotalMilliseconds;
            webReloadTimer.Start();
            InitializeComponent();
            Image1Box.Text = Properties.Settings.Default.dayImage;
            Image2Box.Text = Properties.Settings.Default.nightImage;
            ni.Icon = new System.Drawing.Icon(@"E:\Users\Downloads\nightttime.ico");
            ni.DoubleClick +=
                delegate (object sender, EventArgs args)
                {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                    ni.Visible = false;
                };
            ni.Visible = true;
            this.Hide();

        }

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

        public void UpdateWallpaper()
        {
            if (CheckTime() == timestate.Night) SetWallpaper(Properties.Settings.Default.nightImage);
            else SetWallpaper(Properties.Settings.Default.dayImage);
        }
        public static void SetWallpaper(String path)
        {
            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path,
                SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
        }
        public timestate CheckTime()
        {
            if (DateTime.Now.TimeOfDay.CompareTo(Properties.Settings.Default.sunrise) < 0 || DateTime.Now.TimeOfDay.CompareTo(Properties.Settings.Default.sunset) > 0) return timestate.Night;
            else return timestate.Day;
        }
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
        private static void CreateShortcut()
        {
            WshShell shell = new WshShell();
            string shortcutAddress = Environment.GetFolderPath(Environment.SpecialFolder.Startup) + @"\ScreenFlow.lnk";
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutAddress);
            shortcut.Description = "Shortcut";
            shortcut.Hotkey = "Ctrl+Shift+N";
            shortcut.TargetPath = System.Windows.Forms.Application.StartupPath + @"\ScreenFlow.exe";
            shortcut.Save();
            StateArray.states.ElementAt(6);
        }
        static TimeSpan stringToTimeSpan(String time)
        {

            int hours = int.Parse(time.Substring(0, time.IndexOf(':')));
            time = time.Substring(time.IndexOf(':') + 1);
            int minutes = int.Parse(time.Substring(0, time.IndexOf(' ')));
            if (time.Contains("PM")) hours += 12;
            TimeSpan t = new TimeSpan(hours, minutes, 0);
            return t;
        }
        #endregion

        #region timerTrigers
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
        private void WebTimerElapsed(object source, ElapsedEventArgs e)
        {
            if (CheckForInternetConnection())
            {
                try
                {
                    var doc = Dcsoup.Parse(new Uri("http://www.timeanddate.com/sun/usa/new-york"), 5000);
                    // <span itemprop="ratingValue">86</span>
                    var ratingSpan = doc.Select("span[class=three]");
                    var test = ratingSpan.ToArray();
                    if (Properties.Settings.Default.sunrise != stringToTimeSpan(test[0].Text))
                    {
                        Properties.Settings.Default.sunrise = stringToTimeSpan(test[0].Text);
                        Properties.Settings.Default.sunset = stringToTimeSpan(test[1].Text);
                        Properties.Settings.Default.Save();
                        wallpaperUpdateTimer.Interval = IntervalUntilNextChange().TotalMilliseconds;
                        UpdateWallpaper();
                    }
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
        private void SaveChangesButton_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.nightImage = Image2Box.Text;
            Properties.Settings.Default.dayImage = Image1Box.Text;
            Properties.Settings.Default.state = Image3Box.Text;
            Properties.Settings.Default.Save();
            UpdateWallpaper();
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
                // Open document 
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
                // Open document 
                string filename = dlg.FileName;
                Image1Box.Text = @filename;
            }
        }
    }
    #endregion

    static class StateArray
    {

        public static List<US_State> states;

        static StateArray()
        {
            states = new List<US_State>(50);
            states.Add(new US_State("AL", "Alabama"));
            states.Add(new US_State("AK", "Alaska"));
            states.Add(new US_State("AZ", "Arizona"));
            states.Add(new US_State("AR", "Arkansas"));
            states.Add(new US_State("CA", "California"));
            states.Add(new US_State("CO", "Colorado"));
            states.Add(new US_State("CT", "Connecticut"));
            states.Add(new US_State("DE", "Delaware"));
            states.Add(new US_State("DC", "District Of Columbia"));
            states.Add(new US_State("FL", "Florida"));
            states.Add(new US_State("GA", "Georgia"));
            states.Add(new US_State("HI", "Hawaii"));
            states.Add(new US_State("ID", "Idaho"));
            states.Add(new US_State("IL", "Illinois"));
            states.Add(new US_State("IN", "Indiana"));
            states.Add(new US_State("IA", "Iowa"));
            states.Add(new US_State("KS", "Kansas"));
            states.Add(new US_State("KY", "Kentucky"));
            states.Add(new US_State("LA", "Louisiana"));
            states.Add(new US_State("ME", "Maine"));
            states.Add(new US_State("MD", "Maryland"));
            states.Add(new US_State("MA", "Massachusetts"));
            states.Add(new US_State("MI", "Michigan"));
            states.Add(new US_State("MN", "Minnesota"));
            states.Add(new US_State("MS", "Mississippi"));
            states.Add(new US_State("MO", "Missouri"));
            states.Add(new US_State("MT", "Montana"));
            states.Add(new US_State("NE", "Nebraska"));
            states.Add(new US_State("NV", "Nevada"));
            states.Add(new US_State("NH", "New Hampshire"));
            states.Add(new US_State("NJ", "New Jersey"));
            states.Add(new US_State("NM", "New Mexico"));
            states.Add(new US_State("NY", "New York"));
            states.Add(new US_State("NC", "North Carolina"));
            states.Add(new US_State("ND", "North Dakota"));
            states.Add(new US_State("OH", "Ohio"));
            states.Add(new US_State("OK", "Oklahoma"));
            states.Add(new US_State("OR", "Oregon"));
            states.Add(new US_State("PA", "Pennsylvania"));
            states.Add(new US_State("RI", "Rhode Island"));
            states.Add(new US_State("SC", "South Carolina"));
            states.Add(new US_State("SD", "South Dakota"));
            states.Add(new US_State("TN", "Tennessee"));
            states.Add(new US_State("TX", "Texas"));
            states.Add(new US_State("UT", "Utah"));
            states.Add(new US_State("VT", "Vermont"));
            states.Add(new US_State("VA", "Virginia"));
            states.Add(new US_State("WA", "Washington"));
            states.Add(new US_State("WV", "West Virginia"));
            states.Add(new US_State("WI", "Wisconsin"));
            states.Add(new US_State("WY", "Wyoming"));
        }

        public static string[] Abbreviations()
        {
            List<string> abbrevList = new List<string>(states.Count);
            foreach (var state in states)
            {
                abbrevList.Add(state.Abbreviations);
            }
            return abbrevList.ToArray();
        }

        public static string[] Names()
        {
            List<string> nameList = new List<string>(states.Count);
            foreach (var state in states)
            {
                nameList.Add(state.Name);
            }
            return nameList.ToArray();
        }

        public static US_State[] States()
        {
            return states.ToArray();
        }

    }

    class US_State
    {

        public US_State()
        {
            Name = null;
            Abbreviations = null;
        }

        public US_State(string ab, string name)
        {
            Name = name;
            Abbreviations = ab;
        }

        public string Name { get; set; }

        public string Abbreviations { get; set; }



    }
}

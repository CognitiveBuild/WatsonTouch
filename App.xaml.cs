using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace WatsonTouch
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            string processName = Process.GetCurrentProcess().ProcessName;

            if (Process.GetProcesses().Count(p => p.ProcessName == processName) > 1)
            {
                return;
            }
        }
    }
}

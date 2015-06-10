using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;

namespace MyBootstrapperCore
{
    public class Application : BootstrapperApplication
    {
        protected override void Run()
        {
            var mainWindow = new MainWindow();
            Engine.Detect();
            mainWindow.Closed += (o, e) => mainWindow.Dispatcher.InvokeShutdown();
            mainWindow.Show();
            Dispatcher.Run();
        }
    }
}

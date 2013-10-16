using ConsoleApplication1.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            var settingsMain = Settings.Default;
            settingsMain.Test = "main domain";
            settingsMain.Save();
            var domain = AppDomain.CreateDomain(AppDomain.CurrentDomain.FriendlyName);
            domain.DoCallBack(() =>
            {
                var settingsDomain = Settings.Default;
                Console.WriteLine("Test={0}", settingsDomain.Test);
                settingsDomain.Test = "other domain";
                settingsDomain.Save();
            });
            Console.ReadLine();
        }
    }
}

using ConsoleApplication1.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Trio.SharedLibrary;

namespace ConsoleApplication1
{
    class Program
    {
        class Func_Print : ScriptEngine.Func
        {
            public Func_Print()
                : base("print", false)
            {
                Params.Add("_param");
            }

            public override ScriptEngine.Value Execute(ScriptEngine.Context context)
            {
                var a = context.GetVariable("_param", false);
                Debug.Assert(a != null);
                Console.Write(a.Value.ToString());
                return null;
            }
        }
        static void Main(string[] args)
        {
            try
            {
                string[] lines = File.ReadAllLines("example1.txt");
                var script = ScriptEngine.Script.Parse(lines, null);
                var context = new ScriptEngine.Context(null);
                var printFunc = new Func_Print();
                context.AddFunc(printFunc);
                script.Execute(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            Console.ReadLine();
        }
    }
}

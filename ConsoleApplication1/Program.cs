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

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string[] lines = File.ReadAllLines("example1.txt");
                var token = ScriptEngine.Tokenize(lines);
                var script = ScriptEngine.ParseScript(token);
                var context = new ScriptEngine.Context(script);
                var varX = context.AddVariable(null, "x");
                varX.Value = new ScriptEngine.ValueString("o");
                var varY = context.AddVariable(null, "y");
                varY.Value = new ScriptEngine.ValueString("O");
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

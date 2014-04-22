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
        class Func_Print : ScriptEngine.Func
        {
            class Stmt_Print : ScriptEngine.Stmt
            {
                public Stmt_Print(ScriptEngine.Token startToken)
                    : base(startToken)
                {
                }
                public override void Execute(ScriptEngine.Context context)
                {
                    var a = context.GetVariable("_param", false);
                    Debug.Assert(a != null);
                    Console.Write(a.Value.ToString());
                }
            }

            public Func_Print()
                : base(null, "print", new Stmt_Print(null))
            {
                Params.Add("_param");
            }
        }
        static void Main(string[] args)
        {
            try
            {
                string[] lines = File.ReadAllLines("example1.txt");
                var token = ScriptEngine.Tokenize(lines);
                var script = ScriptEngine.ParseScript(token);
                var context = new ScriptEngine.Context(script);
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

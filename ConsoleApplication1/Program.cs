using ConsoleApplication1.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApplication1
{
    class Program
    {
        interface IP
        {
            int P { get; set; }
        }

        interface IPs
        {
            int this[int index] { get; set; }
        }

        [DebuggerDisplay("P:{p}")]
        struct S : IP
        {
            int p;

            public int P
            {
                get { return p; }
                set { p = value; }
            }
        }

        struct Ss : IPs
        {
            S[] ss;

            public Ss(S[] ss)
            {
                this.ss = ss;
            }

            public int this[int index]
            {
                get { return ss[index].P; }
                set { ss[index].P = value; }
            }
        }

        static void Main(string[] args)
        {
            S[] ss1 = new S[1024];
            Ss ss2 = new Ss(ss1);

            foreach (IP s in ss1)
                s.P = 0;

            for (int i = 0; i < 1024; i++)
                ss2[i] = 2; 

            Console.ReadLine();
        }
    }
}

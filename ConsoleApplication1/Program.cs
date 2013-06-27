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
            var sol = Babylon.LocalizationSolution.LocalizationSolutionSerializer.DeserializeSolution(@"S:\SOURCE\Trio\MP v3\Chinese Translation\MPv3_Minhua_CHS.btp");
            var items = from prj in sol.Projects.OfType<Babylon.VSProject.VSLocalizationProject>()
                        from ri in prj.Items
                        from li in ri.LocaleItems
                        where li.Text == null
                        select new {prj, ri, li};
            foreach (var item in items)
            {
                Console.WriteLine("Prj:{0}, Res:{1}, Loc:{2}", item.prj.Name, item.ri.Name, item.li.Locale);
            }
            Console.ReadLine();
        }
    }
}

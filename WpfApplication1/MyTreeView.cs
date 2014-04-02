using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace WpfApplication1
{
    public class MyTreeView : TreeView
    {
        protected override DependencyObject GetContainerForItemOverride()
        {
            return new MyTreeViewItem();
        }
    }
}

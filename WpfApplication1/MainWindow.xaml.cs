using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfApplication1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ShowDialog(Window tw)
        {
            tw.Owner = this;
            tw.ShowInTaskbar = false;
            tw.ShowDialog();
        }

        private void MenuItemOpenClick(object sender, RoutedEventArgs e)
        {
            var tw = new ToolWindow();
            ShowDialog(tw);
        }

        private void _3DView_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new Window3DView();
            ShowDialog(wnd);

        }
    }
}

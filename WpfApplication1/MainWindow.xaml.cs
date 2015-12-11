using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WpfApplication1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static IEnumerable SystemColors { get; private set; }

        static MainWindow()
        {
            SystemColors = from prop in typeof(SystemColors).GetProperties(BindingFlags.Public | BindingFlags.Static)
                           where prop.Name.EndsWith("Brush")
                           let brush = prop.GetValue(null, null) as SolidColorBrush
                           let color = brush.Color
                           select new { Brush = brush, Name = string.Format("{0}: {1}", color, prop.Name) };
        }

        public BitmapDecoder IconDecoder { get; private set; }

        public MainWindow()
        {
            IconDecoder = BitmapDecoder.Create(new Uri("pack://application:,,,/Application.ico"), BitmapCreateOptions.None, BitmapCacheOption.Default);

            InitializeComponent();

            MouseDown += (o, e) =>
            {
                Debug.Print("{0}:MouseDown: btn={1}, clicks={2}", Environment.TickCount, e.ChangedButton, e.ClickCount);
            };

            MouseDoubleClick += (o, e) =>
            {
                Debug.Print("{0}:MouseDoubleClick: btn={1}, clicks={2}", Environment.TickCount, e.ChangedButton, e.ClickCount);
            };
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

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var cb = sender as ComboBox;
            if (cb != null && cb.IsDropDownOpen)
                Dispatcher.BeginInvoke(DispatcherPriority.Background, (Func<bool>)dgData.CommitEdit);
        }

        private void ListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            Debug.Print("ListBox_PreviewMouseWheel");
            e.Handled = true;
            var lb = sender as ListBox;
            lb.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = ListBox.MouseWheelEvent });
        }
    }
}

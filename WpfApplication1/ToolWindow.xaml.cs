using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WpfApplication1
{
    /// <summary>
    /// Interaction logic for ToolWindow.xaml
    /// </summary>
    public partial class ToolWindow : Window
    {

        static public DependencyProperty PercentProperty = DependencyProperty.Register("Percent", typeof(double), typeof(ToolWindow));
        public double Percent
        {
            get { return (double)GetValue(PercentProperty); }
            set { SetValue(PercentProperty, value); }
        }

        class Progress : INotifyPropertyChanged
        {

            public double Percent
            {
                get { return _percent; }
                set
                {
                    if (_percent != value)
                    {
                        _percent = value;
                        NotifyPropertyChanged("Percent");
                    }
                }
            }
            double _percent;

            public event PropertyChangedEventHandler PropertyChanged;

            void NotifyPropertyChanged(string propName)
            {
                var propertyChanged = PropertyChanged;
                if (propertyChanged != null)
                    propertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }

        Timer _timer;
        Progress progress = new Progress();
        public ToolWindow()
        {
            InitializeComponent();

            ProgressBar.DataContext = progress;

            _timer = new Timer(_ =>
            {
                progress.Percent += 1;
                if (progress.Percent > 100)
                    progress.Percent = 0;
            });

            Closed += (o, e) =>
            {
                _timer.Dispose();
            };
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (StartStop.IsChecked == true)
            {
                ProgressBar.Visibility = Visibility.Visible;
                _timer.Change(0, 100);
            }
            else
            {
                ProgressBar.Visibility = Visibility.Collapsed;
                _timer.Change(0, Timeout.Infinite);
            }
        }
    }
}

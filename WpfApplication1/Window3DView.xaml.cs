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
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace WpfApplication1
{
    /// <summary>
    /// Interaction logic for Window3DView.xaml
    /// </summary>
    public partial class Window3DView : Window
    {
        public Window3DView()
        {
            InitializeComponent();

            //var serpentine = new Serpentine(1024, 3, 0.5, 10, 20);
            //serpentine.PopulateMesh(__Mesh);
        }
    }
}

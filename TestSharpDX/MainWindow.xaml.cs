using SharpDX.Direct3D9;
using SharpDX.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace TestSharpDX
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SharpDXHelper _sharpDXHelper;
        public MainWindow()
        {
            InitializeComponent();

            Loaded += (o, e) => _sharpDXHelper = new SharpDXHelper(__RenderControl);
        }

        protected override void OnClosed(EventArgs e)
        {
            _sharpDXHelper.Dispose();

            base.OnClosed(e);
        }
    }

    class SharpDXHelper : IDisposable
    {
        RenderControl _view;
        Direct3DEx _direct3DEx;
        DeviceEx _deviceEx;

        public SharpDXHelper(RenderControl renderControl)
        {
            Debug.Assert(renderControl != null);
            _view = renderControl;

            _direct3DEx = new Direct3DEx();

            var pp = new PresentParameters(_view.ClientSize.Width, _view.ClientSize.Height);

            _deviceEx = new DeviceEx(_direct3DEx, 0, DeviceType.Hardware, _view.Handle, CreateFlags.Multithreaded, pp);
        }

        public void Dispose()
        {
            _deviceEx.Dispose();
            _direct3DEx.Dispose();
        }
    }
}

﻿using System;
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
        DispatcherTimer _timer = new DispatcherTimer();
        Serpentine _serpentine = new Serpentine();
        int _segment = 0, _numSegments = 1024;
        MeshGeometry3D _mesh = new MeshGeometry3D();

        public MainWindow()
        {
            InitializeComponent();

            _serpentine.NumSectors = 8;
            _serpentine.NumSegments = _numSegments;
            _serpentine.Thickness = 0.4;
            _serpentine.Radius = 10;
            _serpentine.Length = 20;
            _serpentine.NumRevolutions = 4;
            RedOne.Geometry = _mesh;

            _timer.Tick += (o, e) =>
            {
                if (_segment == 0)
                    ResetMesh();

                for (int i = _segment; i < _segment + 8; i++)
                    _serpentine.AddSegment(_mesh, i);

                _segment += 8;
                if (_segment >= _numSegments)
                    _segment = 0;
            };
            _timer.Interval = TimeSpan.FromMilliseconds(10);
            _timer.Start();
        }

        private void ResetMesh()
        {
            _mesh.Positions.Clear();
            _mesh.Normals.Clear();
            _mesh.TriangleIndices.Clear();
            _mesh.TextureCoordinates.Clear();
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

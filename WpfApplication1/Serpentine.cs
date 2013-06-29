using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media.Media3D;

namespace WpfApplication1
{
    [RuntimeNameProperty("Name")]
    public class Serpentine : DependencyObject
    {
        static readonly Type _myType = typeof(Serpentine);
        #region Props

        public string Name { get; set; }

        public int NumSegments
        {
            get { return (int)GetValue(NumSegmentsProperty); }
            set { SetValue(NumSegmentsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for NumSegments.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NumSegmentsProperty = DependencyProperty.Register("NumSegments", typeof(int), _myType, new PropertyMetadata(1));


        public int NumSectors
        {
            get { return (int)GetValue(NumSectorsProperty); }
            set { SetValue(NumSectorsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for NumSectors.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NumSectorsProperty = DependencyProperty.Register("NumSectors", typeof(int), _myType, new PropertyMetadata(1));


        public double NumRevolutions
        {
            get { return (double)GetValue(NumRevolutionsProperty); }
            set { SetValue(NumRevolutionsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for NumRevolutions.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NumRevolutionsProperty = DependencyProperty.Register("NumRevolutions", typeof(double), _myType, new PropertyMetadata(1.0));


        public double Thickness
        {
            get { return (double)GetValue(ThicknessProperty); }
            set { SetValue(ThicknessProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Thickness.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register("Thickness", typeof(double), _myType, new PropertyMetadata(1.0));


        public double Radius
        {
            get { return (double)GetValue(RadiusProperty); }
            set { SetValue(RadiusProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Radius.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register("Radius", typeof(double), _myType, new PropertyMetadata(10.0));


        public double Length
        {
            get { return (double)GetValue(LengthProperty); }
            set { SetValue(LengthProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Length.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LengthProperty = DependencyProperty.Register("Length", typeof(double), _myType, new PropertyMetadata(10.0));

        static readonly DependencyPropertyKey MeshPropertyKey = DependencyProperty.RegisterReadOnly("Mesh", typeof(MeshGeometry3D), _myType, new PropertyMetadata(null, null, CoerceMeshCallback));
        public static readonly DependencyProperty MeshProperty = MeshPropertyKey.DependencyProperty;
        public MeshGeometry3D Mesh
        {
            get { return (MeshGeometry3D)GetValue(MeshProperty); }
        }
        static object CoerceMeshCallback(DependencyObject d, object baseValue)
        {
            var sp = d as Serpentine;
            return sp == null ? baseValue : sp.CoerceMeshCallback(baseValue);
        }
        object CoerceMeshCallback(object baseValue)
        {
            var mesh = new MeshGeometry3D();
            PopulateMesh(mesh);
            return mesh;
        }

        #endregion // Props

        Point3D GetSegmentPoint(int segment)
        {
            double offset = (double)(segment - NumSegments / 2) / NumSegments;
            double alpha = offset * Math.PI * 2 * NumRevolutions;
            double x = Math.Cos(alpha) * Radius;
            double y = Math.Sin(alpha) * Radius;
            double z = offset * Length;
            return new Point3D(x, y, z);
        }

        void AddSegment(MeshGeometry3D mesh, int segment)
        {
            var ps = GetSegmentPoint(segment);
            Vector3D oY;
            if (segment == 0)
            {
                var pn = GetSegmentPoint(segment + 1);
                oY = pn - ps;
            }
            else if (segment < NumSegments - 1)
            {
                var pn = GetSegmentPoint(segment + 1);
                var pp = GetSegmentPoint(segment - 1);
                oY = pn - pp;
            }
            else
            {
                var pp = GetSegmentPoint(segment - 1);
                oY = ps - pp;
            }
            oY.Normalize();
            var oX = Vector3D.CrossProduct(oY, new Vector3D(0, 0, 1));
            var oZ = Vector3D.CrossProduct(oX, oY);
            for (int sector = 0; sector < NumSectors; sector++)
            {
                double alpha = Math.PI * 2 * sector / NumSectors;
                var x = oX * Math.Cos(alpha) * Thickness;
                var y = oZ * Math.Sin(alpha) * Thickness;
                var s = x + y;
                mesh.Positions.Add(ps + s);
                mesh.Normals.Add(s);
            }

            if (segment > 0)
            {
                int startPoint = (segment - 1) * NumSectors;
                int modulus = NumSectors;
                for (int sector = 0; sector < NumSectors; sector++)
                {
                    mesh.TriangleIndices.Add(startPoint + sector);
                    mesh.TriangleIndices.Add(startPoint + (sector + 1) % NumSectors);
                    mesh.TriangleIndices.Add(startPoint + sector + NumSectors);
                    mesh.TriangleIndices.Add(startPoint + (sector + 1) % NumSectors);
                    mesh.TriangleIndices.Add(startPoint + NumSectors + (sector + 1) % NumSectors);
                    mesh.TriangleIndices.Add(startPoint + sector + NumSectors);
                }
            }
        }

        public void PopulateMesh(MeshGeometry3D mesh)
        {
            mesh.Positions.Clear();
            mesh.Normals.Clear();
            mesh.TriangleIndices.Clear();
            mesh.TextureCoordinates.Clear();

            for (int segment = 0; segment < NumSegments; segment++)
            {
                AddSegment(mesh, segment);
            }
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            switch (e.Property.Name)
            {
                case "NumSegments":
                case "NumSectors":
                case "NumRevolutions":
                case "Thickness":
                case "Radius":
                case "Length":
                    CoerceValue(MeshProperty);
                    break;
            }
        }
    }
}

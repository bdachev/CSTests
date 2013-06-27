using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Media3D;

namespace WpfApplication1
{
    public class Serpentine
    {
        public int NumSegments { get; set; }
        public int NumSectors { get; set; }
        public double NumRevolutions { get; set; }
        public double Thickness { get; set; }
        public double Radius { get; set; }
        public double Length { get; set; }

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

        public MeshGeometry3D CreateMesh()
        {
            var mesh = new MeshGeometry3D();
            PopulateMesh(mesh);
            return mesh;
        }
    }
}

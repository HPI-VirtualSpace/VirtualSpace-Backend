using System;
using System.Collections.Generic;
using MIConvexHull;

namespace VirtualSpace.Backend
{
    internal class VoronoiCell
    {
        public MIVector Generator { get; set; }
        public List<List<MIVector>> CellVertices = new List<List<MIVector>>();
        public List<Tuple<MIVector, MIVector>> Edges = new List<Tuple<MIVector, MIVector>>();
    }

    internal class DelaunayCell : TriangulationCell<MIVector, DelaunayCell>
    {
        MIVector _circumcenter;
        public MIVector Circumcenter
        {
            get
            {
                _circumcenter = _circumcenter ?? GetCircumcenter();
                return _circumcenter;
            }
        }

        public MIVector Center
        {
            get
            {
                var v1 = Vertices[0];
                var v2 = Vertices[1];
                var v3 = Vertices[2];

                return new MIVector((v1.X + v2.X + v3.X) / 3, (v1.Z + v2.Z + v3.Z) / 3);
            }
        }

        MIVector GetCircumcenter()
        {
            // From MathWorld: http://mathworld.wolfram.com/Circumcircle.html
            var points = Vertices;

            double[,] m = new double[3, 3];
            
            for (int i = 0; i < 3; i++)
            {
                m[i, 0] = points[i].Position[0];
                m[i, 1] = points[i].Position[1];
                m[i, 2] = 1;
            }
            var a = Det(m);
            
            for (int i = 0; i < 3; i++)
            {
                m[i, 0] = LengthSquared(points[i].Position);
            }
            var dx = -Det(m);
            
            for (int i = 0; i < 3; i++)
            {
                m[i, 1] = points[i].Position[0];
            }
            var dy = Det(m);
            
            for (int i = 0; i < 3; i++)
            {
                m[i, 2] = points[i].Position[1];
            }
            var c = -Det(m);

            var s = -1.0 / (2.0 * a);
            var r = Math.Abs(s) * Math.Sqrt(dx * dx + dy * dy - 4 * a * c);
            return new MIVector(s * dx, s * dy);
        }

        double Det(double[,] m)
        {
            return m[0, 0] * ((m[1, 1] * m[2, 2]) - (m[2, 1] * m[1, 2])) - m[0, 1] * (m[1, 0] * m[2, 2] - m[2, 0] * m[1, 2]) + m[0, 2] * (m[1, 0] * m[2, 1] - m[2, 0] * m[1, 1]);
        }

        double LengthSquared(double[] v)
        {
            double norm = 0;
            for (int i = 0; i < v.Length; i++)
            {
                var t = v[i];
                norm += t * t;
            }
            return norm;
        }
    }
}

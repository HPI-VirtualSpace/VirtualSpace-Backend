using System;
using MIConvexHull;

namespace VirtualSpace.Backend
{
    internal class MIVector : IVertex
    {
        public double X;
        public double Z;

        public MIVector(double X, double Z)
        {
            this.X = X;
            this.Z = Z;
        }

        public double[] Position => new[] {X, Z};

        public MIVector Clone()
        {
            return new MIVector(X, Z);
        }

        public MIVector Normalized
        {
            get
            {
                double Magnitude = Math.Sqrt(X * X + Z * Z);
                if (Magnitude > 0)
                    return new MIVector(X / Magnitude, Z / Magnitude);
                return new MIVector(0, 0);
            }
        }
    }
}

#if UNITY
using UnityEngine;
#endif
using System;
using System.Runtime.Serialization;
using ProtoBuf;
using Object = System.Object;

namespace VirtualSpace.Shared
{
    [DataContract]
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class Vector
    {
        public const double Scale = 100000;
        [DataMember]
        public double X;
        [DataMember]
        public double Z;
        public double Length { get { return Magnitude; } }
        public double Magnitude { get { return Math.Sqrt(X * X + Z * Z); } }
        
        public double SqrMagnitude { get { return X * X + Z * Z; } }
        public static Vector Zero { get { return new Vector(0, 0); } }
        public static Vector One { get { return new Vector(1, 1); } }
        public static Vector NaN { get { return new Vector(float.NaN, float.NaN); } }
        public Vector Normal1 { get { return new Vector(Z, -X).Normalized; } }
        public Vector Normal2 { get { return new Vector(-Z, X).Normalized; } }

        /* constructors */
        public Vector() : this(0, 0) { }

        public Vector(double x, double z)
        {
            X = x;
            Z = z;
        }

        public Vector(Vector v) : this(v.X, v.Z) { }

        /* arithmetic operators */

        public static Vector operator +(Vector first, Vector second)
        {
            return new Vector(first.X + second.X, first.Z + second.Z);
        }

        public static Vector operator -(Vector first, Vector second)
        {
            return new Vector(first.X - second.X, first.Z - second.Z);
        }

        public static double operator *(Vector first, Vector second)
        {
            return first.X * second.X + first.Z * second.Z;
        }

        public static Vector operator *(Vector vector, double factor)
        {
            return new Vector(vector.X * factor, vector.Z * factor);
        }

        public static Vector operator *(double factor, Vector vector)
        {
            return new Vector(vector.X * factor, vector.Z * factor);
        }

        public static Vector operator /(Vector vector, double factor)
        {
            return (1 / factor) * vector;
        }

        /* comparer */

        private const double Epsilon = .0000000001;
        public bool AxisValuesEqual(double v1, double v2, double epsilon)
        {
            return Math.Abs(v1 - v2) < epsilon;
        }

        public bool Equals(Vector other, double axisEpsilon = Epsilon)
        {
            return other != null && AxisValuesEqual(X, other.X, axisEpsilon) && AxisValuesEqual(Z, other.Z, axisEpsilon);
        }

        public override bool Equals(Object other)
        {
            return other != null && other is Vector && Equals((Vector)other);
        }

        public override int GetHashCode()
        {
            int hash = 17;

            hash = hash * 23 + X.GetHashCode();
            hash = hash * 23 + Z.GetHashCode();

            return hash;
        }

        /* vector functions */

        public static float Distance(Vector first, Vector second)
        {
            return (float)(first - second).Magnitude;
        }

        public float Distance(Vector other)
        {
            return Distance(this, other);
        }

    public static Vector MapClamped(Vector value, Vector oldMin, Vector oldMax, Vector newMin, Vector newMax)
    { 
        double newX; 
        if (oldMax.X - oldMin.X == 0) newX = newMax.X; 
        double factorX = (value.X - oldMin.X) / (oldMax.X - oldMin.X);
        //double factorX = _clamp((value.X - oldMin.X) / (oldMax.X - oldMin.X), 0, 1); 
        double newY; 
        if (oldMax.Z - oldMin.Z == 0) newY = newMax.Z; 
        double factorY = (value.Z - oldMin.Z) / (oldMax.Z - oldMin.Z);
        //double factorY = _clamp((value.Z - oldMin.Z) / (oldMax.Z - oldMin.Z), 0, 1); 
        newX = factorX* newMax.X + (1 - factorX) * newMin.X; 
        newY = factorY* newMax.Z + (1 - factorY) * newMin.Z; 
        return new Vector(newX, newY);
    }

    public Vector Normalized
        {
            get
            {
                if (Magnitude > 0)
                    return new Vector(X / Magnitude, Z / Magnitude);
                else return this;
            }
        }

        public double Cross(Vector other)
        {
            return X * other.Z - other.X * Z;
        }

        public double Angle(Vector other)
        {
            return Math.Acos((this * other) / (Magnitude * other.Magnitude));
        }

        public Vector Rotate(double radians, Vector pivotPoint = null)
        {
            return RotateCounter(-radians, pivotPoint);
        }

        public Vector RotateCounter(double radians, Vector pivotPoint = null)
        {
            if (pivotPoint == null) pivotPoint = Zero;

            double s = Math.Sin(radians);
            double c = Math.Cos(radians);

            Vector originPoint = this - pivotPoint;

            double rotatedX = originPoint.X * c - originPoint.Z * s;
            double rotatedZ = originPoint.X * s + originPoint.Z * c;

            double X = rotatedX + pivotPoint.X;
            double Z = rotatedZ + pivotPoint.Z;

            return new Vector(X, Z);
        }

        /* converter */

        public override string ToString()
        {
            return GetType().Name + "(" + X.ToString() + Config.delimiter + Z.ToString() + ")";
        }

        public Tuple<T1, T2> ToTuple<T1, T2>()
        {
            return new Tuple<T1, T2>((T1)Convert.ChangeType(X, typeof(T1)), (T2)Convert.ChangeType(Z, typeof(T2)));
        }

        public static implicit operator IntPoint(Vector vector)
        {
            return new IntPoint(
                (long)(vector.X * Scale),
                (long)(vector.Z * Scale)
            );
        }

        public static implicit operator Vector(IntPoint point)
        {
            return new Vector(
                point.X / Scale,
                point.Y / Scale
            );
        }

#if UNITY
        public static implicit operator Vector(Vector3 unityVector)
        {
            return new Vector(unityVector.x, unityVector.z);
        }

        public static Vector3 ToVector3(Vector vector)
        {
            return new Vector3((float)vector.X, (float)0, (float)vector.Z);
        }

        public static Vector FromVector3(Vector3 unityVector)
        {
            return unityVector;
        } 

        public Vector2 ToVector2()
        {
            return new Vector2((float)X, (float)Z);
        }

        public Vector3 ToVector3()
        {
            return ToVector3(this);
        }
#endif

        public Vector Clone()
        {
            return new Vector(X, Z);
        }

#if BACKEND
        static Random random = new Random();
        public static Vector GetRandom(double minX, double maxX, double minZ, double maxZ)
        {
            double X = minX + random.NextDouble() * (maxX - minX);
            double Z = minZ + random.NextDouble() * (maxZ - minZ);

            return new Vector(X, Z);
        }
#endif
    }
}

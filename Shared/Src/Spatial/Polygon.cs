#if UNITY
using UnityEngine;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using ProtoBuf;
using Object = System.Object;

namespace VirtualSpace.Shared
{
    [DataContract]
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class Polygon
    {
        [DataMember]
        public List<Vector> Points;
        [IgnoreDataMember]
        public bool IsHole;
        [IgnoreDataMember]
        public PolygonList Holes;

        #region constructor

        /// <summary>
        /// Creates an empty polygon.
        /// </summary>
        public Polygon()
        {
            Points = new List<Vector>();
            IsHole = false;
            Holes = new PolygonList();
        }

        /// <summary>
        /// Creates a polygon from a list of Vectors and checks is it is an hole.
        /// </summary>
        /// <param name="vectorList">The list of vectors for initializing the polygon</param>
        /// <param name="holes">The polygon's holes</param>
        public Polygon(List<Vector> vectorList, PolygonList holes = null)
        {
            Points = vectorList;
            IsHole = !_isCCW(vectorList);
            Holes = holes ?? new PolygonList();
        }
       
        /// <summary>
        /// Creates a polygon from another polygon's able shape and checks is it is an hole.
        /// Note: If you want to keep the holes, use Clone().
        /// </summary>
        /// <param name="polygon">The polygon whose shape should be copied</param>
        /// <param name="holes">The polygon's holes</param>
        public Polygon(Polygon polygon, PolygonList holes = null) :
            this(new List<Vector>(polygon.Points), holes)
        { }

        /// <summary>
        /// Create a polygon from a Clipper Treenode. This creates a hierarchical polygon (the holes may contain "holes" themselves, which describe nested polygons).
        /// </summary>
        /// <param name="node">The treenode which should be turned into a polygon</param>
        public Polygon(PolyNode node) : this(node.Contour)
        {
            //if (IsHole != node.IsHole)
            //    Logger.Warn("Area returned by clipper is marked as " + (node.IsHole ? "Hole" : "Contour") + " but the vertices are " + (IsHole ? "CW." : "CCW."));
            node.Childs.ForEach((childNode) => Holes.Add(new Polygon(childNode)));
        }

        public Polygon Clone()
        {
            List<Vector> newList = new List<Vector>(Points.Count);
            Points.ForEach(vector => newList.Add(vector));
            return new Polygon(newList, Holes.DeepClone());
        }

        public Polygon DeepClone()
        {
            Polygon clone = new Polygon();
            foreach (Vector point in Points)
            {
                clone.Points.Add(point.Clone());
            }
            // todo: clone holes
            return clone;
        }

        public static Polygon AsRectangle(Vector dimensions, Vector offset = null)
        {
            Polygon polygon = new List<IntPoint>(
                new IntPoint[] {
                    new Vector(0, 0),
                    new Vector(dimensions.X, 0),
                    new Vector(dimensions.X, dimensions.Z),
                    new Vector(0, dimensions.Z)
                });
            if (offset != null)
                polygon += offset;
            return polygon;
        }

        public static Polygon AsCircle(float radius, Vector center, int stepSize = 16)
        {
            Polygon polygon = new Polygon();

            double radiansPerStep = 2 * Math.PI / stepSize;
            for (int i = 0; i < stepSize; i++)
            {
                Vector circleOffset = radius * new Vector(Math.Cos(radiansPerStep * i), Math.Sin(radiansPerStep * i));
                polygon.Points.Add(center + circleOffset);
            }

            return polygon;
        }

        public static Polygon AsLine(Vector start, Vector end)
        {
            Polygon polygon = new Polygon();

            Vector startToEnd = end - start;
            Vector normalOffset = .01f * startToEnd.Normal1;

            polygon.Points.Add(start);
            polygon.Points.Add(start + normalOffset);
            polygon.Points.Add(end + normalOffset);
            polygon.Points.Add(end);

            return polygon;
        }
        #endregion

        #region arithmetic operators

        public static Polygon operator +(Polygon polygon, Vector offset)
        {
            Polygon newPolygon = new Polygon() // need to return a new one
            {
                IsHole = polygon.IsHole
            };
            polygon.Points.ForEach(point => newPolygon.Points.Add(point + offset));
            polygon.Holes.ForEach((hole) => newPolygon.Holes.Add(hole + offset));
            return newPolygon;
        }

        #endregion

        #region comparer

        public bool Equals(Polygon other)
        {
            return other != null &&
                   IsHole == other.IsHole &&
                   ClipperUtility.EqualWithinEpsilon(this, other) &&
                   Holes.Equals(other.Holes);
        }

        public override bool Equals(Object other)
        {
            return other != null && other is Polygon && Equals((Polygon)other);
        }

        public bool EqualsAny(PolygonList list)
        {
            Polygon equalPolygon;
            return EqualsAny(list, out equalPolygon);
        }

        public bool EqualsAny(PolygonList list, out Polygon equalPolygon)
        {
            foreach (Polygon polygon in list)
            {
                if (Equals(polygon))
                {
                    equalPolygon = polygon;
                    return true;
                }
            }

            equalPolygon = null;
            return false;
        }

        public override int GetHashCode()
        {
            int hash = 17;

            hash = hash * 23 + Points.Count.GetHashCode();
            hash = hash * 23 + ClipperUtility.GetArea(this).GetHashCode();
            foreach (Vector point in Points)
                hash = hash * 23 + point.GetHashCode();
            hash = hash * 23 + (IsHole ? 31 : 0);
            hash = hash * 23 + Holes.GetHashCode();

            return hash;
        }

        #endregion

        #region geometric operations

        public Vector Center
        {
            get
            {
                Vector center = new Vector();

                if (Points.Count > 0)
                {
                    Points.ForEach(point => center += point);
                    center /= Points.Count;
                }

                return center;
            }
            set
            {
                Vector oldCenter = Center;
                List<Vector> newPoints = new List<Vector>();
                Points.ForEach(point => newPoints.Add(point - oldCenter + value));
                Points = newPoints;
                PolygonList newHoles = new PolygonList();
                Holes.ForEach(hole => newHoles.Add(hole + (value - oldCenter)));
                Holes = newHoles;
            }
        }

        public double Circumference
        {
            get
            {
                float circumference = 0;
                for (int i = 0; i < Points.Count; i++)
                {
                    circumference += Vector.Distance(Points[(i + 1) % Points.Count], Points[i]);
                }
                return circumference;
            }
        }

        public Tuple<Vector, Vector> EnclosingVectors
        {
            get
            {
                double minX = double.MaxValue;
                double minY = double.MaxValue;
                double maxX = double.MinValue;
                double maxY = double.MinValue;

                foreach (Vector point in Points)
                {
                    if (point.X < minX) minX = point.X;
                    if (point.Z < minY) minY = point.Z;
                    if (maxX < point.X) maxX = point.X;
                    if (maxY < point.Z) maxY = point.Z;
                }

                return new Tuple<Vector, Vector>(new Vector(minX, minY), new Vector(maxX, maxY));
            }
        }

        public Vector Dimension
        {
            get
            {
                Tuple<Vector, Vector> enclosingVectors = EnclosingVectors;
                return enclosingVectors.Item2 - enclosingVectors.Item1;
            }
        }

        private Vector _centroid = null;
        public Vector Centroid // https://en.wikipedia.org/wiki/Centroid#Centroid_of_a_polygon
        {
            get {
                if (_centroid != null)
                    return _centroid;

                double xSum = 0;
                double zSum = 0;
                for(int i = 0; i < Points.Count; i++)
                {
                    Vector current = Points[i];
                    Vector next = Points[(i + 1) % Points.Count];
                    double x = current.X + next.X;
                    double z = current.Z + next.Z;
                    double mixed = current.X * next.Z - next.X * current.Z;
                    xSum += x * mixed;
                    zSum += z * mixed;
                }
                xSum /= 6 * SignedContourArea();
                zSum /= 6 * SignedContourArea();

                _centroid = new Vector(xSum, zSum);

                return _centroid;
            }
        }

        // for the area an CCW calculation, see http://mathworld.wolfram.com/PolygonArea.html

        private static double _getDeterminant(Vector first, Vector second)
        {
            return first.X * second.Z - second.X * first.Z;
        }

        private static double _signedContourArea(List<Vector>.Enumerator enumerator)
        {
            double sum = 0;
            Vector first = null;
            Vector previous = null;
            while (enumerator.MoveNext())
            {
                Vector current = enumerator.Current;
                if (first == null) first = current;
                if (previous != null)
                    sum += _getDeterminant(previous, current);
                previous = current;
            }
            if (first != null)
            {
                sum += _getDeterminant(previous, first);
            }
            return sum / 2;
        }

        public double SignedContourArea()
        {
            return _signedContourArea(Points.GetEnumerator());
        }

        public double Area()
        {
            double holeSum = 0;
            Holes.ForEach((hole) => holeSum += hole.Area());
            return Math.Abs(SignedContourArea()) - holeSum;
        }

        public bool IsCCW()
        {
            // zero area is defined as CCW as well
            return SignedContourArea() >= 0;
        }

        private static bool _isCCW(List<Vector> list)
        {
            // zero area is defined as CCW as well
            return _signedContourArea(list.GetEnumerator()) >= 0;
        }

        public void MakeHole()
        {
            if (IsHole) return;

            Points.Reverse();
            IsHole = true;

            Holes.ForEach((hole) => hole.MakeContour());
        }

        public void MakeContour()
        {
            if (!IsHole) return;

            Points.Reverse();
            IsHole = false;

            Holes.ForEach((hole) => hole.MakeHole());
        }

        private void _flattenRecursive(ref PolygonList result)
        {
            foreach (Polygon polygon in Holes)
            {
                polygon._flattenRecursive(ref result);
                // if this is a contour, remove the hole's holes
                if (!IsHole)
                    polygon.Holes = new PolygonList();
            }
            // if this is a contour, add it to the result list
            if (!IsHole)
                result.Add(this);
        }

        public PolygonList Flatten()
        {
            PolygonList result = new PolygonList();
            Clone()._flattenRecursive(ref result);
            return result;
        }

        #endregion

        #region Converter
        public static implicit operator List<IntPoint>(Polygon polygon)
        {
            return polygon.Points.Select(vector => (IntPoint)vector).ToList();
        }

        public static implicit operator Polygon(List<IntPoint> list)
        {
            return new Polygon(list.Select(intPoint => (Vector)intPoint).ToList());
        }

        public static implicit operator List<Vector>(Polygon polygon)
        {
            List<Vector> result = new List<Vector>();
            polygon.Points.ForEach((point) => result.Add(point.Clone()));
            return result;
        }

        public static implicit operator PolygonList(Polygon polygon)
        {
            PolygonList wrapper = new PolygonList() { polygon };
            return wrapper;
        }
        #endregion

        #region Operators
        public void RotateCounter(double radians, Vector pivotPoint = null)
        {
            List<Vector> rotatedPoints = new List<Vector>();
            Points.ForEach(point => rotatedPoints.Add(point.RotateCounter(radians, pivotPoint)));
            Points = rotatedPoints;
        }

        public void Rotate(double radians, Vector pivotPoint = null)
        {
            RotateCounter(-radians, pivotPoint);
        }

        public override string ToString()
        {
            return Points.ToPrintableString();
        }
        #endregion

        #region Enumeration
        public IEnumerator<Vector> GetEnumerator()
        {
            return Points.GetEnumerator();
        }

        #endregion

        public bool Contains(Vector currentPlayerPosition)
        {
            Polygon pointPolygon = Polygon.AsCircle(.01f, currentPlayerPosition, 8);
            return ClipperUtility.ContainsWithinEpsilon(this, pointPolygon);
        }
    }
}

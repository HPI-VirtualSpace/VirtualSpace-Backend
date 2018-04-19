using System;
using System.Collections.Generic;

namespace VirtualSpace.Shared
{
    public static class ClipperUtility
    {
        public static bool HaveIntersection(PolygonList a, Polygon b)
        {
            var solution = Intersection(a, b);
            return solution.Count > 0;
        }

        public static bool HaveIntersection(Polygon a, Polygon b)
        {
            var solution = Intersection(a, b);
            return solution.Count > 0;
        }

        private static void _addRecursive(PolygonList hierarchicalList, ref PolygonList flatList)
        {
            foreach(Polygon polygon in hierarchicalList)
            {
                flatList.Add(polygon);
                _addRecursive(polygon.Holes, ref flatList);
            }
        }

        public static PolygonList Execute(PolygonList a, PolygonList b, ClipType clipType)
        {
            PolyTree solution = new PolyTree();

            Clipper clipper = new Clipper();

            PolygonList a_flat = new PolygonList();
            _addRecursive(a, ref a_flat);
            PolygonList b_flat = new PolygonList();
            _addRecursive(b, ref b_flat);

            clipper.AddPaths(a_flat, PolyType.ptSubject, true);
            clipper.AddPaths(b_flat, PolyType.ptClip, true);
            clipper.Execute(clipType, solution);

            PolygonList solutionPolygons = new PolygonList();

            foreach(PolyNode node in solution.Childs)
            {
                Polygon polygon = new Polygon(node);
                solutionPolygons.AddRange(polygon.Flatten());
            }

            return solutionPolygons;
        }

        #region Intersection

        public static PolygonList Intersection(PolygonList a, PolygonList b)
        {
            return Execute(a, b, ClipType.ctIntersection);
        }

        public static PolygonList Intersection(PolygonList a, Polygon b)
        {
            return Intersection(a, new PolygonList { b });
        }

        public static PolygonList Intersection(Polygon a, PolygonList b)
        {
            return Intersection(b, a);
        }

        public static PolygonList Intersection(Polygon a, Polygon b)
        {
            return Intersection(new PolygonList { a }, new PolygonList { b });
        }

        #endregion

        #region Difference

        public static PolygonList Difference(PolygonList a, PolygonList b)
        {
            return Execute(a, b, ClipType.ctDifference);
        }

        public static PolygonList Difference(Polygon a, Polygon b)
        {
            return Difference(new PolygonList { a }, new PolygonList { b });
        }

        #endregion

        #region Union
        public static PolygonList Union(PolygonList a, PolygonList b)
        {
            return Execute(a, b, ClipType.ctUnion);
        }
        #endregion

        // todo clearly define what equal means: cm difference in height+width maximal
        public static bool EqualWithinEpsilon(Polygon a, Polygon b, double epsilon=(.01 * Vector.Scale * .01 * Vector.Scale))
        {
            PolygonList intersections = Intersection(a, b);
            if (intersections.Count != 1) return false;
            
            double intersectionArea = Clipper.Area(intersections[0]);

            return (Math.Abs(intersectionArea - Clipper.Area(a)) < epsilon) &&
                (Math.Abs(intersectionArea - Clipper.Area(b)) < epsilon);
        }

        public static bool ContainsWithinEpsilon(Polygon larger, Polygon smaller, double epsilon = (.01 * Vector.Scale * .01 * Vector.Scale))
        {
            PolygonList intersections = Intersection(larger, smaller);
            if (intersections.Count != 1) return false; // @lukas should it be possible to have multiple?
            
            return EqualWithinEpsilon(smaller, intersections[0], epsilon);
        }

        public static bool ContainsWithinEpsilon(PolygonList larger, Polygon smaller, double epsilon = (.01 * Vector.Scale * .01 * Vector.Scale))
        {
            PolygonList intersections = Intersection(larger, smaller);
            if (intersections.Count != 1) return false; // @lukas should it be possible to have multiple?

            return EqualWithinEpsilon(smaller, intersections[0], epsilon);
        }

        public static double GetArea(Polygon polygon)
        {
            return Clipper.Area(polygon) / (Vector.Scale * Vector.Scale);
        }

        public static double GetArea(PolygonList polygonList)
        {
            double totalArea = 0;
            polygonList.ForEach(polygon => totalArea += GetArea(polygon));
            return totalArea;
        }

        public static double ContainsRelative(Polygon larger, Polygon smaller)
        {
            PolygonList intersections = Intersection(larger, smaller);

            double sharedArea = 0;
            foreach (Polygon intersection in intersections)
            {
                sharedArea += GetArea(intersection);
            }

            return sharedArea / GetArea(smaller);
        }

        public static double ContainsRelative(PolygonList larger, Polygon smaller)
        {
            PolygonList intersections = Intersection(larger, smaller);

            double sharedArea = 0;
            foreach (Polygon intersection in intersections)
            {
                sharedArea += GetArea(intersection);
            }

            return sharedArea / GetArea(smaller);
        }


        private static Polygon _totalAssumedPolygon = null;

        private static Polygon TotalAssumedArea
        {
            get
            {
                if (_totalAssumedPolygon == null)
                {
                    _totalAssumedPolygon = Config.PlayArea;
                }
                return _totalAssumedPolygon;
            }
        }

        public static PolygonList OffsetPolygonForSafety(PolygonList poly, float absoluteDistance,
            JoinType joinType = JoinType.jtMiter,
            EndType endType = EndType.etClosedPolygon,
            bool pretty = false)
        {
            //PolygonList contrast = Difference(TotalAssumedArea, poly);

            PolygonList contrast = Difference(TotalAssumedArea, poly);

            Func<Vector, bool> boundaryNotEdge = point => Math.Abs(point.X) > 1.999999 && Math.Abs(point.Z) < 1.999999 || Math.Abs(point.X) < 1.999999 && Math.Abs(point.Z) > 1.999999;
            Func<Vector, bool> notEdge = point => !(Math.Abs(point.X) < 2.000001 && Math.Abs(point.X) > 1.999999 && Math.Abs(point.Z) < 2.000001 && Math.Abs(point.Z) > 1.999999);
            if (pretty)
            {
                foreach (Polygon area in contrast)
                {
                    // check if point is on bounds
                    var numPoints = area.Points.Count;
                    int numOnBoundaryNotEdge = 0;
                    int numOnBoundaryNotEdgeNeighbor = 0;
                    for (int i = 0; i < numPoints; i++)
                    {
                        var point = area.Points[i];
                        var isOnBoundaryNotEdge = boundaryNotEdge(point);
                        if (isOnBoundaryNotEdge)
                        {
                            // check neighbors
                            numOnBoundaryNotEdge++;
                            var leftIndex = (i - 1 + numPoints) % numPoints;
                            var rightIndex = (i + 1) % numPoints;
                            var leftPoint = area.Points[leftIndex];
                            var rightPoint = area.Points[rightIndex];
                            // check if points are on bounds
                            Vector extendTowards = Vector.Zero;
                            if (notEdge(leftPoint))
                            {
                                extendTowards = point - leftPoint;
                                numOnBoundaryNotEdgeNeighbor++;
                            } else if (notEdge(rightPoint)) {
                                extendTowards = point - rightPoint;
                                numOnBoundaryNotEdgeNeighbor++;
                            } else {
                                //throw new Exception("This probably shouldn't happen");
                            }

                            area.Points[i] += 3 * extendTowards;
                        } 
                    }
                    
                    //if (numOnBoundaryNotEdge != numOnBoundaryNotEdgeNeighbor)
                    //{
                    //    throw new ArgumentException();
                    //}
                }
            }

            contrast = OffsetPolygon(contrast, -absoluteDistance);
            PolygonList solution = Difference(poly, contrast);

            return solution;

            //List<List<IntPoint>> finalSolution;
            //{
            //    Clipper c = new Clipper();
            //    c.AddPath(TotalAssumedArea, PolyType.ptSubject, true);
            //    c.AddPaths(poly, PolyType.ptClip, true);
            //    finalSolution = new List<List<IntPoint>>();
            //    c.Execute(ClipType.ctDifference, finalSolution);
            //}

            //{
            //    Clipper c = new Clipper();
            //    c.AddPaths(finalSolution, PolyType.ptSubject, true);
            //    finalSolution = new List<List<IntPoint>>();
            //    c.Execute(ClipType.ctUnion, finalSolution, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
            //}
            //contrast = OffsetPolygon(contrast, -absoluteDistance);
            //PolygonList solution = Difference(poly, contrast);
            //solution = OffsetPolygon(solution, -.01f);
            //solution = Union(solution, solution);

            //PolygonList unified = new PolygonList();
            //foreach (Polygon polygon in solution)
            //{
            //    unified = Union(polygon, unified);
            //}

            //solution = Union(TotalAssumedArea, solution);

            //Clipper clipper = new Clipper();

            //clipper.AddPaths(solution, PolyType.ptSubject, true);

            ////PolyTree finalSolution = new PolyTree();
            //List<List<IntPoint>> finalSolution = new List<List<IntPoint>>();
            //clipper.Execute(ClipType.ctUnion, finalSolution, PolyFillType.pftNonZero, PolyFillType.pftNonZero);

            //return finalSolution;
        }

        public static PolygonList OffsetPolygon(PolygonList poly, float absoluteDistance,
            JoinType joinType = JoinType.jtMiter,
            EndType endType = EndType.etClosedPolygon)
        {
            ClipperOffset offset = new ClipperOffset();
            offset.AddPaths(poly, joinType, endType);

            List<List<IntPoint>> solution = new List<List<IntPoint>>();
            offset.Execute(ref solution, absoluteDistance * Vector.Scale);

            return solution;
        }
    }
}

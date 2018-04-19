using System;

namespace VirtualSpace.Shared
{
    public static class GeometryHelper
    {
        public static void DistancePointToPolygonInside(Polygon polygon, Vector point, out double minDist, out double maxDist)
        {
            var multiplier = PointInPolygon(polygon, point) ? 1 : -1;

            var numPoints = polygon.Points.Count;
            minDist = double.MaxValue;
            maxDist = double.MinValue;
            for (int i = 0, j = numPoints - 1; i < numPoints; j = i++)
            {
                var lineVector = polygon.Points[j] - polygon.Points[i];
                var lineNormal = lineVector.Normal1;
                var intersectionPoint = Intersection(polygon.Points[i], polygon.Points[j], point, point + lineNormal);

                //if (intersectionPoint == null)
                //{
                //    Logger.Debug("Intersection point was not found " +
                //        polygon.Points[i] + " " + polygon.Points[j] + " " + point + " " + (point + lineNormal));
                //    continue;
                //}

                intersectionPoint = polygon.Points[i];

                var distance = intersectionPoint.Distance(point);
                if (distance < minDist) minDist = distance;
                if (maxDist < distance) maxDist = distance;
            }
            minDist *= multiplier;
        }

        // https://github.com/substack/point-in-polygon/blob/master/index.js
        public static bool PointInPolygon(Polygon polygon, Vector point)
        {
            var x = point.X;
            var y = point.Z;

            var numPoints = polygon.Points.Count;

            var inside = false;
            for (int i = 0, j = numPoints - 1; i < numPoints; j = i++)
            {
                var xi = polygon.Points[i].X;
                var yi = polygon.Points[i].Z;
                var xj = polygon.Points[j].X;
                var yj = polygon.Points[j].Z;

                var intersect = ((yi > y) != (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi) + xi);
                if (intersect) inside = !inside;
            }

            return inside;
        }

        public static Vector Intersection(Vector l1p1, Vector l1p2, Vector l2p1, Vector l2p2)
        {
            double denominator = (l2p2.Z - l2p1.Z) * (l1p2.X - l1p1.X) - (l2p2.X - l2p1.X) * (l1p2.Z - l1p1.Z);

            if (Math.Abs(denominator) < 10e-6) { return null; }

            double ua = ((l2p2.X - l2p1.X) * (l1p1.Z - l2p1.Z) - (l2p2.Z - l2p1.Z) * (l1p1.X - l2p1.X)) / denominator;
            //double ub = ((l2.X - l1.X) * (l1.Z - l3.Z) - (l2.Z - l1.Z) * (l1.X - l3.X)) / denominator;

            double x = l1p1.X + ua * (l1p2.X - l1p1.X);
            double z = l1p1.Z + ua * (l1p2.Z - l1p1.Z);
            
            return new Vector(x, z);
        }
    }
}

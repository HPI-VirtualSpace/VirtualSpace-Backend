using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using VirtualSpace.Shared;

namespace SharedTests
{
    [TestClass]
    public class SpatialClassesTests
    {
        [TestMethod]
        public void PolygonRotation90Degrees()
        {
            Polygon expectedPolygon = Polygon.AsRectangle(new Vector(2, 2), new Vector(-1, -1));
            Polygon actualPolygon = expectedPolygon.DeepClone();
            actualPolygon.RotateCounter(90 * Math.PI / 180);

            CollectionAssert.AreNotEqual(expectedPolygon.Points, actualPolygon.Points);
            Assert.IsTrue(ClipperUtility.EqualWithinEpsilon(expectedPolygon, actualPolygon));
        }

        [TestMethod]
        public void PolygonVectorAddition()
        {
            Polygon polygon = Polygon.AsRectangle(new Vector(3, 4));
            Vector offset = new Vector(1, 1);
            polygon += offset;

            List<IntPoint> expectedCoordinates = new List<IntPoint>(
                new IntPoint[] { new Vector(1, 1), new Vector(4, 1), new Vector(4, 5), new Vector(1, 5) });

            Assert.IsTrue(ClipperUtility.EqualWithinEpsilon(polygon, expectedCoordinates));
        }

        [TestMethod]
        public void PolygonCircumferenceTest()
        {
            Polygon polygon = Polygon.AsRectangle(new Vector(3, 4));
            Assert.AreEqual(14, polygon.Circumference);
        }

        [TestMethod]
        public void PolygonAsCircleUnitCircleTest()
        {
            Polygon circle = Polygon.AsCircle(1, new Vector(0, 0), 4);
            List<Vector> expectedPoints = new List<Vector>(new Vector[] { new Vector(1, 0), new Vector(0, 1), new Vector(-1, 0), new Vector(0, -1) });
            CollectionAssert.AreEqual(expectedPoints, circle.Points);
        }

        [TestMethod]
        public void PolygonAsCircleAreaTest()
        {
            Polygon circle = Polygon.AsCircle(4, new Vector(5, 5), 360);
            double area = ClipperUtility.GetArea(circle);
            double expectedArea = Math.PI * 4 * 4; // PI * r * r
            Assert.AreEqual(expectedArea, area, .01);
        }
    }
}

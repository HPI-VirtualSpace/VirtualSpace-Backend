using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VirtualSpace.Shared;

// for debugging

namespace SharedTests
{
    [TestClass]
    public class ClipperUtilityTests
    {
        [TestMethod]
        public void SimpleOverlapTest()
        {
            Polygon a = Polygon.AsRectangle(new Vector(2, 2));
            Polygon b = Polygon.AsRectangle(new Vector(2, 2), new Vector(1, 1));

            Polygon expectedPolygon = Polygon.AsRectangle(new Vector(1, 1), new Vector(1, 1));
            PolygonList resultIntersection = ClipperUtility.Intersection(a, b);

            Assert.AreEqual(1, resultIntersection.Count);
            Polygon resultPolygon = resultIntersection[0];
            Assert.IsTrue(ClipperUtility.HaveIntersection(a, b));
            Assert.IsTrue(ClipperUtility.EqualWithinEpsilon(expectedPolygon, resultPolygon));
        }

        [TestMethod]
        public void ObviouslyEqualTest()
        {
            Polygon a = Polygon.AsRectangle(new Vector(1000, 1000));
            Polygon b = Polygon.AsRectangle(new Vector(1000, 1000));
            Assert.IsTrue(ClipperUtility.EqualWithinEpsilon(a, b));
        }

        [TestMethod]
        public void EqualWithinEpsilonTest()
        {
            Polygon a = Polygon.AsRectangle(new Vector(1000, 1000));
            Polygon b = Polygon.AsRectangle(new Vector(1000.000001, 1000.000001));
            Assert.IsTrue(ClipperUtility.EqualWithinEpsilon(a, b));
        }

        [TestMethod]
        public void NotEqualWithinEpsilonTest()
        {
            Polygon a = Polygon.AsRectangle(new Vector(1000, 1000));
            Polygon b = Polygon.AsRectangle(new Vector(2000, 3000));
            Assert.IsFalse(ClipperUtility.EqualWithinEpsilon(a, b));
        }

        [TestMethod]
        public void EqualWithinHighEpsilonTest()
        {
            Polygon a = Polygon.AsRectangle(new Vector(10, 10));
            Polygon b = Polygon.AsRectangle(new Vector(10, 10), new Vector(1, 1));
            Assert.IsFalse(ClipperUtility.EqualWithinEpsilon(a, b));
            Assert.IsTrue(ClipperUtility.EqualWithinEpsilon(a, b, epsilon: 15 * Vector.Scale * 15 * Vector.Scale));
        }

        [TestMethod]
        public void GetAreaTest()
        {
            Polygon polygon = Polygon.AsRectangle(new Vector(10, 100));
            double expectedArea = 10 * 100;
            double resultArea = ClipperUtility.GetArea(polygon);
            Assert.AreEqual(expectedArea, resultArea, .001);
        }

        [TestMethod]
        public void ContainsFullyTest()
        {
            Polygon a = Polygon.AsRectangle(new Vector(10, 10));
            Polygon b = Polygon.AsRectangle(new Vector(1, 1), new Vector(1, 1));
            Assert.IsTrue(ClipperUtility.ContainsWithinEpsilon(a, b));
            Assert.AreEqual(1, ClipperUtility.ContainsRelative(a, b), .001);
        }

        [TestMethod]
        public void OffsetPolygonByHalfTest()
        {
            Polygon input = Polygon.AsRectangle(new Vector(10, 10));
            Polygon expectedOutput = Polygon.AsRectangle(new Vector(5, 5), new Vector(2.5, 2.5));

            PolygonList output = ClipperUtility.OffsetPolygon(input, -2.5f);

            Assert.AreEqual(1, output.Count);

            Polygon actualOutput = output[0];

            Assert.IsTrue(ClipperUtility.EqualWithinEpsilon(expectedOutput, actualOutput));
        }

        [TestMethod]
        public void LineIntersectionTest()
        {
            Polygon line = Polygon.AsLine(Vector.Zero, Vector.One);
            Polygon rectangle = Polygon.AsRectangle(Vector.One);

            PolygonList output = ClipperUtility.Intersection(line, rectangle);

            Assert.AreEqual(1, output.Count);

            Polygon actualOutput = output[0];

            Assert.IsTrue(ClipperUtility.EqualWithinEpsilon(line, actualOutput, 
                epsilon: 15 * Vector.Scale * 15 * Vector.Scale));
        }

        private static PolygonList GetSingleResultPolygonList(Polygon resultPolygon)
        {
            PolygonList result = new PolygonList();
            result.Add(resultPolygon);
            return result;
        }

        
    }
}

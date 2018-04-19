using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VirtualSpace.Shared;

namespace BackendTests
{
    [TestClass]
    public class VectorTests
    {
        [TestMethod]
        public void RotateCC90AroundDefault()
        {
            var vec = new Vector(1, 0);
            var exp = new Vector(0, 1);
            var res = vec.RotateCounter(Math.PI / 2);

            Assert.AreEqual(exp, res);
        }

        [TestMethod]
        public void Rotate90AroundDefault()
        {
            var vec = new Vector(1, 0);
            var exp = new Vector(0, -1);
            var res = vec.Rotate(Math.PI / 2);

            Assert.AreEqual(exp, res);
        }
    }
}

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VirtualSpace.Backend;
using VirtualSpace;
using VirtualSpace.Shared;

namespace BackendTests
{
    [TestClass]
    public class PlayerDataTests
    {
        [TestMethod]
        public void ExtrapolationTest()
        {
            PlayerDataEntry player = new PlayerDataEntry("testUser");

            Vector defaultOrientation = Vector.One;

            player.UpdateStatus(0, Vector.Zero, defaultOrientation);
            Assert.AreEqual(Vector.Zero, player.ExtrapolateMovement(0f));
            Assert.AreEqual(Vector.Zero, player.ExtrapolateMovement(1f));
            player.UpdateStatus(1, Vector.One, defaultOrientation);
            Assert.AreEqual(Vector.One, player.ExtrapolateMovement(0f));
            Assert.AreEqual(2 * Vector.One, player.ExtrapolateMovement(1f));
            Assert.AreEqual(5 * Vector.One, player.ExtrapolateMovement(4f));
            player.UpdateStatus(2, new Vector(2, 2), defaultOrientation);
            Assert.AreEqual(2 * Vector.One, player.ExtrapolateMovement(0f));
            Assert.AreEqual(5 * Vector.One, player.ExtrapolateMovement(3f));
        }
    }
}

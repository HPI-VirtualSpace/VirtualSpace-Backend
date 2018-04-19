using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VirtualSpace.Shared;

namespace SharedTests
{
    [TestClass]
    public class SubtypeUtilityTests
    {
        class Super { }
        class Derived : Super { }
        class DerivedOfDerived : Derived { }

        [TestMethod]
        public void GetTypeOfSubtypeWithSimpleInheritance()
        {
            var derived = new Derived();

            Assert.AreEqual(derived.GetType().Name, "Derived");
            Assert.AreEqual(derived.GetType(), SubtypeUtility.GetTypeOfSubtype((Super)derived));
        }

        [TestMethod]
        public void GetTypeOfSubtypeWithSimpleInheritanceInDifferentVariables()
        {
            var derived = new Derived();
            var super = (Super)derived;

            Assert.AreEqual(derived.GetType().Name, "Derived");
            Assert.AreEqual(derived.GetType(), SubtypeUtility.GetTypeOfSubtype(super));
        }

        [TestMethod]
        public void GetTypeOfSubtypeWithDoubleInheritance()
        {
            var derived = new DerivedOfDerived();
            
            Assert.AreEqual(derived.GetType().Name, "DerivedOfDerived");
            Assert.AreEqual(derived.GetType(), SubtypeUtility.GetTypeOfSubtype((Super)derived));
            Assert.AreEqual(derived.GetType(), SubtypeUtility.GetTypeOfSubtype((Derived)derived));
        }

    }
}

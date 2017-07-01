using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Flexinets.Radius;

namespace RadiusServerTests
{
    [TestClass]
    public class FailedRequestBackOffCounterTests
    {
        [TestMethod]
        public void TestBackOffCounter1()
        {
            var foo = new FailedRequestBackOffCounter(0, DateTime.UtcNow);
            Assert.AreEqual(0, foo.Delay);
        }

        [TestMethod]
        public void TestBackOffCounter4()
        {
            var foo = new FailedRequestBackOffCounter(1, DateTime.UtcNow);
            Assert.AreEqual(0, foo.Delay);
        }

        [TestMethod]
        public void TestBackOffCounter5()
        {
            var foo = new FailedRequestBackOffCounter(2, DateTime.UtcNow);
            Assert.AreEqual(0, foo.Delay);
        }

        [TestMethod]
        public void TestBackOffCounter2()
        {
            var foo = new FailedRequestBackOffCounter(3, DateTime.UtcNow);
            Assert.AreEqual(1, foo.Delay);
        }

        [TestMethod]
        public void TestBackOffCounter7()
        {
            var foo = new FailedRequestBackOffCounter(4, DateTime.UtcNow);
            Assert.AreEqual(8, foo.Delay);
        }

        [TestMethod]
        public void TestBackOffCounter3()
        {
            var foo = new FailedRequestBackOffCounter(5000, DateTime.UtcNow);
            Assert.AreEqual(300, foo.Delay);
        }
    }
}

using Flexinets.Radius;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RadiusServerTests
{
    [TestClass]
    public class UsernameTests
    {
        [TestMethod]
        public void TestUsernameUsername()
        {
            var test = "test@example.com";
            var expected = "test";

            var usernamedomain = UsernameDomain.Parse(test);
            Assert.AreEqual(expected, usernamedomain.Username);
        }

        [TestMethod]
        public void TestUsernameDomain()
        {
            var test = "test@example.com";
            var expected = "example.com";

            var usernamedomain = UsernameDomain.Parse(test);
            Assert.AreEqual(expected, usernamedomain.Domain);
        }

        [TestMethod]
        public void TestUsernameToString()
        {
            var expected = "test@example.com";

            var usernamedomain = UsernameDomain.Parse(expected);
            Assert.AreEqual(expected, usernamedomain.ToString());
        }

        [TestMethod]
        public void TestUsernameToStringStripAdditional()
        {
            var username = "test@example.com@example.net";
            var expected = "test@example.net";

            var usernamedomain = UsernameDomain.Parse(username, true);
            Assert.AreEqual(expected, usernamedomain.ToString());
        }

        [TestMethod]
        public void TestUsernameToStringStripAdditional2()
        {
            var username = "test@example.net";
            var expected = "test@example.net";

            var usernamedomain = UsernameDomain.Parse(username, true);
            Assert.AreEqual(expected, usernamedomain.ToString());
        }
    }
}
using System.Linq;
using NUnit.Framework;

namespace Severnbli.LocalizationCodegen.Editor.Tests
{
    public class SmartStringParserTests
    {
        [Test]
        public void PositionalAndPlural_IgnoresInnerCurrentValue()
        {
            var result = SmartStringParser.Parse("{0}, you have {count:plural:one item|{} items}", "en");

            Assert.IsFalse(result.Failed, result.Error);
            Assert.AreEqual(2, result.Usages.Count);
            Assert.IsTrue(result.Usages.Any(u => u.IsPositional && u.Index == 0));
            var count = result.Usages.Single(u => !u.IsPositional);
            Assert.AreEqual("count", count.SourceName);
            Assert.AreEqual("plural", count.FormatterName);
        }

        [Test]
        public void PositionalFormat_UsesIndexOnly()
        {
            var result = SmartStringParser.Parse("{1:N2}", "en");

            Assert.IsFalse(result.Failed, result.Error);
            Assert.AreEqual(1, result.Usages.Count);
            Assert.IsTrue(result.Usages[0].IsPositional);
            Assert.AreEqual(1, result.Usages[0].Index);
        }

        [TestCase("{player-name}", "player-name")]
        [TestCase("{player.Name}", "player")]
        [TestCase("{count:plural:one|{} items}", "count")]
        [TestCase("{x:choose(a|b):he|she}", "x")]
        [TestCase("{items:list:{}|, }", "items")]
        public void NamedPlaceholders_UseTheFirstSelector(string format, string expected)
        {
            var result = SmartStringParser.Parse(format, "en");

            Assert.IsFalse(result.Failed, result.Error);
            Assert.AreEqual(expected, result.Usages.Single(u => !u.IsPositional).SourceName);
        }

        [Test]
        public void NestedPlaceholders_InsideFormatterAreExtracted()
        {
            var result = SmartStringParser.Parse(
                "{gender:choose(male|female):{name} is here|{name} is elsewhere}",
                "en");

            Assert.IsFalse(result.Failed, result.Error);
            Assert.IsTrue(result.Usages.Any(u => u.SourceName == "gender" && u.FormatterName == "choose"));
            Assert.AreEqual(2, result.Usages.Count(u => u.SourceName == "name"));
        }

        [TestCase("{{notvar}}")]
        [TestCase("\\{notvar\\}")]
        [TestCase("plain text")]
        public void EscapedBracesAndPlainText_ProduceNoVariables(string format)
        {
            var result = SmartStringParser.Parse(format, "en");
            Assert.IsFalse(result.Failed, result.Error);
            Assert.AreEqual(0, result.Usages.Count);
        }

        [Test]
        public void UnknownFormatter_IsReported()
        {
            var result = SmartStringParser.Parse("{value:notAFormatter:x}", "en");
            Assert.Contains("notAFormatter", result.UnknownFormatters);
        }

        [Test]
        public void UnparsableText_IsReported()
        {
            var result = SmartStringParser.Parse("{unclosed", "en");
            Assert.IsTrue(result.Failed);
        }
    }
}

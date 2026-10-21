using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Severnbli.LocalizationCodegen.Editor.Tests
{
    public class CodegenTextTests
    {
        [TestCase("", "_")]
        [TestCase("1abc", "_1abc")]
        [TestCase("a b", "a_b")]
        [TestCase("a  b", "a__b")]
        [TestCase("class", "@class")]
        [TestCase("@", "_")]
        [TestCase("héllo", "héllo")]
        [TestCase("player/name", "player_name")]
        public void ToIdentifier_MatchesLegacyBehaviour(string input, string expected)
        {
            Assert.AreEqual(expected, CodegenText.ToIdentifier(input));
        }

        [Test]
        public void ToIdentifier_HandlesSurrogatePairsInLegacyModeAsTwoUnits()
        {
            Assert.AreEqual("__", CodegenText.ToIdentifier("\U0001F600"));
        }

        [TestCase("a  b", "a_b")]
        [TestCase("a!!", "a")]
        [TestCase("!!!", "_")]
        [TestCase("", "_")]
        [TestCase("1abc", "_1abc")]
        [TestCase("class", "@class")]
        [TestCase("héllo", "héllo")]
        [TestCase("\U00020000", "\U00020000")]
        public void ToIdentifier_CompactCollapsesInvalidRuns(string input, string expected)
        {
            Assert.AreEqual(expected, CodegenText.ToIdentifier(input, true));
        }

        [Test]
        public void ToIdentifier_CompactKeepsLoneSurrogateAsUnderscore()
        {
            Assert.AreEqual("_", CodegenText.ToIdentifier("\uD800", true));
        }

        [Test]
        public void Literal_EscapesControlAndLineSeparatorCharacters()
        {
            Assert.AreEqual("\"a\\\"b\\\\c\\n\\r\\t\"", CodegenText.Literal("a\"b\\c\n\r\t"));
            Assert.AreEqual("\"\\u0085\"", CodegenText.Literal("\u0085"));
            Assert.AreEqual("\"\\u2028\"", CodegenText.Literal("\u2028"));
            Assert.AreEqual("\"\\u2029\"", CodegenText.Literal("\u2029"));
            Assert.AreEqual("\"\\u007f\"", CodegenText.Literal("\u007F"));
            Assert.AreEqual("\"\\ud800\"", CodegenText.Literal("\uD800"));
        }

        [Test]
        public void Literal_KeepsValidSurrogatePair()
        {
            var literal = CodegenText.Literal("\U0001F600");
            Assert.AreEqual("\"" + "\U0001F600" + "\"", literal);
            Assert.IsFalse(literal.Contains("\\u"));
        }

        [Test]
        public void Literal_NeverContainsRawLineBreaks()
        {
            var literal = CodegenText.Literal("a\u0085b\u2028c\u2029d\ne\rf");
            Assert.IsFalse(literal.Contains("\n"));
            Assert.IsFalse(literal.Contains("\r"));
            Assert.IsFalse(literal.Contains("\u2028"));
            Assert.IsFalse(literal.Contains("\u2029"));
            Assert.IsFalse(literal.Contains("\u0085"));
        }

        [Test]
        public void SanitizeComment_ReplacesEveryCommentBreakingCharacter()
        {
            Assert.AreEqual(
                "a b c d e f",
                CodegenText.SanitizeComment("a\u2028b\u0085c\u2029d\ne\rf"));
        }

        [Test]
        public void SanitizeComment_ReplacesLoneSurrogates()
        {
            Assert.AreEqual("a b", CodegenText.SanitizeComment("a\uD800b"));
        }

        [Test]
        public void Unique_ClassNamesAreCaseInsensitive()
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ui" };
            Assert.AreEqual("UI_2", CodegenText.Unique("UI", used));
        }

        [TestCase("CON", true)]
        [TestCase("con", true)]
        [TestCase("NUL", true)]
        [TestCase("COM1", true)]
        [TestCase("LPT9", true)]
        [TestCase("Console", false)]
        [TestCase("Com10", false)]
        public void IsWindowsReservedFileName_DetectsDeviceNames(string name, bool expected)
        {
            Assert.AreEqual(expected, CodegenText.IsWindowsReservedFileName(name));
        }

        [Test]
        public void ToCamelIdentifier_HandlesSeparatorsAndKeywords()
        {
            Assert.AreEqual("playerName", CodegenText.ToCamelIdentifier("player-name"));
            Assert.AreEqual("playerName", CodegenText.ToCamelIdentifier("player.Name"));
            Assert.AreEqual("@event", CodegenText.ToCamelIdentifier("event"));
        }

        [Test]
        public void ToPascalCase_SplitsWords()
        {
            Assert.AreEqual("PlayerName", CodegenText.ToPascalCase("player name"));
            Assert.AreEqual("MenuItem", CodegenText.ToPascalCase("menu-item"));
        }
    }
}

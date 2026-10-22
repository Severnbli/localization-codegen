using NUnit.Framework;

namespace Severnbli.LocalizationCodegen.Editor.Tests
{
    public class NamespaceAndPathTests
    {
        [Test]
        public void Normalize_TreatsNullEmptyAndWhitespaceAsGlobal()
        {
            Assert.IsNull(NamespaceUtil.Normalize(null));
            Assert.IsNull(NamespaceUtil.Normalize(""));
            Assert.IsNull(NamespaceUtil.Normalize("   "));
            Assert.IsNull(NamespaceUtil.Normalize(". ."));
        }

        [TestCase("A.B", "A.B")]
        [TestCase("A..B", "A.B")]
        [TestCase("namespace.x", "@namespace.x")]
        [TestCase("1A", "_1A")]
        [TestCase(" A . B ", "A.B")]
        public void Normalize_SanitizesSegments(string input, string expected)
        {
            Assert.AreEqual(expected, NamespaceUtil.Normalize(input));
        }

        [TestCase(null, true)]
        [TestCase("", true)]
        [TestCase("   ", true)]
        [TestCase("A", true)]
        [TestCase("A.B", true)]
        [TestCase("A..B", false)]
        [TestCase("1A", false)]
        [TestCase("A-B", false)]
        public void IsValid_OnlyAcceptsTypedNamespaces(string input, bool expected)
        {
            Assert.AreEqual(expected, NamespaceUtil.IsValid(input));
        }

        [TestCase("Assets", "Assets", true)]
        [TestCase("Assets/", "Assets", true)]
        [TestCase("Assets\\Gen", "Assets/Gen", true)]
        [TestCase("Assets/Gen/", "Assets/Gen", true)]
        [TestCase("AssetsX", null, false)]
        [TestCase("Assets2/x", null, false)]
        [TestCase("Assets/../x", null, false)]
        [TestCase("Assets//a", null, false)]
        [TestCase("Assets/./a", null, false)]
        [TestCase("Assets/a:b", null, false)]
        [TestCase("", null, false)]
        [TestCase("   ", null, false)]
        public void TryNormalize_ValidatesAndNormalizes(string input, string expected, bool expectedSuccess)
        {
            string folder;
            string error;
            var success = OutputPath.TryNormalize(input, out folder, out error);

            Assert.AreEqual(expectedSuccess, success, error);
            Assert.AreEqual(expectedSuccess ? expected : null, folder);
            if (expectedSuccess)
                Assert.IsNull(error);
            else
                Assert.IsNotEmpty(error);
        }

        [Test]
        public void HasEditorSegment_IsCaseInsensitive()
        {
            Assert.IsTrue(OutputPath.HasEditorSegment("Assets/Editor/Gen"));
            Assert.IsTrue(OutputPath.HasEditorSegment("Assets/editor/Gen"));
            Assert.IsFalse(OutputPath.HasEditorSegment("Assets/Generated"));
        }

        [Test]
        public void NormalizeNewLines_UnifiesCrLf()
        {
            Assert.AreEqual("a\nb\n", OutputPath.NormalizeNewLines("a\r\nb\r"));
        }
    }
}

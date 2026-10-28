using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Severnbli.LocalizationCodegen.Editor.Tests
{
    public class SmartVariableCatalogTests
    {
        static List<SmartVariableInfo> Collect(params (string Locale, string Text)[] texts)
        {
            return SmartVariableCatalog.Collect(
                texts.Select(t => new KeyValuePair<string, string>(t.Locale, t.Text)),
                new HashSet<string> { "global" });
        }

        [Test]
        public void Collect_MergesLocalesAndOrdersPositionalFirst()
        {
            var variables = Collect(
                ("en", "{1}: {player} has {count:plural:one coin|{} coins}"),
                ("de", "{player} hat {count:plural:eine Münze|{} Münzen} {bonus}"));

            CollectionAssert.AreEqual(new[] { "0", "1", "bonus", "count", "player" }, variables.Select(v => v.Name));

            var gap = variables[0];
            Assert.IsTrue(gap.IsPositional);
            CollectionAssert.IsEmpty(gap.Locales, "A positional gap is listed, since the method has a parameter for it.");

            CollectionAssert.AreEqual(new[] { "de" }, variables.Single(v => v.Name == "bonus").Locales);
            CollectionAssert.AreEqual(new[] { "de", "en" }, variables.Single(v => v.Name == "count").Locales);
        }

        [Test]
        public void Collect_SkipsGlobalVariables()
        {
            var variables = Collect(("en", "{global.score} {name}"), ("de", "no placeholders"));

            CollectionAssert.AreEqual(new[] { "name" }, variables.Select(v => v.Name));
        }

        [Test]
        public void InferredType_MatchesInferredMode()
        {
            var variables = Collect(
                ("en", "{count:plural:a|b} {items:list:{}|, } {name} {mixed:plural:a|b}"),
                ("de", "{mixed:list:{}|, }"));

            Assert.AreEqual(SmartParameterType.Int, variables.Single(v => v.Name == "count").InferredType);
            Assert.AreEqual(SmartParameterType.List, variables.Single(v => v.Name == "items").InferredType);
            Assert.AreEqual(SmartParameterType.Object, variables.Single(v => v.Name == "name").InferredType);
            Assert.AreEqual(SmartParameterType.Object, variables.Single(v => v.Name == "mixed").InferredType);
        }

        [Test]
        public void Label_ShowsFormattersAndLocales()
        {
            var variables = Collect(("en", "{0} {count:plural:a|b}"), ("ru", "{count:plural:a|b|c}"));

            Assert.AreEqual("{0}  (en)", variables.Single(v => v.Name == "0").Label);
            Assert.AreEqual("count  (plural; en, ru)", variables.Single(v => v.Name == "count").Label);
        }

        [Test]
        public void Label_ReplacesSlashes()
        {
            var variables = Collect(("en", "{0}"), ("pt/BR", "{0}"));

            StringAssert.DoesNotContain("/", variables[0].Label);
        }

        [Test]
        public void Missing_SkipsNamesThatHaveARow()
        {
            var variables = Collect(("en", "{0} {count} {player}"));

            var missing = SmartVariableCatalog.Missing(variables, new[] { "{0}", " player " });

            CollectionAssert.AreEqual(new[] { "count" }, missing.Select(v => v.Name));
        }

        [Test]
        public void Find_AcceptsBracedIndex()
        {
            var variables = Collect(("en", "{0}"));

            Assert.AreSame(variables[0], SmartVariableCatalog.Find(variables, "{0}"));
            Assert.IsNull(SmartVariableCatalog.Find(variables, "1"));
        }

        [Test]
        public void Problem_ReportsWhatGenerationWouldWarnAbout()
        {
            var variables = Collect(("en", "{count:plural:a|b} {items:list:{}|, } {name}"));
            var count = variables.Single(v => v.Name == "count");
            var items = variables.Single(v => v.Name == "items");
            var name = variables.Single(v => v.Name == "name");

            Assert.IsNull(SmartVariableCatalog.Problem("count", SmartParameterType.Int, count, false));
            Assert.IsNull(SmartVariableCatalog.Problem("items", SmartParameterType.List, items, false));
            Assert.IsNull(SmartVariableCatalog.Problem("name", SmartParameterType.String, name, false));

            StringAssert.Contains("plural", SmartVariableCatalog.Problem("count", SmartParameterType.String, count, false));
            StringAssert.Contains("list", SmartVariableCatalog.Problem("items", SmartParameterType.Int, items, false));
            StringAssert.Contains("no such variable", SmartVariableCatalog.Problem("score", SmartParameterType.Int, null, false));
            StringAssert.Contains("more than once", SmartVariableCatalog.Problem("name", SmartParameterType.String, name, true));
            StringAssert.Contains("Pick a variable", SmartVariableCatalog.Problem(" ", SmartParameterType.Int, null, false));
        }
    }
}

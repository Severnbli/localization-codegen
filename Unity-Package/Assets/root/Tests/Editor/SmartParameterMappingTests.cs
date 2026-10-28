using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Severnbli.LocalizationCodegen.Editor.Tests
{
    public class SmartParameterMappingTests
    {
        static EntryModel Entry()
        {
            // "{0} {player} has {count:plural:one coin|{} coins}: {items:list:{}|, }"
            var entry = new EntryModel { Id = 1, Key = "Coins" };
            entry.Params.Add(new SmartParam { IsPositional = true, Index = 0, SourceName = "0", ClrName = "arg0", TypeName = "object" });
            entry.Params.Add(new SmartParam { SourceName = "count", ClrName = "count", TypeName = "int", FormatterName = "plural" });
            entry.Params.Add(new SmartParam
            {
                SourceName = "items", ClrName = "items", TypeName = "System.Collections.IEnumerable", FormatterName = "list"
            });
            entry.Params.Add(new SmartParam { SourceName = "player", ClrName = "player", TypeName = "object" });

            foreach (var locale in new[] { "en", "de" })
            {
                entry.Usages.Add(new SmartVariableUsage { Locale = locale, IsPositional = true, Index = 0 });
                entry.Usages.Add(new SmartVariableUsage { Locale = locale, SourceName = "player" });
                entry.Usages.Add(new SmartVariableUsage { Locale = locale, SourceName = "count", FormatterName = "plural" });
                entry.Usages.Add(new SmartVariableUsage { Locale = locale, SourceName = "items", FormatterName = "list" });
            }
            return entry;
        }

        static List<string> Apply(EntryModel entry, params SmartParameterTypes.Parameter[] hints)
        {
            var warnings = new List<string>();
            SmartParameterMapping.Apply(entry, hints.ToList(), warnings, "Ui", "Coins");
            return warnings;
        }

        static string TypeOf(EntryModel entry, string sourceName) =>
            entry.Params.Single(p => p.SourceName == sourceName).TypeName;

        static SmartParameterTypes.Parameter Hint(string name, SmartParameterType type) =>
            new SmartParameterTypes.Parameter(name, type);

        [Test]
        public void Hints_SetNamedAndPositionalTypes()
        {
            var entry = Entry();
            var warnings = Apply(entry,
                Hint("player", SmartParameterType.String),
                Hint("count", SmartParameterType.Long),
                Hint("0", SmartParameterType.DateTime));

            CollectionAssert.IsEmpty(warnings);
            Assert.AreEqual("string", TypeOf(entry, "player"));
            Assert.AreEqual("long", TypeOf(entry, "count"));
            Assert.AreEqual("System.DateTime", TypeOf(entry, "0"));
        }

        [Test]
        public void ParametersWithoutHint_KeepTheInferredType()
        {
            var entry = Entry();
            Apply(entry, Hint("player", SmartParameterType.String));

            Assert.AreEqual("int", TypeOf(entry, "count"));
            Assert.AreEqual("System.Collections.IEnumerable", TypeOf(entry, "items"));
            Assert.AreEqual("object", TypeOf(entry, "0"));
        }

        [Test]
        public void NoHints_ChangeNothing()
        {
            var entry = Entry();
            var warnings = new List<string>();
            SmartParameterMapping.Apply(entry, null, warnings, "Ui", "Coins");

            CollectionAssert.IsEmpty(warnings);
            Assert.AreEqual("object", TypeOf(entry, "player"));
            Assert.AreEqual("int", TypeOf(entry, "count"));
        }

        [Test]
        public void BracedIndex_MatchesPositionalParameter()
        {
            var entry = Entry();
            var warnings = Apply(entry, Hint(" {0} ", SmartParameterType.Int));

            CollectionAssert.IsEmpty(warnings);
            Assert.AreEqual("int", TypeOf(entry, "0"));
        }

        [TestCase(SmartParameterType.String)]
        [TestCase(SmartParameterType.Bool)]
        [TestCase(SmartParameterType.Char)]
        [TestCase(SmartParameterType.DateTime)]
        [TestCase(SmartParameterType.Object)]
        public void PluralWithWrongType_Warns(SmartParameterType type)
        {
            var entry = Entry();
            var warnings = Apply(entry, Hint("count", type));

            Assert.AreEqual(1, warnings.Count, string.Join("\n", warnings));
            StringAssert.Contains($"variable 'count' is declared as {type} but used with plural", warnings[0]);
            StringAssert.StartsWith("Collection 'Ui', key 'Coins': ", warnings[0]);
            // The declared type is still used.
            Assert.AreEqual(SmartParameterMapping.TypeName(type), TypeOf(entry, "count"));
        }

        [TestCase(SmartParameterType.Int)]
        [TestCase(SmartParameterType.Long)]
        [TestCase(SmartParameterType.Float)]
        [TestCase(SmartParameterType.Double)]
        [TestCase(SmartParameterType.Decimal)]
        [TestCase(SmartParameterType.List)]
        public void PluralWithNumberOrCollection_DoesNotWarn(SmartParameterType type)
        {
            CollectionAssert.IsEmpty(Apply(Entry(), Hint("count", type)));
        }

        [Test]
        public void PluralShortName_IsChecked()
        {
            var entry = Entry();
            foreach (var usage in entry.Usages.Where(u => u.SourceName == "count"))
                usage.FormatterName = "p";

            var warnings = Apply(entry, Hint("count", SmartParameterType.String));
            Assert.AreEqual(1, warnings.Count, string.Join("\n", warnings));
            StringAssert.Contains("used with plural", warnings[0]);
        }

        [Test]
        public void ListWithNonCollectionType_Warns()
        {
            var warnings = Apply(Entry(), Hint("items", SmartParameterType.String));

            Assert.AreEqual(1, warnings.Count, string.Join("\n", warnings));
            StringAssert.Contains("variable 'items' is declared as String but used with list", warnings[0]);
        }

        [Test]
        public void UnknownName_Warns()
        {
            var entry = Entry();
            var warnings = Apply(entry, Hint("score", SmartParameterType.Int));

            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains("lists 'score', which is not a parameter of this entry", warnings[0]);
        }

        [Test]
        public void DuplicateName_WarnsAndKeepsTheFirstType()
        {
            var entry = Entry();
            var warnings = Apply(entry,
                Hint("player", SmartParameterType.String),
                Hint("player", SmartParameterType.Int));

            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains("lists 'player' more than once", warnings[0]);
            Assert.AreEqual("string", TypeOf(entry, "player"));
        }

        [Test]
        public void EmptyName_Warns()
        {
            var warnings = Apply(Entry(), Hint("  ", SmartParameterType.Int));

            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains("parameter without a name", warnings[0]);
        }

        [Test]
        public void EveryType_HasATypeName()
        {
            foreach (SmartParameterType type in System.Enum.GetValues(typeof(SmartParameterType)))
                Assert.IsFalse(string.IsNullOrEmpty(SmartParameterMapping.TypeName(type)), type.ToString());
        }
    }
}

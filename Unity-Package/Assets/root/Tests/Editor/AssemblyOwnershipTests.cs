using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Severnbli.LocalizationCodegen.Editor.Tests
{
    public class AssemblyOwnershipTests
    {
        // Unity ignores folders ending with '~', so these asmdefs are never imported or compiled.
        const string Root = "Assets/__LocalizationCodegenAsmdefTests~";

        static readonly string[] s_Required =
        {
            AssemblyOwnership.RuntimeAssemblyName,
            AssemblyOwnership.LocalizationAssemblyName
        };

        [Serializable]
        sealed class AsmdefJson
        {
            public string name;
            public string[] references;
            public bool autoReferenced;
        }

        [SetUp]
        public void SetUp()
        {
            DeleteRoot();
            Directory.CreateDirectory(Root);
        }

        [TearDown]
        public void TearDown()
        {
            DeleteRoot();
        }

        static void DeleteRoot()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }

        static string WriteFile(string path, string content, bool bom = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content, new UTF8Encoding(bom));
            return path;
        }

        static string RuntimeGuid()
        {
            var path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(AssemblyOwnership.RuntimeAssemblyName);
            Assert.IsNotNull(path, "The runtime asmdef must be resolvable.");
            return AssetDatabase.AssetPathToGUID(path);
        }

        // ---------------------------------------------------------------------------------------------------
        // Text editing
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void AddReferences_AppendsToAMultiLineArray()
        {
            const string json =
                "{\n    \"name\": \"Game\",\n    \"references\": [\n        \"Unity.TextMeshPro\"\n    ],\n    \"autoReferenced\": true\n}\n";

            var updated = AssemblyOwnership.AddReferences(json, new[] { "A", "B" });

            Assert.AreEqual(
                "{\n    \"name\": \"Game\",\n    \"references\": [\n        \"Unity.TextMeshPro\",\n        \"A\",\n        \"B\"\n    ],\n    \"autoReferenced\": true\n}\n",
                updated);
        }

        [Test]
        public void AddReferences_FillsAnEmptyArray()
        {
            const string json = "{\n    \"name\": \"Game\",\n    \"references\": [],\n    \"autoReferenced\": true\n}\n";

            var updated = AssemblyOwnership.AddReferences(json, new[] { "A" });

            Assert.AreEqual(
                "{\n    \"name\": \"Game\",\n    \"references\": [\n        \"A\"\n    ],\n    \"autoReferenced\": true\n}\n",
                updated);
        }

        [Test]
        public void AddReferences_CreatesTheArrayWhenMissing()
        {
            const string json = "{\n    \"name\": \"Game\"\n}\n";

            var updated = AssemblyOwnership.AddReferences(json, new[] { "A" });

            Assert.AreEqual("{\n    \"references\": [\n        \"A\"\n    ],\n    \"name\": \"Game\"\n}\n", updated);
            var parsed = JsonUtility.FromJson<AsmdefJson>(updated);
            Assert.AreEqual("Game", parsed.name);
            CollectionAssert.AreEqual(new[] { "A" }, parsed.references);
        }

        [Test]
        public void AddReferences_KeepsASingleLineArrayOnOneLine()
        {
            const string json = "{ \"name\": \"Game\", \"references\": [\"X\"] }";

            Assert.AreEqual(
                "{ \"name\": \"Game\", \"references\": [\"X\", \"A\"] }",
                AssemblyOwnership.AddReferences(json, new[] { "A" }));
        }

        [Test]
        public void AddReferences_KeepsCrLfAndTabs()
        {
            const string json = "{\r\n\t\"name\": \"Game\",\r\n\t\"references\": []\r\n}";

            var updated = AssemblyOwnership.AddReferences(json, new[] { "A" });

            Assert.AreEqual("{\r\n\t\"name\": \"Game\",\r\n\t\"references\": [\r\n\t\t\"A\"\r\n\t]\r\n}", updated);
        }

        [Test]
        public void AddReferences_DoesNotTouchOtherArraysOrFields()
        {
            const string json =
                "{\n    \"name\": \"Game\",\n    \"references\": [\n        \"X\"\n    ],\n    \"includePlatforms\": [],\n" +
                "    \"versionDefines\": [\n        {\n            \"name\": \"com.unity.ugui\",\n            \"expression\": \"\",\n" +
                "            \"define\": \"HAS_UGUI\"\n        }\n    ]\n}\n";

            var updated = AssemblyOwnership.AddReferences(json, new[] { "A" });

            StringAssert.Contains("\"X\",\n        \"A\"\n    ],\n    \"includePlatforms\": [],", updated);
            StringAssert.Contains("\"name\": \"com.unity.ugui\"", updated);
            var parsed = JsonUtility.FromJson<AsmdefJson>(updated);
            Assert.AreEqual("Game", parsed.name);
            CollectionAssert.AreEqual(new[] { "X", "A" }, parsed.references);
        }

        // ---------------------------------------------------------------------------------------------------
        // Lookup
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void Find_ReturnsNull_WhenNoAsmdefOwnsTheFolder()
        {
            Assert.IsNull(AssemblyOwnership.Find(Root + "/Generated/Localization", s_Required));
        }

        [Test]
        public void Find_ReturnsTheNearestAsmdefAndItsMissingReferences()
        {
            WriteFile(Root + "/Game/Game.asmdef", "{ \"name\": \"Game\", \"references\": [\"Unity.Localization\"] }");
            WriteFile(Root + "/Game/Ui/Game.Ui.asmdef", "{ \"name\": \"Game.Ui\", \"references\": [] }");

            // The folder does not exist yet; the lookup still walks up from it.
            var owner = AssemblyOwnership.Find(Root + "/Game/Ui/Generated", s_Required);

            Assert.IsNotNull(owner);
            Assert.AreEqual("Game.Ui", owner.Name);
            Assert.AreEqual(Root + "/Game/Ui/Game.Ui.asmdef", owner.AsmdefPath);
            Assert.IsNull(owner.AsmrefPath);
            Assert.IsTrue(owner.IsEditable);
            CollectionAssert.AreEqual(s_Required, owner.MissingReferences);

            var outer = AssemblyOwnership.Find(Root + "/Game/Generated", s_Required);
            Assert.AreEqual("Game", outer.Name);
            CollectionAssert.AreEqual(new[] { AssemblyOwnership.RuntimeAssemblyName }, outer.MissingReferences);
        }

        [Test]
        public void Find_SkipsTheAsmdefWrittenByTheGenerator()
        {
            var generated = WriteFile(Root + "/Out/Localization.Generated.asmdef", "{ \"name\": \"Localization.Generated\" }");

            Assert.IsNull(AssemblyOwnership.Find(Root + "/Out", s_Required, Path.GetFullPath(generated)));
            Assert.IsNotNull(AssemblyOwnership.Find(Root + "/Out", s_Required));
        }

        [Test]
        public void Find_ResolvesGuidReferences()
        {
            WriteFile(Root + "/Game/Game.asmdef",
                "{ \"name\": \"Game\", \"references\": [\"GUID:" + RuntimeGuid() + "\", \"Unity.Localization\"] }");

            var owner = AssemblyOwnership.Find(Root + "/Game", s_Required);

            CollectionAssert.IsEmpty(owner.MissingReferences);
        }

        [Test]
        public void Find_FollowsAnAsmrefByNameAndByGuid()
        {
            WriteFile(Root + "/ByName/Extension.asmref", "{ \"reference\": \"" + AssemblyOwnership.RuntimeAssemblyName + "\" }");
            WriteFile(Root + "/ByGuid/Extension.asmref", "{ \"reference\": \"GUID:" + RuntimeGuid() + "\" }");

            foreach (var folder in new[] { "ByName", "ByGuid" })
            {
                var owner = AssemblyOwnership.Find(Root + "/" + folder, s_Required);

                Assert.IsNotNull(owner, folder);
                Assert.AreEqual(AssemblyOwnership.RuntimeAssemblyName, owner.Name, folder);
                Assert.AreEqual(Root + "/" + folder + "/Extension.asmref", owner.AsmrefPath, folder);
                StringAssert.EndsWith(".asmdef", owner.AsmdefPath, folder);
                // The runtime asmdef references Unity.Localization and is the other required assembly itself.
                CollectionAssert.IsEmpty(owner.MissingReferences, folder);
            }
        }

        [Test]
        public void Find_ReportsAnAsmrefToAnUnknownAssembly()
        {
            WriteFile(Root + "/Broken/Broken.asmref", "{ \"reference\": \"Does.Not.Exist\" }");

            var owner = AssemblyOwnership.Find(Root + "/Broken", s_Required);

            Assert.AreEqual("Does.Not.Exist", owner.Name);
            Assert.IsNull(owner.AsmdefPath);
            Assert.IsFalse(AssemblyOwnership.AddMissingReferences(owner, false));
        }

        [Test]
        public void RequiredReferences_IncludeUniTaskOnlyWhenRequested()
        {
            CollectionAssert.AreEqual(s_Required, AssemblyOwnership.RequiredReferences(false));
            CollectionAssert.AreEqual(
                new[]
                {
                    AssemblyOwnership.RuntimeAssemblyName,
                    AssemblyOwnership.LocalizationAssemblyName,
                    UniTaskSupport.RuntimeAssemblyName,
                    UniTaskSupport.UniTaskAssemblyName
                },
                AssemblyOwnership.RequiredReferences(true));
        }

        // ---------------------------------------------------------------------------------------------------
        // Writing
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void AddMissingReferences_WritesNamesAndKeepsTheBom()
        {
            var path = WriteFile(Root + "/Game/Game.asmdef",
                "{\n    \"name\": \"Game\",\n    \"references\": []\n}\n", bom: true);
            var owner = AssemblyOwnership.Find(Root + "/Game", s_Required);

            Assert.IsTrue(AssemblyOwnership.AddMissingReferences(owner, false));

            var bytes = File.ReadAllBytes(path);
            Assert.AreEqual(new byte[] { 0xEF, 0xBB, 0xBF }, new[] { bytes[0], bytes[1], bytes[2] }, "The BOM must be kept.");
            CollectionAssert.IsEmpty(AssemblyOwnership.Find(Root + "/Game", s_Required).MissingReferences);
            StringAssert.Contains("\"" + AssemblyOwnership.RuntimeAssemblyName + "\"", File.ReadAllText(path));
        }

        [Test]
        public void AddMissingReferences_UsesGuidsWhenTheAsmdefDoes()
        {
            var path = WriteFile(Root + "/Game/Game.asmdef",
                "{\n    \"name\": \"Game\",\n    \"references\": [\n        \"GUID:" + RuntimeGuid() + "\"\n    ]\n}\n");
            var owner = AssemblyOwnership.Find(Root + "/Game", s_Required);
            CollectionAssert.AreEqual(new[] { AssemblyOwnership.LocalizationAssemblyName }, owner.MissingReferences);

            Assert.IsTrue(AssemblyOwnership.AddMissingReferences(owner, false));

            var references = JsonUtility.FromJson<AsmdefJson>(File.ReadAllText(path)).references;
            Assert.AreEqual(2, references.Length);
            StringAssert.StartsWith("GUID:", references[1]);
            CollectionAssert.IsEmpty(AssemblyOwnership.Find(Root + "/Game", s_Required).MissingReferences);
        }

        [Test]
        public void AddMissingReferences_DoesNothingWhenComplete()
        {
            WriteFile(Root + "/Game/Game.asmdef",
                "{ \"name\": \"Game\", \"references\": [\"" + AssemblyOwnership.RuntimeAssemblyName + "\", \"Unity.Localization\"] }");
            var owner = AssemblyOwnership.Find(Root + "/Game", s_Required);

            Assert.IsFalse(AssemblyOwnership.AddMissingReferences(owner, false));
        }
    }
}

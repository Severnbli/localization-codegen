using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Localization;

namespace Severnbli.LocalizationCodegen.Editor.Tests
{
    /// <summary>
    /// File-level tests for the writer/cleanup paths. These create temporary table collections, so they are slower
    /// than the pure emitter tests.
    /// </summary>
    public class GenerationIntegrationTests
    {
        const string TestRoot = "Assets/__LocalizationCodegenTests";

        CodegenSettings m_Settings;
        StringTableCollection m_Collection;

        string m_OutputFolder;
        string m_Namespace;
        bool m_SetupCompleted;
        bool m_AutoRegenerate;
        bool m_GenerateStringTables;
        bool m_GenerateAssetTables;
        bool m_UseRuntimeAlias;
        SmartStringMode m_SmartStrings;
        bool m_GenerateUniTask;
        string m_LastOutputFolder;
        string[] m_GeneratedPaths;
        string[] m_CollectionHashes;
        string m_LastModelHash;

        [SetUp]
        public void SetUp()
        {
            m_Settings = CodegenSettings.instance;

            m_OutputFolder = m_Settings.OutputFolder;
            m_Namespace = m_Settings.Namespace;
            m_SetupCompleted = m_Settings.SetupCompleted;
            m_AutoRegenerate = m_Settings.AutoRegenerate;
            m_GenerateStringTables = m_Settings.GenerateStringTables;
            m_GenerateAssetTables = m_Settings.GenerateAssetTables;
            m_UseRuntimeAlias = m_Settings.UseRuntimeAlias;
            m_SmartStrings = m_Settings.SmartStrings;
            m_GenerateUniTask = m_Settings.GenerateUniTask;
            m_LastOutputFolder = m_Settings.LastOutputFolder;
            m_GeneratedPaths = m_Settings.GeneratedPaths;
            m_CollectionHashes = m_Settings.CollectionHashes;
            m_LastModelHash = m_Settings.LastModelHash;

            if (AssetDatabase.IsValidFolder(TestRoot))
                AssetDatabase.DeleteAsset(TestRoot);

            EnsureAddressableSettings();
            m_Collection = LocalizationEditorSettings.CreateStringTableCollection(
                "CodegenIntegration",
                TestRoot,
                new System.Collections.Generic.List<UnityEngine.Localization.Locale>());
            m_Collection.SharedData.AddKey("Greeting", 1);
            EditorUtility.SetDirty(m_Collection.SharedData);
            AssetDatabase.SaveAssets();

            m_Settings.SetupCompleted = true;
            m_Settings.AutoRegenerate = false;
            m_Settings.GenerateStringTables = true;
            m_Settings.GenerateAssetTables = false;
            m_Settings.UseRuntimeAlias = false;
            m_Settings.SmartStrings = SmartStringMode.Off;
            m_Settings.GenerateUniTask = false;
            m_Settings.Namespace = "CodegenIntegrationTests";
            m_Settings.OutputFolder = TestRoot + "/OutA";
            m_Settings.CollectionHashes = new string[0];
            m_Settings.SaveSettings();
        }

        [TearDown]
        public void TearDown()
        {
            m_Settings.OutputFolder = m_OutputFolder;
            m_Settings.Namespace = m_Namespace;
            m_Settings.SetupCompleted = m_SetupCompleted;
            m_Settings.AutoRegenerate = m_AutoRegenerate;
            m_Settings.GenerateStringTables = m_GenerateStringTables;
            m_Settings.GenerateAssetTables = m_GenerateAssetTables;
            m_Settings.UseRuntimeAlias = m_UseRuntimeAlias;
            m_Settings.SmartStrings = m_SmartStrings;
            m_Settings.GenerateUniTask = m_GenerateUniTask;
            m_Settings.LastOutputFolder = m_LastOutputFolder;
            m_Settings.GeneratedPaths = m_GeneratedPaths;
            m_Settings.CollectionHashes = m_CollectionHashes;
            m_Settings.LastModelHash = m_LastModelHash;
            m_Settings.SaveSettings();

            if (AssetDatabase.IsValidFolder(TestRoot))
                AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Creating a table collection lazily creates the Addressables settings. On Unity 6 (Addressables 2.x)
        /// that fails in a fresh project: the settings folder is created with Directory.CreateDirectory, so
        /// AssetDatabase.CreateAsset cannot place the asset there and Addressables dereferences null. Creating the
        /// folder through the AssetDatabase first avoids it.
        /// </summary>
        static void EnsureAddressableSettings()
        {
            if (AddressableAssetSettingsDefaultObject.SettingsExists)
                return;

            var folder = AddressableAssetSettingsDefaultObject.kDefaultConfigFolder;
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));

            AddressableAssetSettingsDefaultObject.Settings = AddressableAssetSettings.Create(
                folder, AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName, true, true);
        }

        [Test]
        public void ChangingOutputFolder_RemovesFilesFromTheOldFolder()
        {
            var firstRun = CodeGenerator.Run(new GenerateOptions { PromptOnMassDeletion = false, AllowInBatchMode = true });
            Assert.IsTrue(firstRun.Success, firstRun.Error);

            var oldFile = TestRoot + "/OutA/CodegenIntegration.g.cs";
            Assert.IsTrue(File.Exists(oldFile), "The first run should write the table class.");
            StringAssert.StartsWith(CodeGenerator.Marker, File.ReadAllText(oldFile));

            m_Settings.OutputFolder = TestRoot + "/OutB";
            var secondRun = CodeGenerator.Run(new GenerateOptions { PromptOnMassDeletion = false, AllowInBatchMode = true });
            Assert.IsTrue(secondRun.Success, secondRun.Error);

            Assert.IsFalse(File.Exists(oldFile), "Files from the previous output folder must be removed.");
            Assert.IsTrue(File.Exists(TestRoot + "/OutB/CodegenIntegration.g.cs"));
        }

        [Test]
        public void DryRun_ReportsHandEditedFile_EvenWhenTablesAreUnchanged()
        {
            var firstRun = CodeGenerator.Run(new GenerateOptions { PromptOnMassDeletion = false, AllowInBatchMode = true });
            Assert.IsTrue(firstRun.Success, firstRun.Error);

            var check = CodeGenerator.Run(new GenerateOptions
            {
                DryRun = true, PromptOnMassDeletion = false, AllowInBatchMode = true
            });
            Assert.IsTrue(check.Success, check.Error);
            Assert.IsFalse(check.WouldChange, "Freshly generated code must be reported as up to date.");

            var path = TestRoot + "/OutA/CodegenIntegration.g.cs";
            File.AppendAllText(path, "// edited by hand\n");

            check = CodeGenerator.Run(new GenerateOptions
            {
                DryRun = true, PromptOnMassDeletion = false, AllowInBatchMode = true
            });
            Assert.IsTrue(check.Success, check.Error);
            Assert.IsTrue(check.WouldChange, "A hand-edited generated file must be reported as out of date.");
            Assert.AreEqual(1, check.Written);
        }

        [Test]
        public void RemovedCollection_DropsItsStoredHash()
        {
            var firstRun = CodeGenerator.Run(new GenerateOptions { PromptOnMassDeletion = false, AllowInBatchMode = true });
            Assert.IsTrue(firstRun.Success, firstRun.Error);

            m_Settings.CollectionHashes = new[] { "S|RemovedCollection|0123456789abcdef" };
            var secondRun = CodeGenerator.Run(new GenerateOptions { PromptOnMassDeletion = false, AllowInBatchMode = true });
            Assert.IsTrue(secondRun.Success, secondRun.Error);

            CollectionAssert.IsEmpty(
                System.Array.FindAll(m_Settings.CollectionHashes, h => h.StartsWith("S|RemovedCollection|")));
        }

        [Test]
        public void UniTaskOption_EmitsAsyncMembersOnlyWhenTheAddOnIsCompiled()
        {
            m_Settings.GenerateUniTask = true;
            var run = CodeGenerator.Run(new GenerateOptions { PromptOnMassDeletion = false, AllowInBatchMode = true });
            Assert.IsTrue(run.Success, run.Error);

            var output = File.ReadAllText(TestRoot + "/OutA/CodegenIntegration.g.cs");
            if (UniTaskSupport.IsAvailable)
            {
                StringAssert.Contains("GreetingAsync(", output);
                CollectionAssert.DoesNotContain(run.Warnings, UniTaskSupport.MissingMessage);
            }
            else
            {
                // Code that references UniTask would not compile, so the option is skipped with a warning.
                StringAssert.DoesNotContain("UniTask", output);
                CollectionAssert.Contains(run.Warnings, UniTaskSupport.MissingMessage);
            }
        }

        [Test]
        public void CrLfFileOnDisk_IsNotRewritten()
        {
            var firstRun = CodeGenerator.Run(new GenerateOptions { PromptOnMassDeletion = false, AllowInBatchMode = true });
            Assert.IsTrue(firstRun.Success, firstRun.Error);

            var path = TestRoot + "/OutA/CodegenIntegration.g.cs";
            var crlf = File.ReadAllText(path).Replace("\n", "\r\n");
            File.WriteAllText(path, crlf);

            // Force the emitter to build the content again instead of taking the incremental hash shortcut.
            m_Settings.CollectionHashes = new string[0];

            var secondRun = CodeGenerator.Run(new GenerateOptions { PromptOnMassDeletion = false, AllowInBatchMode = true });
            Assert.IsTrue(secondRun.Success, secondRun.Error);
            Assert.AreEqual(0, secondRun.Written);
            Assert.AreEqual(crlf, File.ReadAllText(path));
        }
    }
}

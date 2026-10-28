using UnityEditor;
using UnityEngine;

namespace Severnbli.LocalizationCodegen.Editor
{
    [FilePath("ProjectSettings/LocalizationCodegenSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class CodegenSettings : ScriptableSingleton<CodegenSettings>
    {
        /// <summary>Current settings schema. Version 2 introduced the 1.1.0 options.</summary>
        public const int CurrentVersion = 2;

        [SerializeField] private int settingsVersion;

        [SerializeField] private bool setupCompleted;
        [SerializeField] private string outputFolder = "Assets/Generated/Localization";
        [SerializeField] private string generatedNamespace = "Localization";
        [SerializeField] private bool autoRegenerate = true;
        [SerializeField] private bool generateStringTables = true;
        [SerializeField] private bool generateAssetTables = true;

        // Cleanup manifest (A2).
        [SerializeField] private string lastOutputFolder;
        [SerializeField] private string[] generatedPaths = new string[0];
        [SerializeField] private string lastAsmdefPath;

        // Incremental generation (B8) and staleness (A6).
        [SerializeField] private string lastModelHash;
        [SerializeField] private string[] collectionHashes = new string[0];

        // Options introduced in 1.1.0. Defaults are intentionally "old output" and are migrated explicitly.
        [SerializeField] private bool generateReferences;
        [SerializeField] private bool generateXmlDocs;
        [SerializeField] private int smartStrings;
        [SerializeField] private int namingConvention;
        [SerializeField] private string keySeparators = "";
        [SerializeField] private int smartParamTypes;
        [SerializeField] private bool generateAsmdef;
        [SerializeField] private bool failOnSmartStringMismatch;

        // Options introduced in 1.2.0. Off by default, so no migration is needed.
        [SerializeField] private bool generateUniTask;

        // Options introduced in 1.3.1. Off by default, so no migration is needed.
        [SerializeField] private bool generateLoadTable;

        public bool SetupCompleted { get => setupCompleted; set => setupCompleted = value; }
        public string OutputFolder { get => outputFolder; set => outputFolder = value; }
        public string Namespace { get => generatedNamespace; set => generatedNamespace = value; }
        public bool AutoRegenerate { get => autoRegenerate; set => autoRegenerate = value; }
        public bool GenerateStringTables { get => generateStringTables; set => generateStringTables = value; }
        public bool GenerateAssetTables { get => generateAssetTables; set => generateAssetTables = value; }

        public string LastOutputFolder { get => lastOutputFolder; set => lastOutputFolder = value; }
        public string[] GeneratedPaths { get => generatedPaths ?? (generatedPaths = new string[0]); set => generatedPaths = value; }
        public string LastAsmdefPath { get => lastAsmdefPath; set => lastAsmdefPath = value; }
        public string LastModelHash { get => lastModelHash; set => lastModelHash = value; }
        public string[] CollectionHashes { get => collectionHashes ?? (collectionHashes = new string[0]); set => collectionHashes = value; }

        public bool GenerateReferences { get => generateReferences; set => generateReferences = value; }
        public bool GenerateXmlDocs { get => generateXmlDocs; set => generateXmlDocs = value; }
        public bool GenerateAsmdef { get => generateAsmdef; set => generateAsmdef = value; }
        public bool FailOnSmartStringMismatch { get => failOnSmartStringMismatch; set => failOnSmartStringMismatch = value; }
        public bool GenerateUniTask { get => generateUniTask; set => generateUniTask = value; }
        public bool GenerateLoadTable { get => generateLoadTable; set => generateLoadTable = value; }

        public SmartStringMode SmartStrings
        {
            get => (SmartStringMode)Mathf.Clamp(smartStrings, 0, 2);
            set => smartStrings = (int)value;
        }

        public NamingConvention Naming
        {
            get => (NamingConvention)Mathf.Clamp(namingConvention, 0, 1);
            set => namingConvention = (int)value;
        }

        public SmartParamTypeMode SmartParamTypes
        {
            get => (SmartParamTypeMode)Mathf.Clamp(smartParamTypes, 0, 2);
            set => smartParamTypes = (int)value;
        }

        public string KeySeparators
        {
            get => keySeparators ?? "";
            set => keySeparators = value ?? "";
        }

        public int SettingsVersion { get => settingsVersion; set => settingsVersion = value; }

        /// <summary>Upgrades settings created by older versions.</summary>
        public void EnsureMigrated()
        {
            if (settingsVersion >= CurrentVersion)
                return;

            if (settingsVersion < 2)
            {
                if (generatedPaths == null)
                    generatedPaths = new string[0];
                if (collectionHashes == null)
                    collectionHashes = new string[0];
                settingsVersion = CurrentVersion;
                SaveSettings();
            }
        }

        public void SaveSettings() => Save(true);
    }
}

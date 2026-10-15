using System;
using UnityEditor;
using UnityEngine;

namespace Severnbli.LocalizationCodegen.Editor
{
    [FilePath("ProjectSettings/LocalizationCodegenSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class CodegenSettings : ScriptableSingleton<CodegenSettings>
    {
        [SerializeField] private bool setupCompleted;
        [SerializeField] private string outputFolder = "Assets/Generated/Localization";
        [SerializeField] private string generatedNamespace = "Localization";
        [SerializeField] private bool autoRegenerate = true;
        [SerializeField] private bool generateStringTables = true;
        [SerializeField] private bool generateAssetTables = true;

        public bool SetupCompleted { get => setupCompleted; set => setupCompleted = value; }
        public string OutputFolder { get => outputFolder; set => outputFolder = value; }
        public string Namespace { get => generatedNamespace; set => generatedNamespace = value; }
        public bool AutoRegenerate { get => autoRegenerate; set => autoRegenerate = value; }
        public bool GenerateStringTables { get => generateStringTables; set => generateStringTables = value; }
        public bool GenerateAssetTables { get => generateAssetTables; set => generateAssetTables = value; }

        public void SaveSettings() => Save(true);
    }
}

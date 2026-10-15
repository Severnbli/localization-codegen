using UnityEditor;
using UnityEngine;

namespace Severnbli.LocalizationCodegen.Editor
{
    public sealed class CodegenWindow : EditorWindow
    {
        [MenuItem("Window/Localization Codegen")]
        public static void Open() => GetWindow<CodegenWindow>("Localization Codegen");

        void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            SettingsGui.Draw();

            EditorGUILayout.Space();
            if (GUILayout.Button("Regenerate Now", GUILayout.Height(28)))
                CodeGenerator.Generate();

            if (GUILayout.Button("Run Setup Wizard"))
                SetupWizard.Open();
        }
    }
}

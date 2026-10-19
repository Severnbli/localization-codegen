using UnityEditor;
using UnityEngine;

namespace Severnbli.LocalizationCodegen.Editor
{
    public sealed class CodegenWindow : EditorWindow
    {
        Vector2 scroll;
        string statusMessage;
        MessageType statusType = MessageType.Info;

        [MenuItem("Window/Localization Codegen")]
        public static void Open() => GetWindow<CodegenWindow>("Localization Codegen");

        void OnEnable()
        {
            minSize = new Vector2(400, 380);
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Localization Codegen", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generates strongly typed C# classes from Unity Localization table collections. " +
                "Each table collection becomes a class and each key a property, e.g. Ui.PlayerName.",
                MessageType.Info);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            SettingsGui.Draw();

            EditorGUILayout.Space();
            if (GUILayout.Button("Regenerate Now", GUILayout.Height(28)))
            {
                var changed = CodeGenerator.Generate();
                if (changed < 0)
                {
                    statusMessage = "Output folder must be inside the Assets folder.";
                    statusType = MessageType.Error;
                }
                else
                {
                    statusMessage = $"Generation complete — {changed} file(s) changed.";
                    statusType = MessageType.Info;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Output Folder", GUILayout.Height(26)))
                    OpenOutputFolder();
                if (GUILayout.Button("Run Setup Wizard", GUILayout.Height(26)))
                    SetupWizard.Open();
            }

            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }

            EditorGUILayout.EndScrollView();
        }

        static void OpenOutputFolder()
        {
            var folder = (CodegenSettings.instance.OutputFolder ?? "").Trim();
            if (string.IsNullOrEmpty(folder) || !folder.StartsWith("Assets"))
            {
                EditorUtility.DisplayDialog("Localization Codegen",
                    "Output folder must be inside the Assets folder.", "OK");
                return;
            }

            if (AssetDatabase.IsValidFolder(folder))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folder);
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            else
            {
                EditorUtility.RevealInFinder(Application.dataPath);
            }
        }
    }
}

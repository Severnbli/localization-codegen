using UnityEditor;
using UnityEngine;

namespace Severnbli.LocalizationCodegen.Editor
{
    public sealed class CodegenWindow : EditorWindow
    {
        Vector2 scroll;
        bool showWarnings;

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
                CodeGenerator.Run(new GenerateOptions
                {
                    IsAutomatic = false,
                    PromptOnMassDeletion = true
                });
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Output Folder", GUILayout.Height(26)))
                    OpenOutputFolder();
                if (GUILayout.Button("Run Setup Wizard", GUILayout.Height(26)))
                    SetupWizard.Open();
            }

            DrawLastResult();

            EditorGUILayout.EndScrollView();
        }

        void DrawLastResult()
        {
            var result = CodeGenerator.LastResult;
            if (result == null)
                return;

            EditorGUILayout.Space();
            if (!result.Success)
            {
                EditorGUILayout.HelpBox(
                    string.IsNullOrEmpty(result.Error) ? "Generation failed." : result.Error,
                    MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Last run: {result.Total} table class file(s) – {result.Written} written, " +
                    $"{result.Deleted} deleted, {result.Unchanged} unchanged.",
                    MessageType.Info);
            }

            if (result.Warnings.Count == 0)
                return;

            showWarnings = EditorGUILayout.Foldout(showWarnings, $"Warnings ({result.Warnings.Count})", true);
            if (!showWarnings)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var warning in result.Warnings)
                    EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedLabel);
            }
        }

        static void OpenOutputFolder()
        {
            string folder;
            string error;
            if (!OutputPath.TryNormalize(CodegenSettings.instance.OutputFolder, out folder, out error))
            {
                EditorUtility.DisplayDialog("Localization Codegen", error, "OK");
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

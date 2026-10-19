using UnityEditor;
using UnityEngine;

namespace Severnbli.LocalizationCodegen.Editor
{
    public sealed class SetupWizard : EditorWindow
    {
        public static void Open()
        {
            var window = GetWindow<SetupWizard>(true, "Localization Codegen Setup");
            window.minSize = new Vector2(460, 360);
            window.maxSize = new Vector2(460, 360);
        }

        void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Welcome to Localization Codegen", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "A C# class will be generated for every Unity Localization table collection. " +
                "Each key becomes a strongly typed property, e.g. Ui.PlayerName. " +
                "You can change these settings later in Window > Localization Codegen.",
                MessageType.Info);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            SettingsGui.Draw();

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Skip", GUILayout.Height(28)))
                    Finish(false);

                using (new EditorGUI.DisabledScope(!SettingsGui.IsOutputFolderValid(out _)))
                {
                    if (GUILayout.Button("Finish and Generate", GUILayout.Height(28)))
                        Finish(true);
                }
            }
        }

        void Finish(bool generate)
        {
            var settings = CodegenSettings.instance;
            settings.SetupCompleted = true;
            settings.SaveSettings();
            Close();
            if (generate)
                CodeGenerator.Generate();
        }
    }
}

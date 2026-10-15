using UnityEditor;
using UnityEngine;

namespace Severnbli.LocalizationCodegen.Editor
{
    public sealed class SetupWizard : EditorWindow
    {
        public static void Open()
        {
            var window = GetWindow<SetupWizard>(true, "Localization Codegen Setup");
            window.minSize = window.maxSize = new Vector2(420, 260);
        }

        void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Welcome to Localization Codegen", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "C# classes will be generated for every Unity Localization table collection, e.g. Ui.PlayerName. " +
                "You can change these settings later in Window > Localization Codegen.", MessageType.Info);
            EditorGUILayout.Space();

            SettingsGui.Draw();

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Skip", GUILayout.Height(26)))
                    Finish(false);
                if (GUILayout.Button("Finish and Generate", GUILayout.Height(26)))
                    Finish(true);
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

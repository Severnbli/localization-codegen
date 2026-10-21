using UnityEditor;
using UnityEngine;

namespace Severnbli.LocalizationCodegen.Editor
{
    public sealed class SetupWizard : EditorWindow
    {
        const string ShownKey = "LocalizationCodegen.WizardShown";

        Vector2 m_Scroll;

        public static void Open()
        {
            SessionState.SetBool(ShownKey, true);
            var window = GetWindow<SetupWizard>(true, "Localization Codegen Setup");
            window.minSize = new Vector2(460, 360);
        }

        void OnGUI()
        {
            var settings = CodegenSettings.instance;
            settings.EnsureMigrated();

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Welcome to Localization Codegen", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "A C# class will be generated for every Unity Localization table collection. " +
                "Each key becomes a strongly typed property, e.g. Ui.PlayerName. " +
                "You can change these settings later in Project Settings > Localization Codegen.",
                MessageType.Info);
            EditorGUILayout.HelpBox(
                "Tip: choose a project-specific namespace such as 'MyGame.Localization' instead of the generic " +
                "default 'Localization'. It avoids collisions with UnityEngine.Localization types.",
                MessageType.None);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            SettingsGui.Draw();

            EditorGUILayout.EndScrollView();

            string folder;
            string folderError;
            var folderOk = OutputPath.TryNormalize(settings.OutputFolder, out folder, out folderError);
            var namespaceOk = NamespaceUtil.IsValid(settings.Namespace);
            if (!folderOk)
                EditorGUILayout.HelpBox(folderError, MessageType.Error);
            else if (!namespaceOk)
                EditorGUILayout.HelpBox("Namespace contains invalid characters.", MessageType.Error);

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Skip", GUILayout.Height(28)))
                    Finish(false);

                using (new EditorGUI.DisabledScope(!folderOk || !namespaceOk))
                {
                    if (GUILayout.Button("Finish and Generate", GUILayout.Height(28)))
                        Finish(true);
                }
            }
        }

        void Finish(bool generate)
        {
            var settings = CodegenSettings.instance;
            var wasCompleted = settings.SetupCompleted;

            settings.SetupCompleted = true;
            if (!generate)
                settings.AutoRegenerate = false;
            else if (!wasCompleted && settings.SmartStrings == SmartStringMode.Off)
                settings.SmartStrings = SmartStringMode.Methods;

            settings.SaveSettings();
            Close();

            if (generate)
            {
                CodeGenerator.Run(new GenerateOptions
                {
                    IsAutomatic = false,
                    PromptOnMassDeletion = true
                });
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Localization Codegen",
                    "Automatic regeneration is off. You can enable 'Regenerate On Changes' later in " +
                    "Project Settings > Localization Codegen.",
                    "OK");
            }
        }
    }
}

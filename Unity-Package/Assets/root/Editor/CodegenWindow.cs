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
            minSize = new Vector2(420, 380);
        }

        void OnGUI()
        {
            SettingsGui.DrawToolbar();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUILayout.VerticalScope(SettingsGui.PageStyle))
            {
                EditorGUILayout.LabelField(
                    "Generates strongly typed C# classes from Unity Localization table collections: each " +
                    "collection becomes a class and each key a member, e.g. Ui.PlayerName.",
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space();

                SettingsGui.DrawLastResult(ref showWarnings);
                EditorGUILayout.Space();

                SettingsGui.Draw();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}

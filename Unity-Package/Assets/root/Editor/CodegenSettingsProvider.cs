using System.Collections.Generic;
using UnityEditor;

namespace Severnbli.LocalizationCodegen.Editor
{
    /// <summary>Project settings page (B5).</summary>
    static class CodegenSettingsProvider
    {
        static bool s_ShowWarnings;

        [SettingsProvider]
        static SettingsProvider Create()
        {
            return new SettingsProvider("Project/Localization Codegen", SettingsScope.Project)
            {
                label = "Localization Codegen",
                keywords = new HashSet<string>(SettingsGui.Content.Keywords()),
                guiHandler = _ =>
                {
                    SettingsGui.DrawToolbar();
                    using (new EditorGUILayout.VerticalScope(SettingsGui.PageStyle))
                    {
                        EditorGUILayout.LabelField(
                            "Generates strongly typed C# classes from Unity Localization table collections.",
                            EditorStyles.wordWrappedMiniLabel);
                        EditorGUILayout.Space();

                        SettingsGui.DrawLastResult(ref s_ShowWarnings);
                        EditorGUILayout.Space();

                        SettingsGui.Draw();
                    }
                }
            };
        }
    }
}

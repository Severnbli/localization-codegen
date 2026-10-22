using UnityEditor;
using UnityEngine;

namespace Severnbli.LocalizationCodegen.Editor
{
    /// <summary>Project settings page (B5).</summary>
    static class CodegenSettingsProvider
    {
        [SettingsProvider]
        static SettingsProvider Create()
        {
            return new SettingsProvider("Project/Localization Codegen", SettingsScope.Project)
            {
                label = "Localization Codegen",
                guiHandler = _ =>
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox(
                        "Generates strongly typed C# classes from Unity Localization table collections.",
                        MessageType.Info);
                    SettingsGui.Draw();

                    EditorGUILayout.Space();
                    if (GUILayout.Button("Regenerate Now"))
                        CodeGenerator.Run(new GenerateOptions { IsAutomatic = false });
                }
            };
        }
    }
}

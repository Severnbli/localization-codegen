using UnityEditor;
using UnityEngine;

namespace Severnbli.LocalizationCodegen.Editor
{
    static class SettingsGui
    {
        public static bool Draw()
        {
            var s = CodegenSettings.instance;
            EditorGUI.BeginChangeCheck();

            s.Namespace = EditorGUILayout.TextField("Namespace", s.Namespace);

            using (new EditorGUILayout.HorizontalScope())
            {
                s.OutputFolder = EditorGUILayout.TextField("Output Folder", s.OutputFolder);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var picked = EditorUtility.OpenFolderPanel("Output Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        var root = System.IO.Path.GetFullPath(Application.dataPath + "/..").Replace('\\', '/');
                        picked = picked.Replace('\\', '/');
                        if (picked.StartsWith(root + "/"))
                            s.OutputFolder = picked.Substring(root.Length + 1);
                        else
                            EditorUtility.DisplayDialog("Localization Codegen", "Folder must be inside this project's Assets folder.", "OK");
                        GUI.FocusControl(null);
                    }
                }
            }

            s.GenerateStringTables = EditorGUILayout.Toggle("String Tables", s.GenerateStringTables);
            s.GenerateAssetTables = EditorGUILayout.Toggle("Asset Tables", s.GenerateAssetTables);
            s.AutoRegenerate = EditorGUILayout.Toggle("Regenerate On Changes", s.AutoRegenerate);

            var changed = EditorGUI.EndChangeCheck();
            if (changed)
                s.SaveSettings();
            return changed;
        }
    }
}

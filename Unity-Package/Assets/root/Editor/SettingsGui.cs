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

            EditorGUILayout.LabelField("Generated Code", EditorStyles.boldLabel);

            s.Namespace = EditorGUILayout.TextField(
                new GUIContent("Namespace", "Namespace of the generated classes. Leave empty for the global namespace."),
                s.Namespace);

            if (!IsNamespaceValid(s.Namespace))
                EditorGUILayout.HelpBox("Namespace contains invalid characters; invalid segments are sanitized during generation.", MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                s.OutputFolder = EditorGUILayout.TextField(
                    new GUIContent("Output Folder", "Folder (inside Assets) where generated .g.cs files are written."),
                    s.OutputFolder);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                    PickOutputFolder(s);
            }

            if (!IsOutputFolderValid(s.OutputFolder, out var folderReason))
                EditorGUILayout.HelpBox(folderReason, MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tables", EditorStyles.boldLabel);

            s.GenerateStringTables = EditorGUILayout.Toggle(
                new GUIContent("String Tables", "Generate classes for string table collections."),
                s.GenerateStringTables);
            s.GenerateAssetTables = EditorGUILayout.Toggle(
                new GUIContent("Asset Tables", "Generate classes for asset table collections."),
                s.GenerateAssetTables);
            s.AutoRegenerate = EditorGUILayout.Toggle(
                new GUIContent("Regenerate On Changes", "Automatically regenerate when table collections or entries change."),
                s.AutoRegenerate);

            var changed = EditorGUI.EndChangeCheck();
            if (changed)
                s.SaveSettings();
            return changed;
        }

        static void PickOutputFolder(CodegenSettings s)
        {
            var picked = EditorUtility.OpenFolderPanel("Output Folder", "Assets", "");
            if (string.IsNullOrEmpty(picked))
                return;

            var root = System.IO.Path.GetFullPath(Application.dataPath + "/..").Replace('\\', '/');
            picked = picked.Replace('\\', '/');
            if (picked.StartsWith(root + "/"))
                s.OutputFolder = picked.Substring(root.Length + 1);
            else
                EditorUtility.DisplayDialog("Localization Codegen", "Folder must be inside this project's Assets folder.", "OK");

            GUI.FocusControl(null);
        }

        public static bool IsOutputFolderValid(out string reason)
            => IsOutputFolderValid(CodegenSettings.instance.OutputFolder, out reason);

        public static bool IsOutputFolderValid(string folder, out string reason)
        {
            folder = (folder ?? "").Trim();
            if (string.IsNullOrEmpty(folder))
            {
                reason = "Output folder cannot be empty.";
                return false;
            }
            if (!folder.StartsWith("Assets"))
            {
                reason = "Output folder must be inside the Assets folder.";
                return false;
            }
            reason = null;
            return true;
        }

        static bool IsNamespaceValid(string ns)
        {
            if (string.IsNullOrEmpty(ns))
                return true;

            foreach (var segment in ns.Split('.'))
            {
                if (string.IsNullOrEmpty(segment))
                    return false;
                if (!char.IsLetter(segment[0]) && segment[0] != '_')
                    return false;
                foreach (var c in segment)
                {
                    if (!char.IsLetterOrDigit(c) && c != '_')
                        return false;
                }
            }
            return true;
        }
    }
}

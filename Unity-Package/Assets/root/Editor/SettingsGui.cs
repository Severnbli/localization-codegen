using UnityEditor;
using UnityEngine;

namespace Severnbli.LocalizationCodegen.Editor
{
    static class SettingsGui
    {
        public static bool Draw()
        {
            var s = CodegenSettings.instance;
            s.EnsureMigrated();

            var oldSmartStrings = s.SmartStrings;
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Generated Code", EditorStyles.boldLabel);

            s.Namespace = EditorGUILayout.DelayedTextField(
                new GUIContent("Namespace", "Namespace of the generated classes. Leave empty for the global namespace."),
                s.Namespace);

            var normalized = NamespaceUtil.Normalize(s.Namespace);
            if (normalized == null)
            {
                EditorGUILayout.HelpBox("Generating into the global namespace.", MessageType.Info);
            }
            else if (!NamespaceUtil.IsValid(s.Namespace))
            {
                EditorGUILayout.HelpBox(
                    $"Namespace contains invalid segments; they will be sanitized to '{normalized}'.",
                    MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                s.OutputFolder = EditorGUILayout.DelayedTextField(
                    new GUIContent("Output Folder", "Folder (inside Assets) where generated .g.cs files are written."),
                    s.OutputFolder);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                    PickOutputFolder(s);
            }

            string folder;
            string folderError;
            if (!OutputPath.TryNormalize(s.OutputFolder, out folder, out folderError))
                EditorGUILayout.HelpBox(folderError, MessageType.Warning);
            else if (OutputPath.HasEditorSegment(folder))
                EditorGUILayout.HelpBox(
                    "The folder 'Editor' is part of the path. Generated runtime classes will compile into the editor " +
                    "assembly and will not be available in player builds.",
                    MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tables", EditorStyles.boldLabel);

            s.GenerateStringTables = EditorGUILayout.Toggle(
                new GUIContent("String Tables",
                    "Generate classes for string table collections. Disabling this deletes the matching generated files on the next run."),
                s.GenerateStringTables);
            s.GenerateAssetTables = EditorGUILayout.Toggle(
                new GUIContent("Asset Tables",
                    "Generate classes for asset table collections. Disabling this deletes the matching generated files on the next run."),
                s.GenerateAssetTables);
            s.AutoRegenerate = EditorGUILayout.Toggle(
                new GUIContent("Regenerate On Changes", "Automatically regenerate when table collections or entries change."),
                s.AutoRegenerate);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Code Style", EditorStyles.boldLabel);

            s.UseRuntimeAlias = EditorGUILayout.Toggle(
                new GUIContent("Use Runtime Alias",
                    "Reference the runtime helper through one file-level alias (__LT) instead of repeating the fully qualified name on every member."),
                s.UseRuntimeAlias);
            s.GenerateReferences = EditorGUILayout.Toggle(
                new GUIContent("Generate Reference Members",
                    "Emit a nested Ref class with LocalizedString / LocalizedAsset references."),
                s.GenerateReferences);
            s.GenerateXmlDocs = EditorGUILayout.Toggle(
                new GUIContent("XML Documentation",
                    "Emit XML doc comments containing the default locale text."),
                s.GenerateXmlDocs);

            s.SmartStrings = (SmartStringMode)EditorGUILayout.EnumPopup(
                new GUIContent("Smart Strings",
                    "Generate methods with parameters for smart string entries. Off keeps the 1.0 behaviour."),
                s.SmartStrings);
            s.SmartParamTypes = (SmartParamTypeMode)EditorGUILayout.EnumPopup(
                new GUIContent("Smart Parameter Types",
                    "Object uses object for every named parameter. Inferred uses int for plural and IEnumerable for list."),
                s.SmartParamTypes);
            s.Naming = (NamingConvention)EditorGUILayout.EnumPopup(
                new GUIContent("Naming Convention",
                    "Keep preserves the 1.0 names. PascalCase turns 'player name' into 'PlayerName'."),
                s.Naming);
            s.KeySeparators = EditorGUILayout.TextField(
                new GUIContent("Key Separators",
                    "Optional characters that create nested classes, e.g. '/' makes 'Menu/Start' become Menu.Start."),
                s.KeySeparators);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Advanced", EditorStyles.boldLabel);

            s.GenerateAsmdef = EditorGUILayout.Toggle(
                new GUIContent("Generate .asmdef",
                    "Write an assembly definition into the output folder so it can be placed below folders owned by another asmdef."),
                s.GenerateAsmdef);
            s.FailOnSmartStringMismatch = EditorGUILayout.Toggle(
                new GUIContent("Fail CI On Smart String Mismatch",
                    "Make CheckBatch exit with a non-zero code when locales use different smart string placeholders."),
                s.FailOnSmartStringMismatch);

            var changed = EditorGUI.EndChangeCheck();
            if (changed)
            {
                s.SaveSettings();
                CodegenHooks.MarkDirtyExternal();

                if (oldSmartStrings == SmartStringMode.Off && s.SmartStrings != SmartStringMode.Off)
                    CodeGenerator.LogPropertyToMethodChanges();
            }

            return changed;
        }

        static void PickOutputFolder(CodegenSettings s)
        {
            var dataPath = (Application.dataPath ?? "").Replace('\\', '/').TrimEnd('/');
            var picked = EditorUtility.OpenFolderPanel("Output Folder", dataPath, "");
            if (string.IsNullOrEmpty(picked))
            {
                GUIUtility.ExitGUI();
                return;
            }

            picked = picked.Replace('\\', '/').TrimEnd('/');
            if (string.Equals(picked, dataPath, System.StringComparison.OrdinalIgnoreCase))
            {
                s.OutputFolder = "Assets";
            }
            else if (picked.StartsWith(dataPath + "/", System.StringComparison.OrdinalIgnoreCase))
            {
                s.OutputFolder = "Assets/" + picked.Substring(dataPath.Length + 1);
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Localization Codegen",
                    "Folder must be inside this project's Assets folder.",
                    "OK");
            }

            GUI.FocusControl(null);
            GUIUtility.ExitGUI();
        }

        public static bool IsOutputFolderValid(out string reason)
            => OutputPath.TryNormalize(CodegenSettings.instance.OutputFolder, out _, out reason);

        public static bool IsNamespaceValid(string value) => NamespaceUtil.IsValid(value);
    }
}

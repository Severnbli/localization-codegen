using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Severnbli.LocalizationCodegen.Editor
{
    /// <summary>Settings UI shared by the Project Settings page, the editor window and the setup wizard.</summary>
    static class SettingsGui
    {
        const float LabelWidth = 220f;
        const string FoldoutPrefKey = "LocalizationCodegen.Settings.Foldout.";

        /// <summary>Every label and tooltip, also used as search keywords for the Project Settings page.</summary>
        internal static class Content
        {
            public static readonly GUIContent OutputSection = new GUIContent("Output");
            public static readonly GUIContent TablesSection = new GUIContent("Tables");
            public static readonly GUIContent MembersSection = new GUIContent("Generated Members");
            public static readonly GUIContent NamingSection = new GUIContent("Naming");
            public static readonly GUIContent AdvancedSection = new GUIContent("Advanced");

            public static readonly GUIContent Namespace = new GUIContent("Namespace",
                "Namespace of the generated classes. Leave empty for the global namespace.");
            public static readonly GUIContent OutputFolder = new GUIContent("Output Folder",
                "Folder (inside Assets) where generated .g.cs files are written.");
            public static readonly GUIContent Browse = new GUIContent("Browse…", "Pick a folder inside Assets.");

            public static readonly GUIContent StringTables = new GUIContent("String Tables",
                "Generate classes for string table collections. Disabling this deletes the matching generated files on the next run.");
            public static readonly GUIContent AssetTables = new GUIContent("Asset Tables",
                "Generate classes for asset table collections. Disabling this deletes the matching generated files on the next run.");
            public static readonly GUIContent AutoRegenerate = new GUIContent("Regenerate On Changes",
                "Automatically regenerate when table collections or entries change.");

            public static readonly GUIContent SmartStrings = new GUIContent("Smart Strings",
                "Generate methods with parameters for smart string entries. Off keeps the 1.0 behaviour.");
            public static readonly GUIContent SmartParamTypes = new GUIContent("Smart Parameter Types",
                "Object uses object for every named parameter. Inferred uses int for plural and IEnumerable for list.");
            public static readonly GUIContent References = new GUIContent("Generate Reference Members",
                "Emit a nested Ref class with LocalizedString / LocalizedAsset references that follow locale changes.");
            public static readonly GUIContent UniTask = new GUIContent("UniTask Members",
                "Also emit ...Async members that return UniTask and accept a CancellationToken. Requires UniTask (com.cysharp.unitask).");
            public static readonly GUIContent XmlDocs = new GUIContent("XML Documentation",
                "Emit XML doc comments containing the default locale text.");

            public static readonly GUIContent Naming = new GUIContent("Naming Convention",
                "Keep preserves the 1.0 names. PascalCase turns 'player name' into 'PlayerName'.");
            public static readonly GUIContent KeySeparators = new GUIContent("Key Separators",
                "Optional characters that create nested classes, e.g. '/' makes 'Menu/Start' become Menu.Start.");

            public static readonly GUIContent GenerateAsmdef = new GUIContent("Generate .asmdef",
                "Write an assembly definition into the output folder, so the generated code gets its own assembly.");
            public static readonly GUIContent FailOnMismatch = new GUIContent("Fail CI On Smart String Mismatch",
                "Make CheckBatch exit with a non-zero code when locales use different smart string placeholders.");

            public static IEnumerable<string> Keywords()
            {
                foreach (var field in typeof(Content).GetFields())
                {
                    var content = field.GetValue(null) as GUIContent;
                    if (content != null && !string.IsNullOrEmpty(content.text))
                        yield return content.text;
                }
            }
        }

        static GUIStyle s_SectionBody;
        static GUIStyle s_PageStyle;

        /// <summary>Page margins matching Unity's own settings pages.</summary>
        public static GUIStyle PageStyle => s_PageStyle ?? (s_PageStyle = new GUIStyle
        {
            padding = new RectOffset(10, 10, 8, 8)
        });

        static GUIStyle SectionBody => s_SectionBody ?? (s_SectionBody = new GUIStyle
        {
            padding = new RectOffset(14, 4, 2, 6)
        });

        public static bool Draw()
        {
            var s = CodegenSettings.instance;
            s.EnsureMigrated();

            var oldSmartStrings = s.SmartStrings;
            var oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;
            EditorGUI.BeginChangeCheck();

            Section("output", Content.OutputSection, () => DrawOutput(s));
            Section("tables", Content.TablesSection, () => DrawTables(s));
            Section("members", Content.MembersSection, () => DrawMembers(s));
            Section("naming", Content.NamingSection, () => DrawNaming(s));
            Section("advanced", Content.AdvancedSection, () => DrawAdvanced(s));

            var changed = EditorGUI.EndChangeCheck();
            EditorGUIUtility.labelWidth = oldLabelWidth;

            if (changed)
            {
                s.SaveSettings();
                CodegenHooks.MarkDirtyExternal();

                if (oldSmartStrings == SmartStringMode.Off && s.SmartStrings != SmartStringMode.Off)
                    CodeGenerator.LogPropertyToMethodChanges();
            }

            return changed;
        }

        /// <summary>A collapsible section whose open state is remembered per user.</summary>
        static void Section(string id, GUIContent title, System.Action body)
        {
            var key = FoldoutPrefKey + id;
            var expanded = EditorPrefs.GetBool(key, true);
            var nowExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, title);
            if (nowExpanded != expanded)
                EditorPrefs.SetBool(key, nowExpanded);

            if (nowExpanded)
            {
                using (new EditorGUILayout.VerticalScope(SectionBody))
                    body();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        static void DrawOutput(CodegenSettings s)
        {
            s.Namespace = EditorGUILayout.DelayedTextField(Content.Namespace, s.Namespace);

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
                var previous = s.OutputFolder;
                s.OutputFolder = EditorGUILayout.DelayedTextField(Content.OutputFolder, s.OutputFolder);
                if (s.OutputFolder != previous)
                    OnOutputFolderChanged();
                if (GUILayout.Button(Content.Browse, EditorStyles.miniButton, GUILayout.Width(70)))
                    PickOutputFolder(s);
            }

            string folder;
            string folderError;
            if (!OutputPath.TryNormalize(s.OutputFolder, out folder, out folderError))
            {
                EditorGUILayout.HelpBox(folderError, MessageType.Error);
            }
            else if (OutputPath.HasEditorSegment(folder))
            {
                EditorGUILayout.HelpBox(
                    "The folder 'Editor' is part of the path. Generated runtime classes will compile into the editor " +
                    "assembly and will not be available in player builds.",
                    MessageType.Warning);
            }
            else
            {
                DrawOwningAssembly(s, folder);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Owning assembly definition
        // ---------------------------------------------------------------------------------------------------

        static string s_OwnerCacheKey;
        static OwningAssembly s_OwnerCache;
        static bool s_ProjectChangeHooked;

        /// <summary>The owning assembly of the configured folder, cached until the project or settings change.</summary>
        static OwningAssembly CurrentOwner(CodegenSettings s, string folder)
        {
            if (!s_ProjectChangeHooked)
            {
                s_ProjectChangeHooked = true;
                EditorApplication.projectChanged += () => s_OwnerCacheKey = null;
            }

            var required = AssemblyOwnership.RequiredReferences(s);
            var key = folder + "|" + s.GenerateAsmdef + "|" + s.LastAsmdefPath + "|" + string.Join(",", required);
            if (key != s_OwnerCacheKey)
            {
                s_OwnerCacheKey = key;
                s_OwnerCache = s.GenerateAsmdef ? null : AssemblyOwnership.Find(folder, required, s.LastAsmdefPath);
            }
            return s_OwnerCache;
        }

        static void DrawOwningAssembly(CodegenSettings s, string folder)
        {
            var owner = CurrentOwner(s, folder);
            if (owner == null)
                return;

            if (owner.MissingReferences.Count == 0)
            {
                EditorGUILayout.HelpBox($"Generated code compiles into the assembly '{owner.Name}'.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(OwnerProblem(owner), MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!owner.IsEditable))
                {
                    if (GUILayout.Button("Add References", EditorStyles.miniButtonLeft))
                        ApplyOwnerChoice(s, owner, addReferences: true);
                }
                if (GUILayout.Button("Use Separate .asmdef", EditorStyles.miniButtonRight))
                    ApplyOwnerChoice(s, owner, addReferences: false);
            }
        }

        static string OwnerProblem(OwningAssembly owner)
        {
            var location = owner.AsmdefPath ?? "an unknown assembly definition";
            var via = owner.AsmrefPath != null ? $" through '{owner.AsmrefPath}'" : "";
            var text = $"The output folder belongs to the assembly '{owner.Name}' ({location}){via}. Generated code " +
                       "compiles into it, but it does not reference:\n- " +
                       string.Join("\n- ", owner.MissingReferences.ToArray());
            if (!owner.IsEditable)
                text += "\n\nThat assembly definition is read-only, so use a separate .asmdef or another folder.";
            return text;
        }

        static void OnOutputFolderChanged()
        {
            // Dialogs cannot open in the middle of an IMGUI layout pass.
            if (!Application.isBatchMode)
                EditorApplication.delayCall += PromptForOwningAssembly;
        }

        /// <summary>Asks how to make the generated code compile when the folder lies below another asmdef.</summary>
        internal static void PromptForOwningAssembly()
        {
            var s = CodegenSettings.instance;
            string folder;
            string error;
            if (s.GenerateAsmdef || !OutputPath.TryNormalize(s.OutputFolder, out folder, out error))
                return;

            var owner = AssemblyOwnership.Find(folder, AssemblyOwnership.RequiredReferences(s), s.LastAsmdefPath);
            if (owner == null || owner.MissingReferences.Count == 0)
                return;

            if (!owner.IsEditable)
            {
                if (EditorUtility.DisplayDialog("Localization Codegen", OwnerProblem(owner),
                        "Use Separate .asmdef", "Not Now"))
                    ApplyOwnerChoice(s, owner, addReferences: false);
                return;
            }

            var choice = EditorUtility.DisplayDialogComplex(
                "Localization Codegen",
                OwnerProblem(owner) + $"\n\nAdd the missing references to {Path.GetFileName(owner.AsmdefPath)}, " +
                "or give the generated code its own .asmdef?",
                "Add References",
                "Not Now",
                "Use Separate .asmdef");

            if (choice == 0)
                ApplyOwnerChoice(s, owner, addReferences: true);
            else if (choice == 2)
                ApplyOwnerChoice(s, owner, addReferences: false);
        }

        static void ApplyOwnerChoice(CodegenSettings s, OwningAssembly owner, bool addReferences)
        {
            if (addReferences)
            {
                if (AssemblyOwnership.AddMissingReferences(owner))
                    Debug.Log($"[Localization Codegen] Added {string.Join(", ", owner.MissingReferences.ToArray())} " +
                              $"to {owner.AsmdefPath}.");
            }
            else
            {
                s.GenerateAsmdef = true;
                s.SaveSettings();
                CodegenHooks.MarkDirtyExternal();
            }

            s_OwnerCacheKey = null;
        }

        static void DrawTables(CodegenSettings s)
        {
            s.GenerateStringTables = EditorGUILayout.Toggle(Content.StringTables, s.GenerateStringTables);
            s.GenerateAssetTables = EditorGUILayout.Toggle(Content.AssetTables, s.GenerateAssetTables);
            s.AutoRegenerate = EditorGUILayout.Toggle(Content.AutoRegenerate, s.AutoRegenerate);

            if (!s.GenerateStringTables && !s.GenerateAssetTables)
                EditorGUILayout.HelpBox("Both table kinds are off, so nothing will be generated.", MessageType.Warning);
        }

        static void DrawMembers(CodegenSettings s)
        {
            s.SmartStrings = (SmartStringMode)EditorGUILayout.EnumPopup(Content.SmartStrings, s.SmartStrings);
            using (new EditorGUI.DisabledScope(s.SmartStrings == SmartStringMode.Off))
            using (new EditorGUI.IndentLevelScope())
            {
                s.SmartParamTypes = (SmartParamTypeMode)EditorGUILayout.EnumPopup(Content.SmartParamTypes, s.SmartParamTypes);
            }

            // MethodsAndRefs always emits the Ref class, so the toggle is shown as on and locked.
            var refsForced = s.SmartStrings == SmartStringMode.MethodsAndRefs;
            using (new EditorGUI.DisabledScope(refsForced))
            {
                var value = EditorGUILayout.Toggle(Content.References, s.GenerateReferences || refsForced);
                if (!refsForced)
                    s.GenerateReferences = value;
            }

            s.GenerateUniTask = EditorGUILayout.Toggle(Content.UniTask, s.GenerateUniTask);
            if (s.GenerateUniTask && !UniTaskSupport.IsAvailable)
                EditorGUILayout.HelpBox(UniTaskSupport.MissingMessage, MessageType.Warning);

            s.GenerateXmlDocs = EditorGUILayout.Toggle(Content.XmlDocs, s.GenerateXmlDocs);
        }

        static void DrawNaming(CodegenSettings s)
        {
            s.Naming = (NamingConvention)EditorGUILayout.EnumPopup(Content.Naming, s.Naming);
            s.KeySeparators = EditorGUILayout.TextField(Content.KeySeparators, s.KeySeparators);
        }

        static void DrawAdvanced(CodegenSettings s)
        {
            s.GenerateAsmdef = EditorGUILayout.Toggle(Content.GenerateAsmdef, s.GenerateAsmdef);
            s.FailOnSmartStringMismatch = EditorGUILayout.Toggle(Content.FailOnMismatch, s.FailOnSmartStringMismatch);
        }

        static void PickOutputFolder(CodegenSettings s)
        {
            var previous = s.OutputFolder;
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

            // ExitGUI throws, so Draw never reaches its change check: save here.
            if (s.OutputFolder != previous)
            {
                s.SaveSettings();
                CodegenHooks.MarkDirtyExternal();
                OnOutputFolderChanged();
            }

            GUI.FocusControl(null);
            GUIUtility.ExitGUI();
        }

        /// <summary>Generation actions, drawn as an editor toolbar.</summary>
        public static void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Regenerate", EditorStyles.toolbarButton))
                    CodeGenerator.Run(new GenerateOptions { IsAutomatic = false, PromptOnMassDeletion = true });
                if (GUILayout.Button("Verify", EditorStyles.toolbarButton))
                    EditorApplication.ExecuteMenuItem("Tools/Localization Codegen/Verify (Dry run)");

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Open Output Folder", EditorStyles.toolbarButton))
                    OpenOutputFolder();
                if (GUILayout.Button("Setup Wizard", EditorStyles.toolbarButton))
                    SetupWizard.Open();
            }
        }

        /// <summary>Summary of the last run, with its warnings in a foldout.</summary>
        public static void DrawLastResult(ref bool showWarnings)
        {
            var result = CodeGenerator.LastResult;
            if (result == null)
            {
                EditorGUILayout.HelpBox("Not generated in this session yet.", MessageType.None);
                return;
            }

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
                    result.Warnings.Count > 0 ? MessageType.Warning : MessageType.Info);
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

        public static bool IsOutputFolderValid(out string reason)
            => OutputPath.TryNormalize(CodegenSettings.instance.OutputFolder, out _, out reason);

        public static bool IsNamespaceValid(string value) => NamespaceUtil.IsValid(value);
    }
}

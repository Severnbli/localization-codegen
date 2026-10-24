using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Severnbli.LocalizationCodegen.Editor
{
    /// <summary>Options for one generation run.</summary>
    public sealed class GenerateOptions
    {
        /// <summary>True when the run was triggered by a table edit rather than a user action.</summary>
        public bool IsAutomatic;

        /// <summary>Compute the result without writing or deleting anything.</summary>
        public bool DryRun;

        /// <summary>Ask before deleting all generated files when no collections are found.</summary>
        public bool PromptOnMassDeletion = true;

        /// <summary>Allow a run inside <c>-batchmode</c>.</summary>
        public bool AllowInBatchMode;
    }

    /// <summary>Result of one generation run.</summary>
    public sealed class GenerateResult
    {
        public bool Success;
        public string Error;
        public int Written;
        public int Deleted;
        public int Unchanged;
        public int Total;

        public bool WouldChange => Written + Deleted > 0;

        public readonly List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// Public entry points. The work is split into a Unity reader, a pure emitter and a file writer.
    /// </summary>
    public static class CodeGenerator
    {
        public const string Marker = CodegenText.Marker;
        public const string RuntimeNamespace = "Severnbli.LocalizationCodegen";

        const int MaxLoggedWarnings = 30;

        sealed class GeneratedFile
        {
            public string AssetPath;
            public string FullPath;
            public string Content;
            public string Hash;
        }

        /// <summary>Last completed run, used by the editor window.</summary>
        public static GenerateResult LastResult { get; private set; }

        /// <summary>Regenerates all tables. Kept for source compatibility.</summary>
        public static int Generate()
        {
            var result = Run(new GenerateOptions { IsAutomatic = false });
            return result.Success ? result.Written + result.Deleted : -1;
        }

        /// <summary>Runs generation and returns a structured result.</summary>
        public static GenerateResult Run(GenerateOptions options)
        {
            options = options ?? new GenerateOptions();
            var result = new GenerateResult();
            var settings = CodegenSettings.instance;
            settings.EnsureMigrated();

            if (Application.isBatchMode && !options.AllowInBatchMode)
            {
                result.Success = true;
                result.Warnings.Add("Skipped automatic generation in batch mode.");
                LastResult = result;
                return result;
            }

            string folder;
            string folderError;
            if (!OutputPath.TryNormalize(settings.OutputFolder, out folder, out folderError))
            {
                result.Success = false;
                result.Error = folderError;
                Debug.LogError("[Localization Codegen] " + folderError);
                LastResult = result;
                return result;
            }

            try
            {
                var emitOptions = BuildEmitOptions(settings);
                if (settings.GenerateUniTask && !emitOptions.UniTask)
                    result.Warnings.Add(UniTaskSupport.MissingMessage);

                var reader = new TableReader(emitOptions);
                var read = reader.Read(settings.GenerateStringTables, settings.GenerateAssetTables);
                result.Warnings.AddRange(read.Warnings);
                result.Total = read.Models.Count;

                CodeEmitter.AssignClassNames(read.Models, emitOptions);

                var storedHashes = ParseHashes(settings.CollectionHashes);
                // Keep the hashes of collections that failed to read, so they are not treated as removed.
                var newHashes = read.Failed == 0
                    ? new Dictionary<string, string>(StringComparer.Ordinal)
                    : new Dictionary<string, string>(storedHashes, StringComparer.Ordinal);
                var files = new List<GeneratedFile>();
                var writtenFullPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var folderFull = OutputPath.ToFullPath(folder);

                foreach (var model in read.Models)
                {
                    var fileName = model.ClassName.TrimStart('@') + ".g.cs";
                    var fullPath = Path.Combine(folderFull, fileName);
                    var assetPath = OutputPath.ToAssetPath(fullPath);
                    var hash = HashModel(model, emitOptions);
                    var key = (model.IsAsset ? "A|" : "S|") + model.Name;

                    var file = new GeneratedFile
                    {
                        AssetPath = assetPath,
                        FullPath = fullPath,
                        Hash = hash
                    };

                    // A dry run always compares real content, so a hand-edited file is reported as out of date.
                    string stored;
                    var unchangedOnDisk = !options.DryRun &&
                                          storedHashes.TryGetValue(key, out stored) && stored == hash &&
                                          File.Exists(fullPath);

                    if (unchangedOnDisk)
                    {
                        result.Unchanged++;
                    }
                    else
                    {
                        file.Content = CodeEmitter.Emit(model, emitOptions);
                    }

                    newHashes[key] = hash;
                    files.Add(file);
                    writtenFullPaths.Add(Path.GetFullPath(fullPath));
                }

                var allowCleanup = read.Failed == 0;

                if (read.Models.Count == 0 && HasPreviousOutput(settings, folderFull))
                {
                    if (options.IsAutomatic || !options.PromptOnMassDeletion || Application.isBatchMode)
                    {
                        allowCleanup = false;
                        result.Warnings.Add(
                            "No table collections were found, so previously generated files were kept. " +
                            "Run generation manually to clean them up.");
                    }
                    else if (!EditorUtility.DisplayDialog(
                                 "Localization Codegen",
                                 "No table collections were found. Delete all previously generated files?",
                                 "Delete files",
                                 "Keep files"))
                    {
                        allowCleanup = false;
                    }
                }

                if (options.DryRun)
                {
                    foreach (var file in files)
                    {
                        if (file.Content == null)
                            continue;
                        if (File.Exists(file.FullPath) &&
                            OutputPath.NormalizeNewLines(File.ReadAllText(file.FullPath)) ==
                            OutputPath.NormalizeNewLines(file.Content))
                            result.Unchanged++;
                        else
                            result.Written++;
                    }

                    if (settings.GenerateAsmdef)
                    {
                        string asmdefName;
                        var asmdefContent = BuildAsmdefContent(emitOptions, read.Models, out asmdefName);
                        var asmdefPath = Path.Combine(folderFull, asmdefName + ".asmdef");
                        if (!File.Exists(asmdefPath) ||
                            OutputPath.NormalizeNewLines(File.ReadAllText(asmdefPath)) !=
                            OutputPath.NormalizeNewLines(asmdefContent))
                            result.Written++;
                    }
                    else if (allowCleanup &&
                             !string.IsNullOrEmpty(settings.LastAsmdefPath) &&
                             File.Exists(settings.LastAsmdefPath))
                    {
                        result.Deleted++;
                    }

                    if (allowCleanup)
                        result.Deleted += CountStaleFiles(settings, folderFull, writtenFullPaths);
                }
                else
                {
                    Directory.CreateDirectory(folderFull);
                    AssetDatabase.StartAssetEditing();
                    try
                    {
                        foreach (var file in files)
                        {
                            if (file.Content == null)
                                continue;

                            var existing = File.Exists(file.FullPath) ? File.ReadAllText(file.FullPath) : null;
                            if (existing != null &&
                                OutputPath.NormalizeNewLines(existing) == OutputPath.NormalizeNewLines(file.Content))
                            {
                                result.Unchanged++;
                                continue;
                            }

                            File.WriteAllText(file.FullPath, file.Content, new UTF8Encoding(false));
                            AssetDatabase.ImportAsset(file.AssetPath);
                            result.Written++;
                        }

                        var asmdefFull = WriteAsmdefIfNeeded(settings, emitOptions, read.Models, folderFull, ref result);

                        if (allowCleanup)
                            Cleanup(settings, folderFull, writtenFullPaths, asmdefFull, result);

                        // When cleanup was skipped, keep tracking the old files so a later run can still remove them.
                        var trackedPaths = writtenFullPaths.Select(OutputPath.ToAssetPath);
                        if (!allowCleanup)
                            trackedPaths = trackedPaths.Concat(settings.GeneratedPaths
                                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p)));

                        settings.LastOutputFolder = folder;
                        settings.GeneratedPaths = trackedPaths
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(p => p, StringComparer.Ordinal)
                            .ToArray();
                        settings.CollectionHashes = newHashes
                            .OrderBy(p => p.Key, StringComparer.Ordinal)
                            .Select(p => p.Key + "|" + p.Value)
                            .ToArray();
                        settings.LastModelHash = HashModels(read.Models, emitOptions);
                        settings.SaveSettings();
                    }
                    finally
                    {
                        AssetDatabase.StopAssetEditing();
                        AssetDatabase.Refresh();
                    }
                }

                result.Success = true;
            }
            catch (Exception e)
            {
                result.Success = false;
                result.Error = e.Message;
                Debug.LogError("[Localization Codegen] Generation failed: " + e);
            }

            LogResult(result, folder, options);
            LastResult = result;
            return result;
        }

        static EmitOptions BuildEmitOptions(CodegenSettings settings)
        {
            var smart = settings.SmartStrings;
            return new EmitOptions
            {
                Namespace = NamespaceUtil.Normalize(settings.Namespace),
                UseRuntimeAlias = settings.UseRuntimeAlias,
                Refs = settings.GenerateReferences || smart == SmartStringMode.MethodsAndRefs,
                XmlDocs = settings.GenerateXmlDocs,
                SmartStrings = smart,
                Naming = settings.Naming,
                KeySeparators = settings.KeySeparators,
                SmartParamTypes = settings.SmartParamTypes,
                GenerateAsmdef = settings.GenerateAsmdef,
                // Generated code must compile, so the option only takes effect when the add-on is compiled.
                UniTask = settings.GenerateUniTask && UniTaskSupport.IsAvailable
            };
        }

        static bool HasPreviousOutput(CodegenSettings settings, string currentFolderFull)
        {
            if (settings.GeneratedPaths.Length > 0)
                return true;

            if (!string.IsNullOrEmpty(settings.LastOutputFolder))
            {
                string previous;
                if (OutputPath.TryNormalize(settings.LastOutputFolder, out previous, out _) &&
                    Directory.Exists(OutputPath.ToFullPath(previous)))
                    return true;
            }

            return Directory.Exists(currentFolderFull) &&
                   Directory.GetFiles(currentFolderFull, "*.g.cs", SearchOption.AllDirectories)
                       .Any(IsGenerated);
        }

        static bool IsGenerated(string path)
        {
            try
            {
                using (var reader = new StreamReader(path))
                    return reader.ReadLine() == Marker;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        static void Cleanup(
            CodegenSettings settings,
            string currentFolderFull,
            HashSet<string> writtenFullPaths,
            string asmdefFullPath,
            GenerateResult result)
        {
            DeleteStaleInFolder(currentFolderFull, writtenFullPaths, result);

            string previousFolder;
            if (!string.IsNullOrEmpty(settings.LastOutputFolder) &&
                OutputPath.TryNormalize(settings.LastOutputFolder, out previousFolder, out _))
            {
                var previousFull = OutputPath.ToFullPath(previousFolder);
                if (!string.Equals(previousFull.TrimEnd('/'), currentFolderFull.TrimEnd('/'),
                        StringComparison.OrdinalIgnoreCase))
                {
                    DeleteStaleInFolder(previousFull, writtenFullPaths, result);
                }
            }

            // The manifest also covers files that were moved by hand.
            foreach (var assetPath in settings.GeneratedPaths)
            {
                if (string.IsNullOrEmpty(assetPath))
                    continue;
                var full = Path.GetFullPath(assetPath);
                if (writtenFullPaths.Contains(full))
                    continue;
                if (File.Exists(full) && IsGenerated(full))
                    Delete(full, result);
            }

            if (!settings.GenerateAsmdef &&
                !string.IsNullOrEmpty(settings.LastAsmdefPath) &&
                File.Exists(settings.LastAsmdefPath))
            {
                Delete(settings.LastAsmdefPath, result);
            }
            else if (settings.GenerateAsmdef &&
                     !string.IsNullOrEmpty(settings.LastAsmdefPath) &&
                     !string.Equals(Path.GetFullPath(settings.LastAsmdefPath), Path.GetFullPath(asmdefFullPath ?? ""),
                         StringComparison.OrdinalIgnoreCase) &&
                     File.Exists(settings.LastAsmdefPath))
            {
                Delete(settings.LastAsmdefPath, result);
            }

            if (!string.IsNullOrEmpty(asmdefFullPath))
            {
                settings.LastAsmdefPath = asmdefFullPath;
            }
            else if (!settings.GenerateAsmdef)
            {
                settings.LastAsmdefPath = null;
            }
        }

        static void DeleteStaleInFolder(string folderFull, HashSet<string> writtenFullPaths, GenerateResult result)
        {
            if (!Directory.Exists(folderFull))
                return;

            foreach (var file in Directory.GetFiles(folderFull, "*.g.cs", SearchOption.AllDirectories))
            {
                if (writtenFullPaths.Contains(Path.GetFullPath(file)) || !IsGenerated(file))
                    continue;
                Delete(file, result);
            }
        }

        static void Delete(string fullPath, GenerateResult result)
        {
            var assetPath = OutputPath.ToAssetPath(fullPath);
            if (assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(assetPath);
            else
                File.Delete(fullPath);
            result.Deleted++;
        }

        static int CountStaleFiles(CodegenSettings settings, string currentFolderFull, HashSet<string> writtenFullPaths)
        {
            var count = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string previousFolder;
            var folders = new List<string> { currentFolderFull };
            if (!string.IsNullOrEmpty(settings.LastOutputFolder) &&
                OutputPath.TryNormalize(settings.LastOutputFolder, out previousFolder, out _))
                folders.Add(OutputPath.ToFullPath(previousFolder));

            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder))
                    continue;
                foreach (var file in Directory.GetFiles(folder, "*.g.cs", SearchOption.AllDirectories))
                {
                    var full = Path.GetFullPath(file);
                    if (writtenFullPaths.Contains(full) || !IsGenerated(full) || !seen.Add(full))
                        continue;
                    count++;
                }
            }

            foreach (var assetPath in settings.GeneratedPaths)
            {
                if (string.IsNullOrEmpty(assetPath))
                    continue;
                var full = Path.GetFullPath(assetPath);
                if (writtenFullPaths.Contains(full) || !seen.Add(full))
                    continue;
                if (File.Exists(full) && IsGenerated(full))
                    count++;
            }

            return count;
        }

        static string WriteAsmdefIfNeeded(
            CodegenSettings settings,
            EmitOptions options,
            List<TableModel> models,
            string folderFull,
            ref GenerateResult result)
        {
            if (!settings.GenerateAsmdef)
                return null;

            string baseName;
            var content = BuildAsmdefContent(options, models, out baseName);
            var fullPath = Path.Combine(folderFull, baseName + ".asmdef");
            var existing = File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
            if (existing == null || OutputPath.NormalizeNewLines(existing) != OutputPath.NormalizeNewLines(content))
            {
                File.WriteAllText(fullPath, content, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(OutputPath.ToAssetPath(fullPath));
                result.Written++;
            }

            return fullPath;
        }

        internal static string BuildAsmdefContent(EmitOptions options, List<TableModel> models, out string baseName)
        {
            baseName = string.IsNullOrEmpty(options.Namespace)
                ? "LocalizationCodegen.Generated"
                : options.Namespace.Replace("@", "") + ".Generated";

            var references = new SortedSet<string>(StringComparer.Ordinal)
            {
                "com.severnbli.localization-codegen.Runtime",
                "Unity.Localization"
            };

            if (options.UniTask)
            {
                references.Add(UniTaskSupport.RuntimeAssemblyName);
                references.Add(UniTaskSupport.UniTaskAssemblyName);
            }

            foreach (var model in models)
                foreach (var assembly in model.AssetAssemblies)
                    references.Add(assembly);

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("    \"name\": ").Append(CodegenText.Literal(baseName)).Append(",\n");
            sb.Append("    \"references\": [\n");
            var referenceList = references.ToList();
            for (var i = 0; i < referenceList.Count; i++)
            {
                sb.Append("        ").Append(CodegenText.Literal(referenceList[i]));
                if (i < referenceList.Count - 1)
                    sb.Append(',');
                sb.Append('\n');
            }
            sb.Append("    ],\n");
            sb.Append("    \"includePlatforms\": [],\n");
            sb.Append("    \"excludePlatforms\": [],\n");
            sb.Append("    \"allowUnsafeCode\": false,\n");
            sb.Append("    \"overrideReferences\": false,\n");
            sb.Append("    \"precompiledReferences\": [],\n");
            sb.Append("    \"autoReferenced\": true,\n");
            sb.Append("    \"defineConstraints\": [],\n");
            sb.Append("    \"versionDefines\": [],\n");
            sb.Append("    \"noEngineReferences\": false\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        static Dictionary<string, string> ParseHashes(string[] values)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var value in values ?? new string[0])
            {
                if (string.IsNullOrEmpty(value))
                    continue;
                var last = value.LastIndexOf('|');
                if (last <= 0 || last == value.Length - 1)
                    continue;
                result[value.Substring(0, last)] = value.Substring(last + 1);
            }
            return result;
        }

        internal static string HashModel(TableModel model, EmitOptions options)
        {
            var sb = new StringBuilder();
            sb.Append(options.Signature()).Append('|')
              .Append(model.IsAsset ? "A|" : "S|")
              .Append(model.Name).Append('|')
              .Append(model.ClassName).Append('|');

            foreach (var entry in model.Entries.OrderBy(e => e.Id))
            {
                sb.Append(entry.Id).Append(':')
                  .Append(entry.Key).Append(':')
                  .Append(entry.AssetTypeName).Append(':');
                if (options.XmlDocs)
                    sb.Append(entry.Preview).Append(':');
                foreach (var param in entry.Params)
                {
                    sb.Append(param.IsPositional ? param.Index.ToString() : param.SourceName).Append('=')
                      .Append(param.ClrName).Append(':')
                      .Append(param.TypeName).Append(';');
                }
                sb.Append('\n');
            }

            return StableHash.Compute(sb.ToString());
        }

        internal static string HashModels(List<TableModel> models, EmitOptions options)
        {
            var sb = new StringBuilder();
            foreach (var model in models.OrderBy(m => m.Name, StringComparer.Ordinal).ThenBy(m => m.IsAsset))
            {
                sb.Append(HashModel(model, options)).Append('|');
            }
            return StableHash.Compute(sb.ToString());
        }

        /// <summary>Cheap staleness check used when the editor loads (A6).</summary>
        public static string ComputeModelHash()
        {
            try
            {
                var settings = CodegenSettings.instance;
                settings.EnsureMigrated();
                var options = BuildEmitOptions(settings);
                var reader = new TableReader(options);
                var read = reader.Read(settings.GenerateStringTables, settings.GenerateAssetTables);
                CodeEmitter.AssignClassNames(read.Models, options);
                return HashModels(read.Models, options);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Logs which entries would change from a property to a method when smart strings are enabled.</summary>
        public static void LogPropertyToMethodChanges()
        {
            try
            {
                var settings = CodegenSettings.instance;
                var options = BuildEmitOptions(settings);
                options.SmartStrings = SmartStringMode.Methods;
                var reader = new TableReader(options);
                var read = reader.Read(true, false);
                var changed = read.Models
                    .SelectMany(m => m.Entries.Where(e => e.Params.Count > 0).Select(e => m.Name + "." + e.Key))
                    .OrderBy(v => v, StringComparer.Ordinal)
                    .ToList();

                if (changed.Count == 0)
                {
                    Debug.Log("[Localization Codegen] No existing entries change from a property to a method.");
                    return;
                }

                Debug.LogWarning(
                    "[Localization Codegen] These entries will change from a property to a method:\n- " +
                    string.Join("\n- ", changed.ToArray()));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Localization Codegen] Could not check smart string entry changes: " + e.Message);
            }
        }

        static void LogResult(GenerateResult result, string folder, GenerateOptions options)
        {
            if (!result.Success)
                return;

            var changed = result.Written + result.Deleted;
            if (options.IsAutomatic && changed == 0)
                return;

            var message = result.Error == null
                ? $"[Localization Codegen] Generated {result.Total} table class file(s) in {folder} " +
                  $"({result.Written} written, {result.Deleted} deleted, {result.Unchanged} unchanged)."
                : result.Error;

            Debug.Log(message);
            LogWarnings(result.Warnings);
        }

        static void LogWarnings(List<string> warnings)
        {
            if (warnings == null || warnings.Count == 0)
                return;

            var sb = new StringBuilder();
            sb.Append("[Localization Codegen] ").Append(warnings.Count).Append(" warning(s):");
            for (var i = 0; i < warnings.Count && i < MaxLoggedWarnings; i++)
                sb.Append("\n- ").Append(warnings[i]);
            if (warnings.Count > MaxLoggedWarnings)
                sb.Append("\n- ... ").Append(warnings.Count - MaxLoggedWarnings).Append(" more (see the window)");
            Debug.LogWarning(sb.ToString());
        }

        public static string ToIdentifier(string value) => CodegenText.ToIdentifier(value);

        // ---------------------------------------------------------------------------------------------------
        // Batch mode entry points (B7).
        // ---------------------------------------------------------------------------------------------------

        [MenuItem("Tools/Localization Codegen/Regenerate")]
        static void RegenerateMenu()
        {
            Run(new GenerateOptions { IsAutomatic = false });
        }

        [MenuItem("Tools/Localization Codegen/Verify (Dry run)")]
        static void VerifyMenu()
        {
            var result = Run(new GenerateOptions
            {
                IsAutomatic = false,
                DryRun = true,
                PromptOnMassDeletion = false
            });
            if (result.Success)
                Debug.Log(result.WouldChange
                    ? "[Localization Codegen] Generated code is out of date."
                    : "[Localization Codegen] Generated code is up to date.");
        }

        /// <summary>Call with <c>-executeMethod Severnbli.LocalizationCodegen.Editor.CodeGenerator.GenerateBatch</c>.</summary>
        public static void GenerateBatch()
        {
            var result = Run(new GenerateOptions
            {
                IsAutomatic = false,
                PromptOnMassDeletion = false,
                AllowInBatchMode = true
            });
            EditorApplication.Exit(result.Success ? 0 : 1);
        }

        /// <summary>Fails when the generated code on disk does not match the tables.</summary>
        public static void CheckBatch()
        {
            var settings = CodegenSettings.instance;
            var result = Run(new GenerateOptions
            {
                IsAutomatic = false,
                DryRun = true,
                PromptOnMassDeletion = false,
                AllowInBatchMode = true
            });

            var failed = !result.Success || result.WouldChange;
            if (settings.FailOnSmartStringMismatch &&
                result.Warnings.Any(w => w.IndexOf("placeholder", StringComparison.OrdinalIgnoreCase) >= 0))
                failed = true;

            EditorApplication.Exit(failed ? 1 : 0);
        }
    }
}


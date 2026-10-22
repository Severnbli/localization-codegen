using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Severnbli.LocalizationCodegen.Editor
{
    internal sealed class ReaderResult
    {
        public readonly List<TableModel> Models = new List<TableModel>();
        public readonly List<string> Warnings = new List<string>();
        public int Failed;
    }

    /// <summary>
    /// Reads Unity Localization table collections into pure <see cref="TableModel"/> instances.
    /// This is the only generation stage that touches UnityEditor.
    /// </summary>
    internal sealed class TableReader
    {
        readonly EmitOptions m_Options;
        readonly HashSet<string> m_PlayerAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> m_AssemblyCSharpReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> m_GlobalVariableGroups = new HashSet<string>(StringComparer.Ordinal);

        public TableReader(EmitOptions options)
        {
            m_Options = options ?? throw new ArgumentNullException(nameof(options));
            CollectAssemblies(options);
            CollectGlobalVariableGroups();
        }

        public ReaderResult Read(bool readStringTables, bool readAssetTables)
        {
            var result = new ReaderResult();

            if (readStringTables)
            {
                foreach (var collection in Sort(LocalizationEditorSettings.GetStringTableCollections()))
                {
                    try
                    {
                        result.Models.Add(ReadCollection(collection, false, result.Warnings));
                    }
                    catch (Exception e)
                    {
                        result.Failed++;
                        result.Warnings.Add($"Skipped string collection '{SafeCollectionName(collection)}': {e.Message}");
                    }
                }
            }

            if (readAssetTables)
            {
                foreach (var collection in Sort(LocalizationEditorSettings.GetAssetTableCollections()))
                {
                    try
                    {
                        result.Models.Add(ReadCollection(collection, true, result.Warnings));
                    }
                    catch (Exception e)
                    {
                        result.Failed++;
                        result.Warnings.Add($"Skipped asset collection '{SafeCollectionName(collection)}': {e.Message}");
                    }
                }
            }

            return result;
        }

        static IEnumerable<LocalizationTableCollection> Sort(IEnumerable<LocalizationTableCollection> collections)
        {
            return (collections ?? Enumerable.Empty<LocalizationTableCollection>())
                .Where(c => c != null)
                .OrderBy(c => SafeCollectionName(c), StringComparer.Ordinal);
        }

        static string SafeCollectionName(LocalizationTableCollection collection)
        {
            try
            {
                return collection != null && collection.SharedData != null
                    ? collection.TableCollectionName ?? "<unnamed>"
                    : "<unnamed>";
            }
            catch
            {
                return "<unnamed>";
            }
        }

        TableModel ReadCollection(LocalizationTableCollection collection, bool isAsset, List<string> warnings)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));
            if (collection.SharedData == null)
                throw new InvalidOperationException("The collection has no shared table data.");

            var name = collection.TableCollectionName;
            if (string.IsNullOrEmpty(name))
                throw new InvalidOperationException("The table collection has no name.");

            var model = new TableModel { Name = name, IsAsset = isAsset };
            var shared = collection.SharedData;
            var stringCollection = isAsset ? null : collection as StringTableCollection;
            var assetCollection = isAsset ? collection as AssetTableCollection : null;

            if (isAsset && assetCollection == null)
                throw new InvalidOperationException("Expected an AssetTableCollection.");

            var defaultLocale = GetDefaultLocale();
            var assetTypeError = (string)null;
            var fallbackEntries = 0;
            var fallbackReason = (string)null;

            var entries = (shared.Entries ?? new List<SharedTableData.SharedTableEntry>())
                .Where(e => e != null && !string.IsNullOrEmpty(e.Key))
                .ToList();

            AddKeyWarnings(entries, warnings, name);

            foreach (var entry in entries)
            {
                var modelEntry = new EntryModel
                {
                    Id = entry.Id,
                    Key = entry.Key
                };

                if (isAsset)
                {
                    var type = ResolveAssetType(assetCollection, entry, ref assetTypeError);
                    if (type == null)
                    {
                        modelEntry.AssetTypeName = "global::UnityEngine.Object";
                        fallbackEntries++;
                        if (fallbackReason == null)
                            fallbackReason = assetTypeError ?? "the entry has no asset type";
                    }
                    else if (!m_Options.GenerateAsmdef &&
                             !IsAssemblyReachableFromAssemblyCSharp(type.Assembly.GetName().Name))
                    {
                        modelEntry.AssetTypeName = "global::UnityEngine.Object";
                        fallbackEntries++;
                        if (fallbackReason == null)
                            fallbackReason = $"assembly '{type.Assembly.GetName().Name}' is not referenced by Assembly-CSharp";
                    }
                    else
                    {
                        modelEntry.AssetTypeName = "global::" + type.FullName.Replace('+', '.');
                        model.AssetAssemblies.Add(type.Assembly.GetName().Name);
                    }
                }
                else
                {
                    ReadSmartEntry(modelEntry, stringCollection, entry, defaultLocale, warnings, name);
                }

                model.Entries.Add(modelEntry);
            }

            if (fallbackEntries > 0)
            {
                warnings.Add(
                    $"Collection '{name}': {fallbackEntries} asset entr{(fallbackEntries == 1 ? "y falls" : "ies fall")} " +
                    $"back to UnityEngine.Object ({fallbackReason}).");
            }

            return model;
        }

        static void AddKeyWarnings(List<SharedTableData.SharedTableEntry> entries, List<string> warnings, string collectionName)
        {
            var byBase = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var baseName = CodegenText.ToIdentifier(entry.Key);
                List<string> keys;
                if (!byBase.TryGetValue(baseName, out keys))
                {
                    keys = new List<string>();
                    byBase.Add(baseName, keys);
                }
                keys.Add(entry.Key);

                if (baseName.TrimStart('@').Trim('_').Length == 0)
                    warnings.Add($"Collection '{collectionName}': key '{entry.Key}' sanitizes to '{baseName}'.");
            }

            foreach (var pair in byBase)
            {
                if (pair.Value.Count > 1)
                {
                    warnings.Add(
                        $"Collection '{collectionName}': keys {string.Join(", ", pair.Value.Select(k => "'" + k + "'").ToArray())} " +
                        $"all sanitize to '{pair.Key}'. Suffixes are assigned by entry id.");
                }
            }
        }

        Type ResolveAssetType(AssetTableCollection collection, SharedTableData.SharedTableEntry entry, ref string error)
        {
            Type type = null;
            try
            {
                type = collection.GetEntryAssetType(entry.Id);
            }
            catch (Exception e)
            {
                if (error == null)
                    error = e.Message;
                return null;
            }

            if (type == null)
                return null;

            if (!typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                if (error == null)
                    error = $"type '{type.Name}' is not a UnityEngine.Object";
                return null;
            }

            if (!IsAccessibleFromUserCode(type))
            {
                if (error == null)
                    error = $"type '{type.FullName}' is not public";
                return null;
            }

            if (!m_PlayerAssemblies.Contains(type.Assembly.GetName().Name) &&
                !IsUnityAssembly(type.Assembly.GetName().Name))
            {
                if (error == null)
                    error = $"assembly '{type.Assembly.GetName().Name}' is not a player assembly";
                return null;
            }

            return type;
        }

        static bool IsAccessibleFromUserCode(Type type)
        {
            while (type != null)
            {
                if (type.IsGenericType)
                    return false;
                if (!type.IsPublic && !type.IsNestedPublic)
                    return false;
                type = type.DeclaringType;
            }
            return true;
        }

        bool IsAssemblyReachableFromAssemblyCSharp(string assemblyName)
        {
            if (string.Equals(assemblyName, "Assembly-CSharp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(assemblyName, "Assembly-CSharp-firstpass", StringComparison.OrdinalIgnoreCase)
                || m_AssemblyCSharpReferences.Contains(assemblyName))
                return true;

            // Engine modules and Unity package runtime assemblies are available to Assembly-CSharp.
            return IsUnityAssembly(assemblyName);
        }

        static bool IsUnityAssembly(string assemblyName)
        {
            return !string.IsNullOrEmpty(assemblyName) &&
                   (assemblyName.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase) ||
                    assemblyName.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase));
        }

        void CollectAssemblies(EmitOptions options)
        {
            try
            {
                foreach (var assembly in CompilationPipeline.GetAssemblies(AssembliesType.Player))
                {
                    if (assembly == null || string.IsNullOrEmpty(assembly.name))
                        continue;

                    m_PlayerAssemblies.Add(assembly.name);
                    if (!string.Equals(assembly.name, "Assembly-CSharp", StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (var reference in assembly.assemblyReferences ?? Array.Empty<UnityEditor.Compilation.Assembly>())
                    {
                        if (reference != null && !string.IsNullOrEmpty(reference.name))
                            m_AssemblyCSharpReferences.Add(reference.name);
                    }
                    foreach (var reference in assembly.compiledAssemblyReferences ?? Array.Empty<string>())
                        m_AssemblyCSharpReferences.Add(NormalizeAssemblyReference(reference));
                }
            }
            catch
            {
                // CompilationPipeline can be unavailable during a domain reload; the read stage will retry next time.
            }
        }

        static string NormalizeAssemblyReference(string reference)
        {
            if (string.IsNullOrEmpty(reference))
                return "";
            try
            {
                return Path.GetFileNameWithoutExtension(reference);
            }
            catch
            {
                return reference;
            }
        }

        void CollectGlobalVariableGroups()
        {
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:VariablesGroupAsset"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path))
                        m_GlobalVariableGroups.Add(Path.GetFileNameWithoutExtension(path));
                }
            }
            catch
            {
                // Global variables are only used to avoid generating unnecessary parameters.
            }
        }

        Locale GetDefaultLocale()
        {
            try
            {
                return LocalizationSettings.HasSettings ? LocalizationSettings.ProjectLocale : null;
            }
            catch
            {
                return null;
            }
        }

        void ReadSmartEntry(
            EntryModel modelEntry,
            StringTableCollection collection,
            SharedTableData.SharedTableEntry sharedEntry,
            Locale defaultLocale,
            List<string> warnings,
            string collectionName)
        {
            if (collection == null)
                return;

            var referenceUsages = (List<SmartVariableUsage>)null;
            var referenceLocale = (string)null;
            var anySmart = false;

            foreach (var table in collection.StringTables)
            {
                if (table == null)
                    continue;

                var entry = table.GetEntry(sharedEntry.Id);
                if (entry == null)
                    continue;

                var localeName = table.LocaleIdentifier.Code;
                if (IsDefaultTable(table, defaultLocale))
                    modelEntry.Preview = entry.Value;

                if (!entry.IsSmart)
                    continue;

                anySmart = true;
                var parsed = SmartStringParser.Parse(entry.Value, localeName);
                if (parsed.Failed)
                {
                    warnings.Add(
                        $"Collection '{collectionName}', key '{sharedEntry.Key}', locale '{localeName}': " +
                        $"smart string could not be parsed ({parsed.Error}).");
                    continue;
                }

                if (parsed.UnknownFormatters.Count > 0)
                {
                    warnings.Add(
                        $"Collection '{collectionName}', key '{sharedEntry.Key}', locale '{localeName}': " +
                        $"unknown formatter(s) {string.Join(", ", parsed.UnknownFormatters.Distinct().ToArray())}.");
                }

                if (referenceUsages == null && IsDefaultTable(table, defaultLocale))
                {
                    referenceUsages = parsed.Usages;
                    referenceLocale = localeName;
                }

                foreach (var usage in parsed.Usages)
                    modelEntry.Usages.Add(usage);

                WarnOnPositionalGaps(parsed.Usages, warnings, collectionName, sharedEntry.Key, localeName);
            }

            if (!anySmart)
            {
                if (string.IsNullOrEmpty(modelEntry.Preview))
                    modelEntry.Preview = FindFirstValue(collection, sharedEntry.Id);
                return;
            }

            if (referenceUsages == null)
            {
                // No default locale table (or it has no entry): use the first locale that produced usages.
                referenceUsages = modelEntry.Usages
                    .GroupBy(u => u.Locale, StringComparer.Ordinal)
                    .OrderBy(g => g.Key, StringComparer.Ordinal)
                    .FirstOrDefault()?.ToList();
                referenceLocale = referenceUsages?.FirstOrDefault()?.Locale;
            }

            if (string.IsNullOrEmpty(modelEntry.Preview))
                modelEntry.Preview = FindFirstValue(collection, sharedEntry.Id);

            BuildParams(modelEntry, warnings, collectionName, sharedEntry.Key);
            ValidateLocales(modelEntry, referenceUsages, referenceLocale, warnings, collectionName, sharedEntry.Key);
        }

        static bool IsDefaultTable(StringTable table, Locale defaultLocale)
        {
            return defaultLocale != null && table.LocaleIdentifier.Equals(defaultLocale.Identifier);
        }

        static string FindFirstValue(StringTableCollection collection, long id)
        {
            foreach (var table in collection.StringTables)
            {
                var entry = table?.GetEntry(id);
                if (entry != null && !string.IsNullOrEmpty(entry.Value))
                    return entry.Value;
            }
            return null;
        }

        static void WarnOnPositionalGaps(
            List<SmartVariableUsage> usages,
            List<string> warnings,
            string collectionName,
            string key,
            string locale)
        {
            var indices = new HashSet<int>();
            var max = -1;
            foreach (var usage in usages)
            {
                if (!usage.IsPositional)
                    continue;
                indices.Add(usage.Index);
                if (usage.Index > max)
                    max = usage.Index;
            }

            if (max < 0)
                return;

            for (var i = 0; i <= max; i++)
            {
                if (!indices.Contains(i))
                {
                    warnings.Add(
                        $"Collection '{collectionName}', key '{key}', locale '{locale}': positional index '{{{i}}}' is missing.");
                    break;
                }
            }
        }

        void BuildParams(EntryModel modelEntry, List<string> warnings, string collectionName, string key)
        {
            var maxIndex = -1;
            var named = new Dictionary<string, List<SmartVariableUsage>>(StringComparer.Ordinal);

            foreach (var usage in modelEntry.Usages)
            {
                if (usage.IsPositional)
                {
                    if (usage.Index > maxIndex)
                        maxIndex = usage.Index;
                    continue;
                }

                if (string.IsNullOrEmpty(usage.SourceName))
                    continue;

                // Variables supplied by a Global Variables Group are resolved by Unity, not by us.
                if (m_GlobalVariableGroups.Contains(usage.SourceName))
                    continue;

                List<SmartVariableUsage> list;
                if (!named.TryGetValue(usage.SourceName, out list))
                {
                    list = new List<SmartVariableUsage>();
                    named.Add(usage.SourceName, list);
                }
                list.Add(usage);
            }

            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i <= maxIndex; i++)
            {
                var clrName = CodegenText.Unique("arg" + i, usedNames);
                modelEntry.Params.Add(new SmartParam
                {
                    IsPositional = true,
                    Index = i,
                    SourceName = i.ToString(),
                    ClrName = clrName,
                    TypeName = "object"
                });
            }

            foreach (var pair in named.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var typeName = InferType(pair.Value);
                var baseName = CodegenText.ToCamelIdentifier(pair.Key);
                var clrName = CodegenText.Unique(baseName, usedNames);
                modelEntry.Params.Add(new SmartParam
                {
                    IsPositional = false,
                    SourceName = pair.Key,
                    ClrName = clrName,
                    TypeName = typeName,
                    FormatterName = pair.Value.FirstOrDefault(u => !string.IsNullOrEmpty(u.FormatterName))?.FormatterName
                });

                if (baseName.TrimStart('@').Trim('_').Length == 0)
                    warnings.Add(
                        $"Collection '{collectionName}', key '{key}': variable '{pair.Key}' sanitizes to '{baseName}'.");
            }
        }

        string InferType(List<SmartVariableUsage> usages)
        {
            if (m_Options.SmartParamTypes == SmartParamTypeMode.Object)
                return "object";

            var formatters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var usage in usages)
                if (!string.IsNullOrEmpty(usage.FormatterName))
                    formatters.Add(usage.FormatterName);

            var allPlural = true;
            var allList = true;
            foreach (var formatter in formatters)
            {
                if (!string.Equals(formatter, "plural", StringComparison.OrdinalIgnoreCase))
                    allPlural = false;
                if (!string.Equals(formatter, "list", StringComparison.OrdinalIgnoreCase))
                    allList = false;
            }

            if (allPlural && formatters.Count > 0)
                return "int";
            if (allList && formatters.Count > 0)
                return "global::System.Collections.IEnumerable";
            return "object";
        }

        static void ValidateLocales(
            EntryModel entry,
            List<SmartVariableUsage> reference,
            string referenceLocale,
            List<string> warnings,
            string collectionName,
            string key)
        {
            if (reference == null || reference.Count == 0)
                return;

            var referenceSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var usage in reference)
                referenceSet.Add(UsageKey(usage));

            var locales = entry.Usages
                .GroupBy(u => u.Locale, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal);

            foreach (var locale in locales)
            {
                if (string.Equals(locale.Key, referenceLocale, StringComparison.Ordinal))
                    continue;

                var localeSet = new HashSet<string>(StringComparer.Ordinal);
                foreach (var usage in locale)
                    localeSet.Add(UsageKey(usage));

                foreach (var missing in referenceSet.Where(u => !localeSet.Contains(u)))
                    warnings.Add(
                        $"Collection '{collectionName}', key '{key}', locale '{locale.Key}': " +
                        $"placeholder '{missing}' is missing compared with '{referenceLocale}'.");

                foreach (var extra in localeSet.Where(u => !referenceSet.Contains(u)))
                    warnings.Add(
                        $"Collection '{collectionName}', key '{key}', locale '{locale.Key}': " +
                        $"placeholder '{extra}' exists only in this locale.");
            }
        }

        static string UsageKey(SmartVariableUsage usage)
        {
            return usage.IsPositional ? "{" + usage.Index + "}" : "{" + usage.SourceName + "}";
        }
    }
}

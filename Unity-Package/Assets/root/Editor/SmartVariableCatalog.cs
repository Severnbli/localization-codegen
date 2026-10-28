using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace Severnbli.LocalizationCodegen.Editor
{
    /// <summary>A smart string variable of one entry, gathered from every locale.</summary>
    internal sealed class SmartVariableInfo
    {
        /// <summary>Name as used in <see cref="SmartParameterTypes"/>: the source name, or the index of <c>{0}</c>.</summary>
        public string Name;

        public bool IsPositional;
        public int Index;
        public readonly SortedSet<string> Locales = new SortedSet<string>(StringComparer.Ordinal);
        public readonly SortedSet<string> Formatters = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        public bool UsedWithPlural => Formatters.Any(SmartParameterMapping.IsPlural);
        public bool UsedWithList => Formatters.Any(SmartParameterMapping.IsList);

        /// <summary>The type <see cref="SmartParamTypeMode.Inferred"/> would generate.</summary>
        public SmartParameterType InferredType
        {
            get
            {
                if (Formatters.Count > 0 && Formatters.All(SmartParameterMapping.IsPlural))
                    return SmartParameterType.Int;
                if (Formatters.Count > 0 && Formatters.All(SmartParameterMapping.IsList))
                    return SmartParameterType.List;
                return SmartParameterType.Object;
            }
        }

        /// <summary>Menu label, e.g. <c>count (plural; en, ru)</c>. Slashes would create submenus, so they are replaced.</summary>
        public string Label
        {
            get
            {
                var details = new List<string>();
                if (Formatters.Count > 0)
                    details.Add(string.Join(", ", Formatters));
                details.Add(Locales.Count > 0 ? string.Join(", ", Locales) : "unused");
                var name = IsPositional ? "{" + Name + "}" : Name;
                return (name + "  (" + string.Join("; ", details) + ")").Replace('/', '∕');
            }
        }
    }

    /// <summary>
    /// Lists the variables of a smart string entry the way the generator does, for the
    /// <see cref="SmartParameterTypes"/> editor.
    /// </summary>
    internal static class SmartVariableCatalog
    {
        static readonly Regex s_EntryIndex = new Regex(@"^m_Entries\.Array\.data\[(\d+)\]", RegexOptions.Compiled);

        /// <summary>
        /// Collects the variables of the given locale texts: positional ones first (gaps included, since the generated
        /// method has a parameter for them), then named ones. Variables of Global Variables Groups are skipped.
        /// </summary>
        public static List<SmartVariableInfo> Collect(
            IEnumerable<KeyValuePair<string, string>> localeTexts,
            ICollection<string> globalVariableGroups)
        {
            var positional = new SortedDictionary<int, SmartVariableInfo>();
            var named = new SortedDictionary<string, SmartVariableInfo>(StringComparer.Ordinal);

            foreach (var pair in localeTexts)
            {
                var parsed = SmartStringParser.Parse(pair.Value, pair.Key);
                if (parsed.Failed)
                    continue;

                foreach (var usage in parsed.Usages)
                {
                    SmartVariableInfo info;
                    if (usage.IsPositional)
                    {
                        if (!positional.TryGetValue(usage.Index, out info))
                        {
                            info = new SmartVariableInfo { Name = usage.Index.ToString(), IsPositional = true, Index = usage.Index };
                            positional.Add(usage.Index, info);
                        }
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(usage.SourceName) ||
                            (globalVariableGroups != null && globalVariableGroups.Contains(usage.SourceName)))
                            continue;

                        if (!named.TryGetValue(usage.SourceName, out info))
                        {
                            info = new SmartVariableInfo { Name = usage.SourceName };
                            named.Add(usage.SourceName, info);
                        }
                    }

                    info.Locales.Add(pair.Key);
                    if (!string.IsNullOrEmpty(usage.FormatterName))
                        info.Formatters.Add(usage.FormatterName);
                }
            }

            var result = new List<SmartVariableInfo>();
            var maxIndex = positional.Count > 0 ? positional.Keys.Max() : -1;
            for (var i = 0; i <= maxIndex; i++)
            {
                SmartVariableInfo info;
                result.Add(positional.TryGetValue(i, out info)
                    ? info
                    : new SmartVariableInfo { Name = i.ToString(), IsPositional = true, Index = i });
            }
            result.AddRange(named.Values);
            return result;
        }

        /// <summary>Finds a variable by a name as written in <see cref="SmartParameterTypes"/>.</summary>
        public static SmartVariableInfo Find(IEnumerable<SmartVariableInfo> variables, string name)
        {
            var normalized = SmartParameterMapping.NormalizeName(name);
            return variables.FirstOrDefault(v => string.Equals(v.Name, normalized, StringComparison.Ordinal));
        }

        /// <summary>Variables without a row yet, in display order.</summary>
        public static List<SmartVariableInfo> Missing(IEnumerable<SmartVariableInfo> variables, IEnumerable<string> existingNames)
        {
            var existing = new HashSet<string>(existingNames.Select(SmartParameterMapping.NormalizeName), StringComparer.Ordinal);
            return variables.Where(v => !existing.Contains(v.Name)).ToList();
        }

        /// <summary>
        /// Why a row would produce a warning during generation, or <see langword="null"/>.
        /// <paramref name="variable"/> is null when the name matches no variable.
        /// </summary>
        public static string Problem(string name, SmartParameterType type, SmartVariableInfo variable, bool duplicate)
        {
            if (SmartParameterMapping.NormalizeName(name).Length == 0)
                return "Pick a variable.";
            if (duplicate)
                return "This variable is listed more than once; the first row is used.";
            if (variable == null)
                return "This entry has no such variable in any locale.";
            if (variable.UsedWithPlural && !SmartParameterMapping.AcceptsPlural(type))
                return $"Used with plural, which cannot format {type}. Use a number or List.";
            if (variable.UsedWithList && !SmartParameterMapping.AcceptsList(type))
                return $"Used with list, which cannot format {type}. Use List.";
            return null;
        }

        /// <summary>
        /// Collects the variables of the shared entry a <see cref="SmartParameterTypes"/> property belongs to.
        /// Returns <see langword="null"/> when the property is not on a string table collection's shared entry.
        /// </summary>
        public static List<SmartVariableInfo> ForProperty(SerializedProperty property)
        {
            var sharedData = property.serializedObject.targetObject as SharedTableData;
            if (sharedData == null)
                return null;

            var match = s_EntryIndex.Match(property.propertyPath);
            int index;
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out index) ||
                index < 0 || index >= sharedData.Entries.Count)
                return null;

            var collection = LocalizationEditorSettings.GetCollectionForSharedTableData(sharedData) as StringTableCollection;
            if (collection == null)
                return null;

            var id = sharedData.Entries[index].Id;
            var texts = new List<KeyValuePair<string, string>>();
            foreach (var table in collection.StringTables)
            {
                var entry = table != null ? table.GetEntry(id) : null;
                if (entry != null && entry.IsSmart && !string.IsNullOrEmpty(entry.Value))
                    texts.Add(new KeyValuePair<string, string>(table.LocaleIdentifier.Code, entry.Value));
            }

            return Collect(texts, GlobalVariableGroups());
        }

        static HashSet<string> s_GlobalVariableGroups;
        static double s_GlobalVariableGroupsTime = double.MinValue;

        /// <summary>Names of the Global Variables Groups, refreshed at most every few seconds.</summary>
        static HashSet<string> GlobalVariableGroups()
        {
            if (s_GlobalVariableGroups != null && EditorApplication.timeSinceStartup - s_GlobalVariableGroupsTime < 5)
                return s_GlobalVariableGroups;

            var groups = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:VariablesGroupAsset"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path))
                        groups.Add(Path.GetFileNameWithoutExtension(path));
                }
            }
            catch
            {
                // Global variables only hide variables from the list.
            }

            s_GlobalVariableGroups = groups;
            s_GlobalVariableGroupsTime = EditorApplication.timeSinceStartup;
            return groups;
        }
    }
}

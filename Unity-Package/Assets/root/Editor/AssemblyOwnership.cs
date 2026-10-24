using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Severnbli.LocalizationCodegen.Editor
{
    /// <summary>The assembly definition that generated code compiles into when the output folder lies below one.</summary>
    internal sealed class OwningAssembly
    {
        /// <summary>Assembly name, as other asmdefs reference it.</summary>
        public string Name;

        /// <summary>Project-relative path of the .asmdef, or null when an .asmref points to an unknown assembly.</summary>
        public string AsmdefPath;

        /// <summary>The .asmref that adds the folder to the assembly, or null when an .asmdef owns it directly.</summary>
        public string AsmrefPath;

        /// <summary>Required assemblies the asmdef does not reference yet.</summary>
        public readonly List<string> MissingReferences = new List<string>();

        /// <summary>False for asmdefs inside immutable packages (registry or git), which cannot be edited.</summary>
        public bool IsEditable;
    }

    /// <summary>Finds the assembly that owns the output folder and keeps its references in sync.</summary>
    internal static class AssemblyOwnership
    {
        public const string RuntimeAssemblyName = "com.severnbli.localization-codegen.Runtime";
        public const string LocalizationAssemblyName = "Unity.Localization";
        const string GuidPrefix = "GUID:";

        [Serializable]
        sealed class AsmdefData
        {
            public string name;
            public string[] references;
        }

        [Serializable]
        sealed class AsmrefData
        {
            public string reference;
        }

        /// <summary>Assemblies generated code needs, for the given settings.</summary>
        public static List<string> RequiredReferences(bool uniTask)
        {
            var references = new List<string> { RuntimeAssemblyName, LocalizationAssemblyName };
            if (uniTask)
            {
                references.Add(UniTaskSupport.RuntimeAssemblyName);
                references.Add(UniTaskSupport.UniTaskAssemblyName);
            }
            return references;
        }

        /// <summary>The references the current settings need.</summary>
        public static List<string> RequiredReferences(CodegenSettings settings)
            => RequiredReferences(settings.GenerateUniTask && UniTaskSupport.IsAvailable);

        /// <summary>
        /// Walks up from <paramref name="folder"/> to Assets and returns the nearest owning assembly, or null when
        /// the generated code compiles into Assembly-CSharp. <paramref name="ignoredAsmdefPath"/> skips the asmdef
        /// the generator itself wrote.
        /// </summary>
        public static OwningAssembly Find(string folder, IList<string> requiredReferences, string ignoredAsmdefPath = null)
        {
            if (string.IsNullOrEmpty(folder))
                return null;

            var ignoredFull = string.IsNullOrEmpty(ignoredAsmdefPath) ? null : Path.GetFullPath(ignoredAsmdefPath);

            for (var dir = folder.Replace('\\', '/').TrimEnd('/'); !string.IsNullOrEmpty(dir); dir = ParentFolder(dir))
            {
                var full = OutputPath.ToFullPath(dir);
                if (Directory.Exists(full))
                {
                    // Unity allows one asmdef or asmref per folder; sort so a broken project is still deterministic.
                    var asmdef = Directory.GetFiles(full, "*.asmdef")
                        .Where(f => ignoredFull == null || !string.Equals(Path.GetFullPath(f), ignoredFull, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(f => f, StringComparer.Ordinal)
                        .FirstOrDefault();
                    if (asmdef != null)
                        return FromAsmdef(dir + "/" + Path.GetFileName(asmdef), null, requiredReferences);

                    var asmref = Directory.GetFiles(full, "*.asmref").OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault();
                    if (asmref != null)
                        return FromAsmref(dir + "/" + Path.GetFileName(asmref), requiredReferences);
                }

                if (dir == "Assets")
                    break;
            }

            return null;
        }

        /// <summary>
        /// Reads a project-relative file. Path.GetFullPath is what maps virtual "Packages/..." paths to their real
        /// location (the package cache, an embedded or a local package).
        /// </summary>
        static string ReadText(string assetPath) => File.ReadAllText(Path.GetFullPath(assetPath));

        static string ParentFolder(string path)
        {
            var slash = path.LastIndexOf('/');
            return slash <= 0 ? null : path.Substring(0, slash);
        }

        static OwningAssembly FromAsmref(string asmrefPath, IList<string> requiredReferences)
        {
            var data = JsonUtility.FromJson<AsmrefData>(ReadText(asmrefPath));
            var reference = data?.reference ?? "";

            string asmdefPath;
            string name;
            if (!ResolveReference(reference, out name, out asmdefPath) || asmdefPath == null)
            {
                // The referenced assembly does not exist; there is nothing to fix from here.
                return new OwningAssembly { Name = name, AsmrefPath = asmrefPath };
            }

            return FromAsmdef(asmdefPath, asmrefPath, requiredReferences);
        }

        static OwningAssembly FromAsmdef(string asmdefPath, string asmrefPath, IList<string> requiredReferences)
        {
            var data = JsonUtility.FromJson<AsmdefData>(ReadText(asmdefPath)) ?? new AsmdefData();
            var owner = new OwningAssembly
            {
                Name = string.IsNullOrEmpty(data.name) ? Path.GetFileNameWithoutExtension(asmdefPath) : data.name,
                AsmdefPath = asmdefPath,
                AsmrefPath = asmrefPath,
                IsEditable = IsEditable(asmdefPath)
            };

            var referenced = new HashSet<string>(StringComparer.Ordinal);
            foreach (var reference in data.references ?? Array.Empty<string>())
            {
                string name;
                string ignored;
                if (ResolveReference(reference, out name, out ignored))
                    referenced.Add(name);
            }

            foreach (var required in requiredReferences ?? Array.Empty<string>())
            {
                if (!referenced.Contains(required) && !string.Equals(required, owner.Name, StringComparison.Ordinal))
                    owner.MissingReferences.Add(required);
            }

            return owner;
        }

        /// <summary>Resolves an asmdef reference ("Name" or "GUID:hex") to an assembly name and asmdef path.</summary>
        static bool ResolveReference(string reference, out string name, out string asmdefPath)
        {
            name = reference;
            asmdefPath = null;
            if (string.IsNullOrEmpty(reference))
                return false;

            if (reference.StartsWith(GuidPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var path = AssetDatabase.GUIDToAssetPath(reference.Substring(GuidPrefix.Length));
                if (string.IsNullOrEmpty(path) || !File.Exists(Path.GetFullPath(path)))
                    return false;

                asmdefPath = path;
                var data = JsonUtility.FromJson<AsmdefData>(ReadText(path));
                name = string.IsNullOrEmpty(data?.name) ? Path.GetFileNameWithoutExtension(path) : data.name;
                return true;
            }

            asmdefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(reference);
            return true;
        }

        static bool IsEditable(string assetPath)
        {
            if (assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return true;

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
            return package != null &&
                   (package.source == UnityEditor.PackageManager.PackageSource.Embedded ||
                    package.source == UnityEditor.PackageManager.PackageSource.Local);
        }

        /// <summary>
        /// Adds the missing references to the owner's asmdef, keeping its formatting. References are written as
        /// GUIDs when the asmdef already uses GUID references. Returns false when nothing could be written.
        /// </summary>
        public static bool AddMissingReferences(OwningAssembly owner) => AddMissingReferences(owner, true);

        /// <param name="import">False in tests, whose asmdefs live in folders Unity does not import.</param>
        internal static bool AddMissingReferences(OwningAssembly owner, bool import)
        {
            if (owner == null || owner.AsmdefPath == null || !owner.IsEditable || owner.MissingReferences.Count == 0)
                return false;

            var fullPath = Path.GetFullPath(owner.AsmdefPath);
            var raw = File.ReadAllBytes(fullPath);
            var hasBom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
            var json = new UTF8Encoding(false).GetString(raw, hasBom ? 3 : 0, raw.Length - (hasBom ? 3 : 0));

            var data = JsonUtility.FromJson<AsmdefData>(json) ?? new AsmdefData();
            var useGuids = (data.references ?? Array.Empty<string>())
                .Any(r => r.StartsWith(GuidPrefix, StringComparison.OrdinalIgnoreCase));

            var entries = owner.MissingReferences.Select(name => useGuids ? ToGuidReference(name) : name).ToList();
            var updated = AddReferences(json, entries);
            File.WriteAllText(fullPath, updated, new UTF8Encoding(hasBom));
            if (import)
                AssetDatabase.ImportAsset(owner.AsmdefPath);
            return true;
        }

        static string ToGuidReference(string assemblyName)
        {
            var path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);
            var guid = string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
            return string.IsNullOrEmpty(guid) ? assemblyName : GuidPrefix + guid;
        }

        static readonly Regex s_ReferencesArray =
            new Regex("(?<key>\"references\"\\s*:\\s*)\\[(?<items>[^\\]]*)\\]", RegexOptions.Compiled);

        /// <summary>
        /// Appends <paramref name="references"/> to the "references" array of an asmdef, creating the array when
        /// missing. Pure text editing, so indentation, line endings and every other field stay as they were.
        /// </summary>
        internal static string AddReferences(string json, IList<string> references)
        {
            if (references == null || references.Count == 0)
                return json;

            var newline = json.Contains("\r\n") ? "\r\n" : "\n";
            var quoted = references.Select(CodegenText.Literal).ToList();
            var match = s_ReferencesArray.Match(json);

            if (!match.Success)
            {
                // Insert the array as the first property after the opening brace.
                var open = json.IndexOf('{');
                if (open < 0)
                    throw new FormatException("The assembly definition is not a JSON object.");

                var propertyIndent = DetectPropertyIndent(json, open);
                var array = "\"references\": [" + newline +
                            string.Join("," + newline, quoted.Select(q => propertyIndent + propertyIndent + q)) +
                            newline + propertyIndent + "],";
                return json.Insert(open + 1, newline + propertyIndent + array);
            }

            var items = match.Groups["items"];
            var keyIndent = LineIndent(json, match.Index);
            if (items.Value.Trim().Length == 0)
            {
                var itemIndent = keyIndent + IndentUnit(keyIndent);
                var array = "[" + newline +
                            string.Join("," + newline, quoted.Select(q => itemIndent + q)) +
                            newline + keyIndent + "]";
                return json.Substring(0, items.Index - 1) + array + json.Substring(items.Index + items.Length + 1);
            }

            // Append after the last existing element, matching its layout.
            var lastElementEnd = items.Index + items.Value.TrimEnd().Length;
            string insertion;
            if (items.Value.Contains("\n"))
            {
                var itemIndent = LineIndent(json, lastElementEnd - 1);
                insertion = string.Concat(quoted.Select(q => "," + newline + itemIndent + q));
            }
            else
            {
                insertion = string.Concat(quoted.Select(q => ", " + q));
            }

            return json.Insert(lastElementEnd, insertion);
        }

        static string LineIndent(string text, int index)
        {
            var lineStart = text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
            var end = lineStart;
            while (end < text.Length && (text[end] == ' ' || text[end] == '\t'))
                end++;
            return text.Substring(lineStart, end - lineStart);
        }

        static string DetectPropertyIndent(string json, int openBrace)
        {
            var quote = json.IndexOf('"', openBrace);
            if (quote < 0)
                return "    ";
            var indent = LineIndent(json, quote);
            return indent.Length > 0 ? indent : "    ";
        }

        static string IndentUnit(string keyIndent)
        {
            if (keyIndent.Length == 0)
                return "    ";
            return keyIndent[0] == '\t' ? "\t" : keyIndent;
        }
    }
}

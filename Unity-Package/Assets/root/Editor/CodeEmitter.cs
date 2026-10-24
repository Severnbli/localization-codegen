using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Severnbli.LocalizationCodegen.Editor
{
    /// <summary>
    /// Pure C# emitter. Takes a <see cref="TableModel"/> and produces the generated file contents.
    /// Contains no UnityEditor dependencies so it can be unit tested.
    /// </summary>
    internal static class CodeEmitter
    {
        const string AssetObject = "global::UnityEngine.Object";
        const string LocalizedStringType = "global::UnityEngine.Localization.LocalizedString";
        const string LocalizedAssetType = "global::UnityEngine.Localization.LocalizedAsset";

        /// <summary>Assigns deterministic, collision-free class names to every model.</summary>
        public static void AssignClassNames(IList<TableModel> models, EmitOptions options)
        {
            var stringNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var model in models)
                if (!model.IsAsset)
                    stringNames.Add(model.Name ?? "");

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LocalizedTables" };
            if (options.UseRuntimeAlias)
                used.Add(options.RuntimeAlias ?? EmitOptions.DefaultRuntimeAlias);

            var reserved = ReservedClassNames(options);
            var ordered = models
                .OrderBy(m => m.Name ?? "", StringComparer.Ordinal)
                .ThenBy(m => m.IsAsset ? 1 : 0)
                .ToList();

            foreach (var model in ordered)
            {
                var baseName = options.Naming == NamingConvention.PascalCase
                    ? CodegenText.ToPascalCase(model.Name)
                    : CodegenText.ToIdentifier(model.Name);

                if (model.IsAsset && stringNames.Contains(model.Name ?? ""))
                    baseName += "Assets";

                if (reserved.Contains(baseName.TrimStart('@')))
                    baseName += "Table";

                if (CodegenText.IsWindowsReservedFileName(baseName))
                    baseName += "_";

                model.ClassName = CodegenText.Unique(baseName, used);
            }
        }

        static HashSet<string> ReservedClassNames(EmitOptions options)
        {
            var set = new HashSet<string>(StringComparer.Ordinal)
            {
                "System", "UnityEngine", "UnityEditor", "Unity", "Microsoft", "Mono", "Severnbli"
            };

            var first = NamespaceUtil.FirstSegment(options.Namespace);
            if (!string.IsNullOrEmpty(first))
                set.Add(first.TrimStart('@'));
            return set;
        }

        /// <summary>Emits a single generated file.</summary>
        public static string Emit(TableModel model, EmitOptions options)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            var className = string.IsNullOrEmpty(model.ClassName)
                ? CodegenText.ToIdentifier(model.Name)
                : model.ClassName;

            var sb = new StringBuilder();
            sb.Append(CodegenText.Marker).Append('\n');
            sb.Append("// Table collection: ").Append(CodegenText.SanitizeComment(model.Name)).Append('\n');

            if (options.UseRuntimeAlias)
            {
                sb.Append("using ").Append(options.RuntimeAlias).Append(" = ")
                  .Append(EmitOptions.RuntimeTypeName).Append(";\n");
            }

            var hasNamespace = !string.IsNullOrEmpty(options.Namespace);
            if (hasNamespace)
            {
                if (options.UseRuntimeAlias)
                    sb.Append('\n');
                sb.Append("namespace ").Append(NamespaceUtil.Declaration(options.Namespace)).Append('\n');
                sb.Append("{\n");
            }

            var root = BuildScopeTree(model, options);
            root.ClassName = className;
            AssignMemberNames(root, options);

            var rootLevel = hasNamespace ? 1 : 0;
            EmitScope(sb, root, model, options, rootLevel, true);

            if (hasNamespace)
                sb.Append("}\n");

            return sb.ToString();
        }

        sealed class Scope
        {
            public string RawSegment;
            public string ClassName;
            public readonly Dictionary<string, Scope> ChildLookup = new Dictionary<string, Scope>(StringComparer.Ordinal);
            public readonly List<Scope> Children = new List<Scope>();
            public readonly List<EntryModel> Entries = new List<EntryModel>();
            public readonly Dictionary<EntryModel, string> RawMemberNames = new Dictionary<EntryModel, string>();
            public readonly Dictionary<EntryModel, string> MemberNames = new Dictionary<EntryModel, string>();
            public readonly HashSet<string> MemberUsed = new HashSet<string>(StringComparer.Ordinal);
        }

        static Scope BuildScopeTree(TableModel model, EmitOptions options)
        {
            var root = new Scope();
            var separators = string.IsNullOrEmpty(options.KeySeparators) ? null : options.KeySeparators.ToCharArray();

            foreach (var entry in model.Entries)
            {
                if (string.IsNullOrEmpty(entry.Key))
                    continue;

                var scope = root;
                var leafName = entry.Key;
                if (separators != null)
                {
                    var parts = entry.Key.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                        leafName = parts[parts.Length - 1];

                    for (var i = 0; i < parts.Length - 1; i++)
                    {
                        var segment = parts[i];
                        Scope child;
                        if (!scope.ChildLookup.TryGetValue(segment, out child))
                        {
                            child = new Scope { RawSegment = segment };
                            scope.ChildLookup.Add(segment, child);
                            scope.Children.Add(child);
                        }
                        scope = child;
                    }
                }

                scope.Entries.Add(entry);
                scope.RawMemberNames[entry] = leafName;
            }

            return root;
        }

        static void AssignMemberNames(Scope scope, EmitOptions options)
        {
            var strict = options.Naming == NamingConvention.PascalCase || !string.IsNullOrEmpty(options.KeySeparators);

            // Nested class names claim their names in this scope first.
            scope.Children.Sort((a, b) => string.CompareOrdinal(a.RawSegment, b.RawSegment));
            var childClassNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var child in scope.Children)
            {
                var baseName = options.Naming == NamingConvention.PascalCase
                    ? CodegenText.ToPascalCase(child.RawSegment)
                    : CodegenText.ToIdentifier(child.RawSegment, strict);
                child.ClassName = CodegenText.Unique(baseName, childClassNames);
            }

            // Members must not hide TableName, Ref, the runtime alias, object members, nested classes or the
            // enclosing type name.
            scope.MemberUsed.Clear();
            scope.MemberUsed.Add("TableName");
            scope.MemberUsed.Add("Ref");
            if (options.UseRuntimeAlias)
                scope.MemberUsed.Add(options.RuntimeAlias ?? EmitOptions.DefaultRuntimeAlias);
            foreach (var name in CodegenText.ObjectMemberNames)
                scope.MemberUsed.Add(name);
            foreach (var name in childClassNames)
                scope.MemberUsed.Add(name);
            if (!string.IsNullOrEmpty(scope.ClassName))
                scope.MemberUsed.Add(scope.ClassName.TrimStart('@'));

            var groups = new Dictionary<string, List<EntryModel>>(StringComparer.Ordinal);
            foreach (var entry in scope.Entries)
            {
                var baseName = BaseMemberName(scope.RawMemberNames[entry], options, strict);
                List<EntryModel> group;
                if (!groups.TryGetValue(baseName, out group))
                {
                    group = new List<EntryModel>();
                    groups.Add(baseName, group);
                }
                group.Add(entry);
            }

            // Assign collision suffixes by entry id so older keys keep their names.
            foreach (var pair in groups)
            {
                var group = pair.Value;
                group.Sort((a, b) => a.Id != b.Id ? a.Id.CompareTo(b.Id) : string.CompareOrdinal(a.Key, b.Key));
                foreach (var entry in group)
                    scope.MemberNames[entry] = CodegenText.Unique(pair.Key, scope.MemberUsed);
            }

            foreach (var child in scope.Children)
                AssignMemberNames(child, options);
        }

        static string BaseMemberName(string key, EmitOptions options, bool strict)
        {
            if (options.Naming == NamingConvention.PascalCase)
                return CodegenText.ToPascalCase(key);
            return CodegenText.ToIdentifier(key, strict);
        }

        static void EmitScope(StringBuilder sb, Scope scope, TableModel model, EmitOptions options, int level, bool isRoot)
        {
            sb.Append(Indent(level)).Append("public static class ").Append(scope.ClassName).Append('\n');
            sb.Append(Indent(level)).Append("{\n");

            if (isRoot)
            {
                sb.Append(Indent(level + 1)).Append("public const string TableName = ")
                  .Append(CodegenText.Literal(model.Name)).Append(";\n");
            }

            var orderedEntries = scope.Entries
                .OrderBy(e => e.Key ?? "", StringComparer.Ordinal)
                .ThenBy(e => e.Id)
                .ToList();

            foreach (var entry in orderedEntries)
            {
                sb.Append('\n');
                EmitEntry(sb, entry, scope.MemberNames[entry], options, level + 1);
            }

            if (isRoot && options.Refs)
            {
                sb.Append('\n');
                EmitRefClass(sb, model, options, level + 1);
            }

            foreach (var child in scope.Children.OrderBy(c => c.ClassName, StringComparer.Ordinal))
            {
                sb.Append('\n');
                EmitScope(sb, child, model, options, level + 1, false);
            }

            sb.Append(Indent(level)).Append("}\n");
        }

        static void EmitEntry(StringBuilder sb, EntryModel entry, string name, EmitOptions options, int level)
        {
            if (options.XmlDocs && !string.IsNullOrEmpty(entry.Preview))
                EmitSummary(sb, entry.Preview, level);

            var runtime = RuntimeType(options);
            if (!IsAssetEntry(entry))
            {
                var positional = entry.Params.Where(p => p.IsPositional).OrderBy(p => p.Index).ToList();
                var named = entry.Params.Where(p => !p.IsPositional).OrderBy(p => p.SourceName, StringComparer.Ordinal).ToList();

                if (options.SmartStrings != SmartStringMode.Off && entry.Params.Count > 0)
                {
                    EmitParameterDocs(sb, positional, named, level);
                    sb.Append(Indent(level)).Append("public static string ").Append(name)
                      .Append('(').Append(ParameterList(positional, named)).Append(")\n");
                    sb.Append(Indent(level)).Append("    => ").Append(runtime).Append('.')
                      .Append(BuildStringCall(entry.Key, positional, named)).Append(";\n");
                }
                else
                {
                    sb.Append(Indent(level)).Append("public static string ").Append(name)
                      .Append(" => ").Append(runtime).Append(".GetString(TableName, ")
                      .Append(CodegenText.Literal(entry.Key)).Append(");\n");
                }
            }
            else
            {
                var type = string.IsNullOrEmpty(entry.AssetTypeName) ? AssetObject : entry.AssetTypeName;
                sb.Append(Indent(level)).Append("public static ").Append(type).Append(' ').Append(name)
                  .Append(" => ").Append(runtime).Append(".GetAsset<").Append(type)
                  .Append(">(TableName, ").Append(CodegenText.Literal(entry.Key)).Append(");\n");
            }
        }

        static bool IsAssetEntry(EntryModel entry) => entry.AssetTypeName != null;

        static void EmitRefClass(StringBuilder sb, TableModel model, EmitOptions options, int level)
        {
            var strict = !string.IsNullOrEmpty(options.KeySeparators);
            sb.Append(Indent(level)).Append("public static class Ref\n");
            sb.Append(Indent(level)).Append("{\n");

            var used = new HashSet<string>(StringComparer.Ordinal) { "TableName", "Ref" };
            foreach (var objectMember in CodegenText.ObjectMemberNames)
                used.Add(objectMember);

            var entries = model.Entries
                .Where(e => !string.IsNullOrEmpty(e.Key))
                .OrderBy(e => e.Key, StringComparer.Ordinal)
                .ThenBy(e => e.Id)
                .ToList();

            foreach (var entry in entries)
            {
                sb.Append('\n');
                var name = CodegenText.Unique(BaseMemberName(entry.Key, options, strict), used);

                if (options.XmlDocs && !string.IsNullOrEmpty(entry.Preview))
                    EmitSummary(sb, entry.Preview, level + 1);

                if (!IsAssetEntry(entry))
                {
                    var positional = entry.Params.Where(p => p.IsPositional).OrderBy(p => p.Index).ToList();
                    var named = entry.Params.Where(p => !p.IsPositional).OrderBy(p => p.SourceName, StringComparer.Ordinal).ToList();

                    if (entry.Params.Count > 0 && options.SmartStrings == SmartStringMode.MethodsAndRefs)
                    {
                        EmitParameterDocs(sb, positional, named, level + 1);
                        sb.Append(Indent(level + 1)).Append("public static ").Append(LocalizedStringType).Append(' ')
                          .Append(name).Append('(').Append(ParameterList(positional, named)).Append(")\n");
                        sb.Append(Indent(level + 1)).Append("    => ").Append(RuntimeType(options))
                          .Append(".CreateReference(TableName, ").Append(CodegenText.Literal(entry.Key))
                          .Append(", ").Append(PositionalArray(positional)).Append(", ")
                          .Append(NamedDictionary(named)).Append(");\n");
                    }
                    else if (entry.Params.Count == 0)
                    {
                        sb.Append(Indent(level + 1)).Append("public static ").Append(LocalizedStringType).Append(' ')
                          .Append(name).Append(" => new ").Append(LocalizedStringType)
                          .Append("(TableName, ").Append(CodegenText.Literal(entry.Key)).Append(");\n");
                    }
                    else
                    {
                        // A smart entry with parameters cannot be represented without arguments; omit it from Ref.
                        used.Remove(name);
                    }
                }
                else
                {
                    // Unlike LocalizedString, LocalizedAsset types only have a parameterless constructor.
                    var type = string.IsNullOrEmpty(entry.AssetTypeName) ? AssetObject : entry.AssetTypeName;
                    sb.Append(Indent(level + 1)).Append("public static ").Append(GetLocalizedAssetType(type)).Append(' ')
                      .Append(name).Append(" => new ").Append(GetLocalizedAssetType(type))
                      .Append(" { TableReference = TableName, TableEntryReference = ")
                      .Append(CodegenText.Literal(entry.Key)).Append(" };\n");
                }
            }

            sb.Append(Indent(level)).Append("}\n");
        }

        static string GetLocalizedAssetType(string assetType)
        {
            switch (assetType)
            {
                case "global::UnityEngine.AudioClip": return "global::UnityEngine.Localization.LocalizedAudioClip";
                case "global::UnityEngine.GameObject": return "global::UnityEngine.Localization.LocalizedGameObject";
                case "global::UnityEngine.Mesh": return "global::UnityEngine.Localization.LocalizedMesh";
                case "global::UnityEngine.Material": return "global::UnityEngine.Localization.LocalizedMaterial";
                case "global::UnityEngine.Object": return "global::UnityEngine.Localization.LocalizedObject";
                case "global::UnityEngine.Sprite": return "global::UnityEngine.Localization.LocalizedSprite";
                case "global::UnityEngine.Texture": return "global::UnityEngine.Localization.LocalizedTexture";
                case "global::UnityEngine.Font": return "global::UnityEngine.Localization.LocalizedFont";
                case "global::TMPro.TMP_FontAsset": return "global::UnityEngine.Localization.LocalizedTmpFont";
                case "global::UnityEngine.TextCore.Text.FontAsset": return "global::UnityEngine.Localization.LocalizedTextCoreFontAsset";
                default: return LocalizedAssetType + "<" + assetType + ">";
            }
        }

        static string ParameterList(List<SmartParam> positional, List<SmartParam> named)
        {
            var parts = new List<string>();
            foreach (var p in positional)
                parts.Add(p.TypeName + " " + p.ClrName);
            foreach (var p in named)
                parts.Add(p.TypeName + " " + p.ClrName);
            return string.Join(", ", parts.ToArray());
        }

        static string BuildStringCall(string key, List<SmartParam> positional, List<SmartParam> named)
        {
            var keyLiteral = CodegenText.Literal(key);
            if (positional.Count == 0 && named.Count == 0)
                return "GetString(TableName, " + keyLiteral + ")";

            if (positional.Count == 0)
                return "GetString(TableName, " + keyLiteral + ", " + NamedDictionary(named) + ")";

            if (named.Count == 0)
            {
                var args = string.Join(", ", positional.Select(p => p.ClrName).ToArray());
                return "GetString(TableName, " + keyLiteral + ", " + args + ")";
            }

            return "GetStringMixed(TableName, " + keyLiteral + ", " +
                   PositionalArray(positional) + ", " + NamedDictionary(named) + ")";
        }

        static string PositionalArray(List<SmartParam> positional)
        {
            if (positional.Count == 0)
                return "null";
            var args = string.Join(", ", positional.Select(p => p.ClrName).ToArray());
            return "new object[] { " + args + " }";
        }

        static string NamedDictionary(List<SmartParam> named)
        {
            if (named.Count == 0)
                return "null";

            var sb = new StringBuilder();
            sb.Append("new global::System.Collections.Generic.Dictionary<string, object> { ");
            for (var i = 0; i < named.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append('[').Append(CodegenText.Literal(named[i].SourceName)).Append("] = ")
                  .Append(named[i].ClrName);
            }
            sb.Append(" }");
            return sb.ToString();
        }

        static void EmitParameterDocs(StringBuilder sb, List<SmartParam> positional, List<SmartParam> named, int level)
        {
            foreach (var p in positional)
                sb.Append(Indent(level)).Append("/// <param name=\"").Append(p.ClrName)
                  .Append("\">positional {").Append(p.Index).Append("}</param>\n");
            foreach (var p in named)
                sb.Append(Indent(level)).Append("/// <param name=\"").Append(p.ClrName)
                  .Append("\">named {").Append(CodegenText.EscapeXml(p.SourceName)).Append("}</param>\n");
        }

        static void EmitSummary(StringBuilder sb, string preview, int level)
        {
            // Truncate before escaping so an XML entity such as &amp; is never cut in half.
            var singleLine = preview.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
            var text = CodegenText.EscapeXml(CodegenText.SanitizeComment(CodegenText.Truncate(singleLine, 120)));
            sb.Append(Indent(level)).Append("/// <summary>").Append(text).Append("</summary>\n");
        }

        static string RuntimeType(EmitOptions options)
        {
            return options.UseRuntimeAlias ? options.RuntimeAlias : EmitOptions.RuntimeTypeName;
        }

        static string Indent(int level) => new string(' ', level * 4);
    }
}

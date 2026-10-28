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
        const string AssetObject = "UnityEngine.Object";
        const string LocalizedStringType = "UnityEngine.Localization.LocalizedString";
        const string LocalizedAssetType = "UnityEngine.Localization.LocalizedAsset";
        const string UniTaskType = "Cysharp.Threading.Tasks.UniTask";
        const string CancellationTokenType = "System.Threading.CancellationToken";
        const string DictionaryType = "System.Collections.Generic.Dictionary";
        const string TaskType = "System.Threading.Tasks.Task";
        const string StringTableType = "UnityEngine.Localization.Tables.StringTable";
        const string AssetTableType = "UnityEngine.Localization.Tables.AssetTable";
        const string CancellationTokenName = "cancellationToken";

        /// <summary>
        /// Simple names of every type the emitter itself can reference. Generated classes in the same namespace would
        /// hide them, so table classes never take these names.
        /// </summary>
        static readonly string[] s_ReferencedTypeNames =
        {
            "LocalizedTables", "LocalizedTablesUniTask", "Object", "LocalizedString", "LocalizedAsset",
            "LocalizedAudioClip", "LocalizedGameObject", "LocalizedMesh", "LocalizedMaterial", "LocalizedObject",
            "LocalizedSprite", "LocalizedTexture", "LocalizedFont", "LocalizedTmpFont", "LocalizedTextCoreFontAsset",
            "UniTask", "CancellationToken", "Dictionary", "IEnumerable"
        };

        /// <summary>
        /// Types only the <c>LoadTable</c> methods refer to. They are reserved only while the option is on, so turning
        /// it on is the only thing that can rename a table class with one of these names.
        /// </summary>
        static readonly string[] s_LoadTableTypeNames = { "Task", "StringTable", "AssetTable" };

        /// <summary>Assigns deterministic, collision-free class names to every model.</summary>
        public static void AssignClassNames(IList<TableModel> models, EmitOptions options)
        {
            var stringNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var model in models)
                if (!model.IsAsset)
                    stringNames.Add(model.Name ?? "");

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LocalizedTables", "LocalizedTablesUniTask" };
            var reserved = ReservedClassNames(models, options);
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

        static HashSet<string> ReservedClassNames(IList<TableModel> models, EmitOptions options)
        {
            var set = new HashSet<string>(StringComparer.Ordinal)
            {
                "System", "UnityEngine", "UnityEditor", "Unity", "Microsoft", "Mono", "Severnbli", "Cysharp", "TMPro"
            };

            // Generated files refer to types by their simple names, which a table class must not hide.
            set.UnionWith(s_ReferencedTypeNames);
            if (options.LoadTable)
                set.UnionWith(s_LoadTableTypeNames);
            foreach (var model in models)
            {
                foreach (var entry in model.Entries)
                {
                    if (!string.IsNullOrEmpty(entry.AssetTypeName))
                        set.Add(TypeNames.FirstSegment(TypeNames.SimpleName(entry.AssetTypeName, entry.AssetTypeNamespace)));
                    foreach (var param in entry.Params)
                        if (param.TypeName != null && param.TypeName.IndexOf('.') >= 0)
                            set.Add(TypeNames.FirstSegment(TypeNames.SimpleName(param.TypeName, null)));
                }
            }

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

            var root = BuildScopeTree(model, options);
            root.ClassName = className;
            AssignMemberNames(root, options);

            // Claimed after every key's name, so turning the option on never renames an existing member.
            if (options.LoadTable)
            {
                root.LoadTableName = CodegenText.Unique("LoadTable", root.MemberUsed);
                if (options.UniTask)
                    root.LoadTableAsyncName = CodegenText.Unique("LoadTableAsync", root.MemberUsed);
            }

            var hasNamespace = !string.IsNullOrEmpty(options.Namespace);
            var rootLevel = hasNamespace ? 1 : 0;

            // The first pass only records which types the file refers to, so the second can pick their names.
            var types = new TypeNames();
            EmitScope(new StringBuilder(), root, model, options, types, rootLevel, true);
            var declared = new HashSet<string>(StringComparer.Ordinal);
            CollectClassNames(root, declared);
            if (options.Refs)
                declared.Add("Ref");
            var usings = types.Resolve(options, declared);

            var sb = new StringBuilder();
            sb.Append(CodegenText.Marker).Append('\n');
            sb.Append("// Table collection: ").Append(CodegenText.SanitizeComment(model.Name)).Append('\n');

            foreach (var ns in usings)
                sb.Append("using ").Append(ns).Append(";\n");

            if (hasNamespace)
            {
                if (usings.Count > 0)
                    sb.Append('\n');
                sb.Append("namespace ").Append(NamespaceUtil.Declaration(options.Namespace)).Append('\n');
                sb.Append("{\n");
            }

            EmitScope(sb, root, model, options, types, rootLevel, true);

            if (hasNamespace)
                sb.Append("}\n");

            return sb.ToString();
        }

        static void CollectClassNames(Scope scope, HashSet<string> names)
        {
            names.Add(scope.ClassName.TrimStart('@'));
            foreach (var child in scope.Children)
                CollectClassNames(child, names);
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
            public readonly Dictionary<EntryModel, string> AsyncMemberNames = new Dictionary<EntryModel, string>();
            public readonly HashSet<string> MemberUsed = new HashSet<string>(StringComparer.Ordinal);
            public string LoadTableName;
            public string LoadTableAsyncName;
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

            // Members must not hide TableName, Ref, the runtime classes, object members, nested classes or the
            // enclosing type name.
            scope.MemberUsed.Clear();
            scope.MemberUsed.Add("TableName");
            scope.MemberUsed.Add("Ref");
            scope.MemberUsed.Add("LocalizedTables");
            if (options.UniTask)
                scope.MemberUsed.Add("LocalizedTablesUniTask");
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

            // Async names are claimed after every key's own name, so adding UniTask members never renames an
            // existing member. A key that already ends in "Async" pushes the generated variant to a suffix.
            if (options.UniTask)
            {
                var byId = scope.Entries
                    .OrderBy(e => e.Id)
                    .ThenBy(e => e.Key, StringComparer.Ordinal);
                foreach (var entry in byId)
                {
                    scope.AsyncMemberNames[entry] =
                        CodegenText.Unique(scope.MemberNames[entry].TrimStart('@') + "Async", scope.MemberUsed);
                }
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

        static void EmitScope(StringBuilder sb, Scope scope, TableModel model, EmitOptions options, TypeNames types, int level, bool isRoot)
        {
            sb.Append(Indent(level)).Append("public static class ").Append(scope.ClassName).Append('\n');
            sb.Append(Indent(level)).Append("{\n");

            if (isRoot)
            {
                sb.Append(Indent(level + 1)).Append("public const string TableName = ")
                  .Append(CodegenText.Literal(model.Name)).Append(";\n");

                if (scope.LoadTableName != null)
                    EmitLoadTable(sb, scope, model, options, types, level + 1);
            }

            var orderedEntries = scope.Entries
                .OrderBy(e => e.Key ?? "", StringComparer.Ordinal)
                .ThenBy(e => e.Id)
                .ToList();

            foreach (var entry in orderedEntries)
            {
                sb.Append('\n');
                EmitEntry(sb, entry, scope.MemberNames[entry], options, types, level + 1);

                string asyncName;
                if (scope.AsyncMemberNames.TryGetValue(entry, out asyncName))
                {
                    sb.Append('\n');
                    EmitAsyncEntry(sb, entry, asyncName, options, types, level + 1);
                }
            }

            if (isRoot && options.Refs)
            {
                sb.Append('\n');
                EmitRefClass(sb, model, options, types, level + 1);
            }

            foreach (var child in scope.Children.OrderBy(c => c.ClassName, StringComparer.Ordinal))
            {
                sb.Append('\n');
                EmitScope(sb, child, model, options, types, level + 1, false);
            }

            sb.Append(Indent(level)).Append("}\n");
        }

        static void EmitEntry(StringBuilder sb, EntryModel entry, string name, EmitOptions options, TypeNames types, int level)
        {
            if (options.XmlDocs && !string.IsNullOrEmpty(entry.Preview))
                EmitSummary(sb, entry.Preview, level);

            var runtime = types.Name(EmitOptions.RuntimeTypeName);
            if (!IsAssetEntry(entry))
            {
                var positional = entry.Params.Where(p => p.IsPositional).OrderBy(p => p.Index).ToList();
                var named = entry.Params.Where(p => !p.IsPositional).OrderBy(p => p.SourceName, StringComparer.Ordinal).ToList();

                if (options.SmartStrings != SmartStringMode.Off && entry.Params.Count > 0)
                {
                    EmitParameterDocs(sb, positional, named, level);
                    sb.Append(Indent(level)).Append("public static string ").Append(name)
                      .Append('(').Append(ParameterList(positional, named, types)).Append(")\n");
                    sb.Append(Indent(level)).Append("    => ").Append(runtime).Append('.')
                      .Append(BuildStringCall(entry.Key, positional, named, types)).Append(";\n");
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
                var type = AssetType(entry, types);
                sb.Append(Indent(level)).Append("public static ").Append(type).Append(' ').Append(name)
                  .Append(" => ").Append(runtime).Append(".GetAsset<").Append(type)
                  .Append(">(TableName, ").Append(CodegenText.Literal(entry.Key)).Append(");\n");
            }
        }

        static bool IsAssetEntry(EntryModel entry) => entry.AssetTypeName != null;

        static string AssetType(EntryModel entry, TypeNames types)
        {
            return string.IsNullOrEmpty(entry.AssetTypeName)
                ? types.Name(AssetObject)
                : types.Name(entry.AssetTypeName, entry.AssetTypeNamespace);
        }

        /// <summary>Emits the UniTask variant of an entry, mirroring the parameters of its synchronous member.</summary>
        static void EmitAsyncEntry(StringBuilder sb, EntryModel entry, string name, EmitOptions options, TypeNames types, int level)
        {
            if (options.XmlDocs && !string.IsNullOrEmpty(entry.Preview))
                EmitSummary(sb, entry.Preview, level);

            var runtime = types.Name(EmitOptions.UniTaskRuntimeTypeName);
            var uniTask = types.Name(UniTaskType);
            var cancellationToken = types.Name(CancellationTokenType);
            if (IsAssetEntry(entry))
            {
                var type = AssetType(entry, types);
                sb.Append(Indent(level)).Append("public static ").Append(uniTask).Append('<').Append(type).Append("> ")
                  .Append(name).Append('(').Append(cancellationToken).Append(' ').Append(CancellationTokenName)
                  .Append(" = default)\n");
                sb.Append(Indent(level)).Append("    => ").Append(runtime).Append(".GetAssetAsync<").Append(type)
                  .Append(">(TableName, ").Append(CodegenText.Literal(entry.Key)).Append(", ")
                  .Append(CancellationTokenName).Append(");\n");
                return;
            }

            var withParams = options.SmartStrings != SmartStringMode.Off && entry.Params.Count > 0;
            var positional = withParams
                ? entry.Params.Where(p => p.IsPositional).OrderBy(p => p.Index).ToList()
                : new List<SmartParam>();
            var named = withParams
                ? entry.Params.Where(p => !p.IsPositional).OrderBy(p => p.SourceName, StringComparer.Ordinal).ToList()
                : new List<SmartParam>();

            // A smart string variable may itself be called cancellationToken.
            var parameterNames = new HashSet<string>(entry.Params.Select(p => p.ClrName.TrimStart('@')), StringComparer.Ordinal);
            var tokenName = withParams ? CodegenText.Unique(CancellationTokenName, parameterNames) : CancellationTokenName;

            if (withParams)
            {
                EmitParameterDocs(sb, positional, named, level);
                sb.Append(Indent(level)).Append("/// <param name=\"").Append(tokenName)
                  .Append("\">Stops waiting for the table to load.</param>\n");
            }

            var parameters = ParameterList(positional, named, types);
            sb.Append(Indent(level)).Append("public static ").Append(uniTask).Append("<string> ").Append(name).Append('(')
              .Append(parameters).Append(parameters.Length > 0 ? ", " : "")
              .Append(cancellationToken).Append(' ').Append(tokenName).Append(" = default)\n");
            sb.Append(Indent(level)).Append("    => ").Append(runtime).Append(".GetStringAsync(TableName, ")
              .Append(CodegenText.Literal(entry.Key)).Append(", ");
            if (withParams)
                sb.Append(PositionalArray(positional)).Append(", ").Append(NamedDictionary(named, types)).Append(", ");
            sb.Append(tokenName).Append(");\n");
        }

        /// <summary>Emits <c>LoadTable</c> and, with UniTask, <c>LoadTableAsync</c>.</summary>
        static void EmitLoadTable(StringBuilder sb, Scope scope, TableModel model, EmitOptions options, TypeNames types, int level)
        {
            var table = types.Name(model.IsAsset ? AssetTableType : StringTableType);
            var kind = model.IsAsset ? "Asset" : "String";

            sb.Append('\n');
            if (options.XmlDocs)
                sb.Append(Indent(level)).Append("/// <summary>Loads the table for the selected locale without blocking.</summary>\n");
            sb.Append(Indent(level)).Append("public static ").Append(types.Name(TaskType)).Append('<').Append(table).Append("> ")
              .Append(scope.LoadTableName).Append("() => ").Append(types.Name(EmitOptions.RuntimeTypeName))
              .Append(".Load").Append(kind).Append("Table(TableName);\n");

            if (scope.LoadTableAsyncName == null)
                return;

            sb.Append('\n');
            if (options.XmlDocs)
                sb.Append(Indent(level)).Append("/// <summary>Loads the table for the selected locale without blocking.</summary>\n");
            sb.Append(Indent(level)).Append("public static ").Append(types.Name(UniTaskType)).Append('<').Append(table).Append("> ")
              .Append(scope.LoadTableAsyncName).Append('(').Append(types.Name(CancellationTokenType)).Append(' ')
              .Append(CancellationTokenName).Append(" = default)\n");
            sb.Append(Indent(level)).Append("    => ").Append(types.Name(EmitOptions.UniTaskRuntimeTypeName))
              .Append(".Load").Append(kind).Append("TableAsync(TableName, ").Append(CancellationTokenName).Append(");\n");
        }

        static void EmitRefClass(StringBuilder sb, TableModel model, EmitOptions options, TypeNames types, int level)
        {
            var strict = !string.IsNullOrEmpty(options.KeySeparators);
            sb.Append(Indent(level)).Append("public static class Ref\n");
            sb.Append(Indent(level)).Append("{\n");

            var used = new HashSet<string>(StringComparer.Ordinal) { "TableName", "Ref", "LocalizedTables" };
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
                        sb.Append(Indent(level + 1)).Append("public static ").Append(types.Name(LocalizedStringType)).Append(' ')
                          .Append(name).Append('(').Append(ParameterList(positional, named, types)).Append(")\n");
                        sb.Append(Indent(level + 1)).Append("    => ").Append(types.Name(EmitOptions.RuntimeTypeName))
                          .Append(".CreateReference(TableName, ").Append(CodegenText.Literal(entry.Key))
                          .Append(", ").Append(PositionalArray(positional)).Append(", ")
                          .Append(NamedDictionary(named, types)).Append(");\n");
                    }
                    else if (entry.Params.Count == 0)
                    {
                        var localizedString = types.Name(LocalizedStringType);
                        sb.Append(Indent(level + 1)).Append("public static ").Append(localizedString).Append(' ')
                          .Append(name).Append(" => new ").Append(localizedString)
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
                    var type = GetLocalizedAssetType(entry, types);
                    sb.Append(Indent(level + 1)).Append("public static ").Append(type).Append(' ')
                      .Append(name).Append(" => new ").Append(type)
                      .Append(" { TableReference = TableName, TableEntryReference = ")
                      .Append(CodegenText.Literal(entry.Key)).Append(" };\n");
                }
            }

            sb.Append(Indent(level)).Append("}\n");
        }

        static string GetLocalizedAssetType(EntryModel entry, TypeNames types)
        {
            string localized;
            switch (string.IsNullOrEmpty(entry.AssetTypeName) ? AssetObject : entry.AssetTypeName)
            {
                case "UnityEngine.AudioClip": localized = "UnityEngine.Localization.LocalizedAudioClip"; break;
                case "UnityEngine.GameObject": localized = "UnityEngine.Localization.LocalizedGameObject"; break;
                case "UnityEngine.Mesh": localized = "UnityEngine.Localization.LocalizedMesh"; break;
                case "UnityEngine.Material": localized = "UnityEngine.Localization.LocalizedMaterial"; break;
                case "UnityEngine.Object": localized = "UnityEngine.Localization.LocalizedObject"; break;
                case "UnityEngine.Sprite": localized = "UnityEngine.Localization.LocalizedSprite"; break;
                case "UnityEngine.Texture": localized = "UnityEngine.Localization.LocalizedTexture"; break;
                case "UnityEngine.Font": localized = "UnityEngine.Localization.LocalizedFont"; break;
                case "TMPro.TMP_FontAsset": localized = "UnityEngine.Localization.LocalizedTmpFont"; break;
                case "UnityEngine.TextCore.Text.FontAsset": localized = "UnityEngine.Localization.LocalizedTextCoreFontAsset"; break;
                default: return types.Name(LocalizedAssetType) + "<" + AssetType(entry, types) + ">";
            }
            return types.Name(localized);
        }

        static string ParameterList(List<SmartParam> positional, List<SmartParam> named, TypeNames types)
        {
            var parts = new List<string>();
            foreach (var p in positional)
                parts.Add(types.Name(p.TypeName) + " " + p.ClrName);
            foreach (var p in named)
                parts.Add(types.Name(p.TypeName) + " " + p.ClrName);
            return string.Join(", ", parts.ToArray());
        }

        static string BuildStringCall(string key, List<SmartParam> positional, List<SmartParam> named, TypeNames types)
        {
            var keyLiteral = CodegenText.Literal(key);
            if (positional.Count == 0 && named.Count == 0)
                return "GetString(TableName, " + keyLiteral + ")";

            if (positional.Count == 0)
                return "GetString(TableName, " + keyLiteral + ", " + NamedDictionary(named, types) + ")";

            if (named.Count == 0)
            {
                var args = string.Join(", ", positional.Select(p => p.ClrName).ToArray());
                return "GetString(TableName, " + keyLiteral + ", " + args + ")";
            }

            return "GetStringMixed(TableName, " + keyLiteral + ", " +
                   PositionalArray(positional) + ", " + NamedDictionary(named, types) + ")";
        }

        static string PositionalArray(List<SmartParam> positional)
        {
            if (positional.Count == 0)
                return "null";
            var args = string.Join(", ", positional.Select(p => p.ClrName).ToArray());
            return "new object[] { " + args + " }";
        }

        static string NamedDictionary(List<SmartParam> named, TypeNames types)
        {
            if (named.Count == 0)
                return "null";

            var sb = new StringBuilder();
            sb.Append("new ").Append(types.Name(DictionaryType)).Append("<string, object> { ");
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

        static string Indent(int level) => new string(' ', level * 4);

        /// <summary>
        /// Decides how one file writes the types it refers to. A type is written by its simple name and its namespace
        /// is imported with a using directive, unless that name could bind to another type; then it stays
        /// <c>global::</c> qualified.
        /// </summary>
        internal sealed class TypeNames
        {
            // Full name -> namespace of every type the file refers to.
            readonly Dictionary<string, string> m_Namespaces = new Dictionary<string, string>(StringComparer.Ordinal);

            // Full name -> written name; null while recording.
            Dictionary<string, string> m_Written;

            /// <summary>
            /// Returns the name to write for a type. <paramref name="ns"/> is the type's namespace; when null it is
            /// everything before the last dot. Names without a dot and namespace (keywords such as object) pass through.
            /// </summary>
            public string Name(string fullName, string ns = null)
            {
                if (ns == null)
                {
                    var dot = fullName.LastIndexOf('.');
                    if (dot < 0)
                        return fullName;
                    ns = fullName.Substring(0, dot);
                }

                if (m_Written == null)
                {
                    m_Namespaces[fullName] = ns;
                    return fullName;
                }
                return m_Written[fullName];
            }

            /// <summary>
            /// Picks the written name of every recorded type and returns the namespaces to import, sorted with System
            /// namespaces first. <paramref name="declared"/> holds the type names the file itself declares.
            /// </summary>
            public List<string> Resolve(EmitOptions options, HashSet<string> declared)
            {
                // Namespaces whose types a simple name can bind to: the generated namespace, its parents and the imports.
                var enclosing = new List<string> { "" };
                if (!string.IsNullOrEmpty(options.Namespace))
                {
                    var segments = options.Namespace.Split('.').Select(s => s.TrimStart('@')).ToArray();
                    for (var i = 1; i <= segments.Length; i++)
                        enclosing.Add(string.Join(".", segments, 0, i));
                    // A simple name that matches a segment of the generated namespace binds to that namespace.
                    declared = new HashSet<string>(declared, StringComparer.Ordinal);
                    declared.UnionWith(segments);
                }

                var lookup = new HashSet<string>(enclosing, StringComparer.Ordinal);
                lookup.UnionWith(m_Namespaces.Values);

                m_Written = new Dictionary<string, string>(StringComparer.Ordinal);
                var usings = new HashSet<string>(StringComparer.Ordinal);
                foreach (var pair in m_Namespaces)
                {
                    var ns = pair.Value;
                    var simple = SimpleName(pair.Key, ns);
                    var first = FirstSegment(simple);

                    var conflict = declared.Contains(first) ||
                                   m_Namespaces.Any(o => o.Value != ns && FirstSegment(SimpleName(o.Key, o.Value)) == first) ||
                                   (options.TypeExists != null && lookup.Any(n => n != ns && options.TypeExists(n, first)));

                    if (conflict)
                    {
                        m_Written[pair.Key] = "global::" + pair.Key;
                        continue;
                    }

                    m_Written[pair.Key] = simple;
                    if (!enclosing.Contains(ns))
                        usings.Add(ns);
                }

                return usings
                    .OrderBy(n => n == "System" || n.StartsWith("System.", StringComparison.Ordinal) ? 0 : 1)
                    .ThenBy(n => n, StringComparer.Ordinal)
                    .ToList();
            }

            /// <summary>The name of a type relative to its namespace, e.g. <c>Outer.Inner</c> for a nested type.</summary>
            public static string SimpleName(string fullName, string ns)
            {
                if (ns == null)
                {
                    var dot = fullName.LastIndexOf('.');
                    return dot < 0 ? fullName : fullName.Substring(dot + 1);
                }
                return ns.Length == 0 ? fullName : fullName.Substring(ns.Length + 1);
            }

            public static string FirstSegment(string simpleName)
            {
                var dot = simpleName.IndexOf('.');
                return dot < 0 ? simpleName : simpleName.Substring(0, dot);
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace Severnbli.LocalizationCodegen.Editor
{
    /// <summary>How smart string entries are emitted.</summary>
    public enum SmartStringMode
    {
        Off = 0,
        Methods = 1,
        MethodsAndRefs = 2
    }

    /// <summary>How generated member names are built from table keys.</summary>
    public enum NamingConvention
    {
        Keep = 0,
        PascalCase = 1
    }

    /// <summary>How smart string parameter types are inferred.</summary>
    public enum SmartParamTypeMode
    {
        Object = 0,
        Inferred = 1
    }

    /// <summary>A table collection reduced to the information the emitter needs.</summary>
    internal sealed class TableModel
    {
        public string Name;
        public string ClassName;
        public bool IsAsset;
        public readonly List<EntryModel> Entries = new List<EntryModel>();
        public readonly HashSet<string> AssetAssemblies = new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>One table entry reduced to the information the emitter needs.</summary>
    internal sealed class EntryModel
    {
        public long Id;
        public string Key;
        public string AssetTypeName;
        public string Preview;
        public readonly List<SmartParam> Params = new List<SmartParam>();
        public readonly List<SmartVariableUsage> Usages = new List<SmartVariableUsage>();
    }

    /// <summary>A parameter required by a smart string.</summary>
    internal sealed class SmartParam
    {
        /// <summary>The variable name as written in the smart string, e.g. <c>player-name</c>.</summary>
        public string SourceName;

        public bool IsPositional;
        public int Index;
        public string ClrName;
        public string TypeName;
        public string FormatterName;
    }

    /// <summary>A variable usage found in one locale of one entry (used for validation).</summary>
    internal sealed class SmartVariableUsage
    {
        public string Locale;
        public bool IsPositional;
        public int Index;
        public string SourceName;
        public string FormatterName;
    }

    /// <summary>Options controlling the pure emitter.</summary>
    internal sealed class EmitOptions
    {
        public const string DefaultRuntimeAlias = "__LT";
        public const string RuntimeTypeName = "global::Severnbli.LocalizationCodegen.LocalizedTables";
        public const string UniTaskRuntimeTypeName = "global::Severnbli.LocalizationCodegen.LocalizedTablesUniTask";

        /// <summary>Normalized namespace, or <see langword="null"/> for the global namespace.</summary>
        public string Namespace;

        public bool UseRuntimeAlias = true;
        public string RuntimeAlias = DefaultRuntimeAlias;
        public bool Refs;
        public bool XmlDocs;
        public SmartStringMode SmartStrings = SmartStringMode.Off;
        public NamingConvention Naming = NamingConvention.Keep;
        public string KeySeparators = "";
        public SmartParamTypeMode SmartParamTypes = SmartParamTypeMode.Object;

        /// <summary>When true, an asmdef is generated and unreferenced player assemblies may be used.</summary>
        public bool GenerateAsmdef;

        /// <summary>When true, every member also gets a UniTask-returning <c>...Async</c> variant.</summary>
        public bool UniTask;

        /// <summary>Stable signature of every option that can change output.</summary>
        public string Signature()
        {
            // UniTask is appended only when enabled, so existing hashes (and files) stay valid after upgrading.
            return (UniTask ? "unitask|" : "") + string.Join("|", new[]
            {
                Namespace ?? "<global>",
                UseRuntimeAlias ? "alias" : "noalias",
                RuntimeAlias ?? DefaultRuntimeAlias,
                RuntimeTypeName,
                Refs ? "refs" : "norefs",
                XmlDocs ? "docs" : "nodocs",
                SmartStrings.ToString(),
                Naming.ToString(),
                KeySeparators ?? "",
                SmartParamTypes.ToString(),
                GenerateAsmdef ? "asmdef" : "noasmdef"
            });
        }
    }

    /// <summary>Small deterministic FNV-1a hash used for incremental generation.</summary>
    internal static class StableHash
    {
        public static string Compute(string value)
        {
            unchecked
            {
                const ulong offset = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                var hash = offset;
                foreach (var c in value ?? "")
                {
                    hash ^= c;
                    hash *= prime;
                }
                return hash.ToString("x16");
            }
        }
    }
}

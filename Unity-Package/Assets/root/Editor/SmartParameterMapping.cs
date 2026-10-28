using System;
using System.Collections.Generic;
using System.Linq;

namespace Severnbli.LocalizationCodegen.Editor
{
    /// <summary>
    /// Applies <see cref="SmartParameterTypes"/> metadata to the parameters of a smart string entry
    /// (<see cref="SmartParamTypeMode.Mapping"/>).
    /// </summary>
    internal static class SmartParameterMapping
    {
        /// <summary>C# type name emitted for a mapped type.</summary>
        public static string TypeName(SmartParameterType type)
        {
            switch (type)
            {
                case SmartParameterType.String: return "string";
                case SmartParameterType.Int: return "int";
                case SmartParameterType.Long: return "long";
                case SmartParameterType.Float: return "float";
                case SmartParameterType.Double: return "double";
                case SmartParameterType.Decimal: return "decimal";
                case SmartParameterType.Bool: return "bool";
                case SmartParameterType.Char: return "char";
                case SmartParameterType.DateTime: return "System.DateTime";
                case SmartParameterType.List: return "System.Collections.IEnumerable";
                default: return "object";
            }
        }

        /// <summary>
        /// Whether the plural formatter can format a value of this type. It takes numbers and collections; strings,
        /// booleans, dates and characters (whose <c>ToDecimal</c> throws) make it fail at runtime.
        /// </summary>
        public static bool AcceptsPlural(SmartParameterType type)
        {
            switch (type)
            {
                case SmartParameterType.Int:
                case SmartParameterType.Long:
                case SmartParameterType.Float:
                case SmartParameterType.Double:
                case SmartParameterType.Decimal:
                case SmartParameterType.List:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Whether the list formatter can format a value of this type.</summary>
        public static bool AcceptsList(SmartParameterType type) => type == SmartParameterType.List;

        /// <summary>
        /// Sets the type of every parameter named in <paramref name="hints"/> and reports duplicate hints, hints
        /// without a parameter, and types the plural or list formatter cannot format.
        /// </summary>
        public static void Apply(
            EntryModel entry,
            IList<SmartParameterTypes.Parameter> hints,
            List<string> warnings,
            string collectionName,
            string key)
        {
            if (hints == null || hints.Count == 0)
                return;

            var prefix = $"Collection '{collectionName}', key '{key}': ";
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var hint in hints)
            {
                if (hint == null)
                    continue;

                var name = NormalizeName(hint.Name);
                if (name.Length == 0)
                {
                    warnings.Add(prefix + "Smart Parameter Types has a parameter without a name.");
                    continue;
                }

                if (!seen.Add(name))
                {
                    warnings.Add(prefix + $"Smart Parameter Types lists '{name}' more than once; the first type is used.");
                    continue;
                }

                var param = entry.Params.FirstOrDefault(p => string.Equals(p.SourceName, name, StringComparison.Ordinal));
                if (param == null)
                {
                    warnings.Add(prefix + $"Smart Parameter Types lists '{name}', which is not a parameter of this entry.");
                    continue;
                }

                param.TypeName = TypeName(hint.Type);

                var formatters = FormattersOf(entry, param);
                if (formatters.Any(IsPlural) && !AcceptsPlural(hint.Type))
                {
                    warnings.Add(prefix +
                        $"variable '{name}' is declared as {hint.Type} but used with plural, which needs a number " +
                        "or a collection. Plural will not format it at runtime.");
                }

                if (formatters.Any(IsList) && !AcceptsList(hint.Type))
                {
                    warnings.Add(prefix +
                        $"variable '{name}' is declared as {hint.Type} but used with list, which needs a collection. " +
                        "List will not format it at runtime.");
                }
            }
        }

        /// <summary>Accepts <c>count</c>, <c>0</c> and <c>{0}</c>.</summary>
        static string NormalizeName(string name)
        {
            return (name ?? "").Trim().TrimStart('{').TrimEnd('}').Trim();
        }

        static List<string> FormattersOf(EntryModel entry, SmartParam param)
        {
            return entry.Usages
                .Where(u => param.IsPositional
                    ? u.IsPositional && u.Index == param.Index
                    : !u.IsPositional && string.Equals(u.SourceName, param.SourceName, StringComparison.Ordinal))
                .Select(u => u.FormatterName)
                .Where(f => !string.IsNullOrEmpty(f))
                .ToList();
        }

        static bool IsPlural(string formatter) =>
            string.Equals(formatter, "plural", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(formatter, "p", StringComparison.OrdinalIgnoreCase);

        static bool IsList(string formatter) =>
            string.Equals(formatter, "list", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(formatter, "l", StringComparison.OrdinalIgnoreCase);
    }
}

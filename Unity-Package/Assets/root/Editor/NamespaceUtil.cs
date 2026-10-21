using System;
using System.Collections.Generic;
using System.Text;

namespace Severnbli.LocalizationCodegen.Editor
{
    /// <summary>Single source of truth for "global namespace" and namespace sanitizing.</summary>
    internal static class NamespaceUtil
    {
        /// <summary>
        /// Returns <see langword="null"/> for the global namespace (null, empty or whitespace input),
        /// otherwise the sanitized dotted namespace.
        /// </summary>
        public static string Normalize(string raw)
        {
            raw = (raw ?? "").Trim();
            if (raw.Length == 0)
                return null;

            var segments = new List<string>();
            foreach (var part in raw.Split('.'))
            {
                var segment = (part ?? "").Trim();
                if (segment.Length == 0)
                    continue;

                segment = CodegenText.ToIdentifier(segment, true);
                // ToIdentifier returns "_" for input that has no valid characters; keep that as a valid segment.
                segments.Add(segment);
            }

            return segments.Count == 0 ? null : string.Join(".", segments.ToArray());
        }

        /// <summary>
        /// True when the raw value is a namespace the user can reasonably type. Whitespace/no value means the
        /// global namespace and is valid.
        /// </summary>
        public static bool IsValid(string raw)
        {
            raw = (raw ?? "").Trim();
            if (raw.Length == 0)
                return true;

            foreach (var part in raw.Split('.'))
            {
                var segment = (part ?? "").Trim();
                if (segment.Length == 0)
                    return false;
                if (!char.IsLetter(segment[0]) && segment[0] != '_')
                    return false;

                for (var i = 1; i < segment.Length; i++)
                {
                    if (!char.IsLetterOrDigit(segment[i]) && segment[i] != '_')
                        return false;
                }
            }
            return true;
        }

        /// <summary>Returns true when the raw value is effectively the global namespace.</summary>
        public static bool IsGlobal(string raw) => string.IsNullOrWhiteSpace(raw);

        /// <summary>The first segment of the namespace, or <see langword="null"/>.</summary>
        public static string FirstSegment(string normalized)
        {
            if (string.IsNullOrEmpty(normalized))
                return null;
            var dot = normalized.IndexOf('.');
            return dot < 0 ? normalized : normalized.Substring(0, dot);
        }

        /// <summary>Builds the namespace declaration, keeping keyword segments escaped.</summary>
        public static string Declaration(string normalized)
        {
            var sb = new StringBuilder();
            foreach (var segment in normalized.Split('.'))
            {
                if (sb.Length > 0)
                    sb.Append('.');
                sb.Append(segment);
            }
            return sb.ToString();
        }
    }
}

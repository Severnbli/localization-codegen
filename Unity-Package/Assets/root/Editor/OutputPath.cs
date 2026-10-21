using System;
using System.IO;

namespace Severnbli.LocalizationCodegen.Editor
{
    /// <summary>Single source of truth for output-folder validation and normalization.</summary>
    internal static class OutputPath
    {
        static readonly char[] s_InvalidSegmentChars = BuildInvalidChars();

        static char[] BuildInvalidChars()
        {
            var invalid = new System.Collections.Generic.HashSet<char>(Path.GetInvalidFileNameChars());
            // Be strict even on platforms where these are technically legal, so a project stays portable.
            foreach (var c in new[] { '<', '>', ':', '"', '|', '?', '*' })
                invalid.Add(c);

            var result = new char[invalid.Count];
            invalid.CopyTo(result);
            return result;
        }

        /// <summary>
        /// Normalizes a project-relative output folder. Returns false with an error message when the folder is not a
        /// safe place below <c>Assets</c>.
        /// </summary>
        public static bool TryNormalize(string raw, out string folder, out string error)
        {
            folder = null;
            var path = (raw ?? "").Trim().Replace('\\', '/').TrimEnd('/');
            if (path.Length == 0)
            {
                error = "Output folder cannot be empty.";
                return false;
            }

            if (path != "Assets" && !path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = "Output folder must be inside the Assets folder.";
                return false;
            }

            foreach (var segment in path.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == ".." ||
                    segment.IndexOfAny(s_InvalidSegmentChars) >= 0)
                {
                    error = $"Invalid path segment '{segment}'.";
                    return false;
                }
            }

            error = null;
            folder = path;
            return true;
        }

        /// <summary>True when any segment is named Editor (case-insensitive).</summary>
        public static bool HasEditorSegment(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                return false;

            foreach (var segment in folder.Split('/'))
                if (string.Equals(segment, "Editor", StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>Converts a project-relative asset path to an absolute file-system path.</summary>
        public static string ToFullPath(string assetPath)
        {
            return Path.GetFullPath(assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>Converts an absolute file-system path to a project-relative asset path.</summary>
        public static string ToAssetPath(string fullPath)
        {
            var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return fullPath;

            var normalizedRoot = projectRoot.Replace('\\', '/').TrimEnd('/') + "/";
            var normalized = fullPath.Replace('\\', '/');
            if (normalized.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return normalized.Substring(normalizedRoot.Length);
            return normalized;
        }

        /// <summary>Normalizes line endings so a CRLF checkout does not look modified.</summary>
        public static string NormalizeNewLines(string text)
        {
            return (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
        }
    }
}

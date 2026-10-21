using System;
using System.Collections.Generic;
using UnityEngine.Localization.SmartFormat;
using UnityEngine.Localization.SmartFormat.Core.Parsing;

namespace Severnbli.LocalizationCodegen.Editor
{
    /// <summary>Result of parsing one smart string.</summary>
    internal sealed class SmartParseResult
    {
        public readonly List<SmartVariableUsage> Usages = new List<SmartVariableUsage>();
        public readonly List<string> UnknownFormatters = new List<string>();
        public bool Failed;
        public string Error;
    }

    /// <summary>
    /// Thin wrapper around Unity's own SmartFormat parser so extraction matches runtime behaviour.
    /// </summary>
    internal static class SmartStringParser
    {
        static readonly SmartFormatter s_Formatter = Smart.CreateDefaultSmartFormat();
        static readonly HashSet<string> s_FormatterNames =
            new HashSet<string>(s_Formatter.GetNotEmptyFormatterExtensionNames(), StringComparer.OrdinalIgnoreCase);
        static bool s_HadFailure;
        static string s_FailureText;

        static SmartStringParser()
        {
            s_Formatter.Parser.OnParsingFailure += (_, args) =>
            {
                s_HadFailure = true;
                s_FailureText = args?.ToString();
            };
        }

        /// <summary>Parses one format string and returns every variable it needs.</summary>
        public static SmartParseResult Parse(string text, string locale)
        {
            var result = new SmartParseResult();
            if (string.IsNullOrEmpty(text))
                return result;

            s_HadFailure = false;
            s_FailureText = null;

            try
            {
                var format = s_Formatter.Parser.ParseFormat(text, s_Formatter.GetNotEmptyFormatterExtensionNames());
                Walk(format, result, locale);
            }
            catch (Exception e)
            {
                result.Failed = true;
                result.Error = e.Message;
            }

            if (s_HadFailure)
            {
                result.Failed = true;
                if (string.IsNullOrEmpty(result.Error))
                    result.Error = s_FailureText ?? "Smart string could not be parsed.";
            }

            return result;
        }

        static void Walk(Format format, SmartParseResult result, string locale)
        {
            if (format == null)
                return;

            foreach (var item in format.Items)
            {
                var placeholder = item as Placeholder;
                if (placeholder == null)
                    continue;

                if (placeholder.Selectors.Count > 0)
                {
                    var raw = placeholder.Selectors[0].RawText;
                    if (!string.IsNullOrEmpty(raw))
                    {
                        var usage = new SmartVariableUsage
                        {
                            Locale = locale,
                            FormatterName = placeholder.FormatterName
                        };

                        int index;
                        if (int.TryParse(raw, out index))
                        {
                            usage.IsPositional = true;
                            usage.Index = index;
                        }
                        else
                        {
                            usage.SourceName = raw;
                        }
                        result.Usages.Add(usage);
                    }
                }

                if (!string.IsNullOrEmpty(placeholder.FormatterName) &&
                    !s_FormatterNames.Contains(placeholder.FormatterName))
                {
                    result.UnknownFormatters.Add(placeholder.FormatterName);
                }
                else if (string.IsNullOrEmpty(placeholder.FormatterName) && placeholder.Format != null)
                {
                    // The parser only assigns FormatterName when the formatter exists. An unknown formatter ends
                    // up in the format text, e.g. "{value:notAFormatter:x}" -> "notAFormatter:x".
                    var formatText = placeholder.Format.RawText;
                    var colon = formatText.IndexOf(':');
                    if (colon > 0)
                    {
                        var candidate = formatText.Substring(0, colon);
                        if (LooksLikeFormatterName(candidate) && !s_FormatterNames.Contains(candidate))
                            result.UnknownFormatters.Add(candidate);
                    }
                }

                // Nested placeholders inside a formatter's format string also need their variables.
                Walk(placeholder.Format, result, locale);
            }
        }

        static bool LooksLikeFormatterName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            foreach (var c in value)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }
            return true;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.SmartFormat.Core.Extensions;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Severnbli.LocalizationCodegen
{
    /// <summary>
    /// Entry points used by generated table classes. Wraps Unity Localization static database calls.
    /// </summary>
    /// <remarks>
    /// The synchronous methods block until the table is loaded. They are convenient in edit-mode tools and for
    /// strings that are already preloaded, but Addressables does not support synchronous loads on WebGL. Use the
    /// <c>...Async</c> variants on platforms where that matters.
    /// </remarks>
    public static class LocalizedTables
    {
        static bool s_WarnedMissingSettings;

        /// <summary>True when Localization is configured; logs one warning the first time it is not.</summary>
        internal static bool HasSettings()
        {
            if (LocalizationSettings.HasSettings)
                return true;

            if (!s_WarnedMissingSettings)
            {
                s_WarnedMissingSettings = true;
                Debug.LogWarning(
                    "[Localization Codegen] LocalizationSettings are not configured. " +
                    "Generated members return the table key instead of a translation.");
            }
            return false;
        }

        /// <summary>Returns the translated string for the selected locale.</summary>
        public static string GetString(string table, string key)
        {
            if (!HasSettings())
                return key;
            return LocalizationSettings.StringDatabase.GetLocalizedString(table, key);
        }

        /// <summary>Returns the translated string after applying positional format arguments.</summary>
        public static string GetString(string table, string key, params object[] arguments)
        {
            if (!HasSettings())
                return key;
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                table, key, null, FallbackBehavior.UseProjectSettings, arguments ?? new object[0]);
        }

        /// <summary>
        /// Returns the translated string after resolving named smart string variables from a dictionary.
        /// The dictionary is passed as the single argument so SmartFormat can resolve named selectors.
        /// </summary>
        public static string GetString(string table, string key, IDictionary<string, object> named)
        {
            if (!HasSettings())
                return key;
            if (named == null || named.Count == 0)
                return GetString(table, key);

            var arguments = new object[] { named };
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                table, key, null, FallbackBehavior.UseProjectSettings, arguments);
        }

        /// <summary>
        /// Handles smart strings that mix positional (<c>{0}</c>) and named (<c>{player}</c>) placeholders.
        /// Positional arguments are the format argument list; named values become local variables.
        /// </summary>
        public static string GetStringMixed(
            string table,
            string key,
            IList<object> positional,
            IDictionary<string, object> named)
        {
            if (!HasSettings())
                return key;

            var hasPositional = positional != null && positional.Count > 0;
            var hasNamed = named != null && named.Count > 0;

            if (!hasNamed)
                return hasPositional ? GetString(table, key, ToArray(positional)) : GetString(table, key);
            if (!hasPositional)
                return GetString(table, key, named);

            var reference = CreateReference(table, key, positional, named);
            return reference.GetLocalizedString(positional);
        }

        /// <summary>Returns the localized asset for the selected locale.</summary>
        public static T GetAsset<T>(string table, string key) where T : Object
        {
            if (!HasSettings())
                return null;
            return LocalizationSettings.AssetDatabase.GetLocalizedAsset<T>(table, key);
        }

        /// <summary>Creates a <see cref="LocalizedString"/> reference with the supplied arguments.</summary>
        public static LocalizedString CreateReference(
            string table,
            string key,
            IList<object> positional = null,
            IDictionary<string, object> named = null)
        {
            var reference = new LocalizedString(table, key);

            if (positional != null && positional.Count > 0)
                reference.Arguments = positional;

            if (named != null)
            {
                foreach (var pair in named)
                {
                    if (!string.IsNullOrEmpty(pair.Key))
                        reference.Add(pair.Key, new ObjectValueVariable(pair.Value));
                }
            }

            return reference;
        }

        /// <summary>Asynchronous string lookup (does not block, works on WebGL).</summary>
        public static AsyncOperationHandle<string> GetStringAsync(string table, string key)
        {
            return LocalizationSettings.StringDatabase.GetLocalizedStringAsync(
                (TableReference)table, (TableEntryReference)key);
        }

        /// <summary>Asynchronous asset lookup (does not block, works on WebGL).</summary>
        public static AsyncOperationHandle<T> GetAssetAsync<T>(string table, string key) where T : Object
        {
            return LocalizationSettings.AssetDatabase.GetLocalizedAssetAsync<T>(
                (TableReference)table, (TableEntryReference)key);
        }

        /// <summary>
        /// Loads a string table for the selected locale without blocking, so later lookups do not wait for it.
        /// Completes with <see langword="null"/> when Localization is not configured.
        /// </summary>
        public static Task<StringTable> LoadStringTable(string table)
        {
            if (!HasSettings())
                return Task.FromResult<StringTable>(null);
            return ToTask(LocalizationSettings.StringDatabase.GetTableAsync(table));
        }

        /// <summary>
        /// Loads an asset table for the selected locale without blocking. The assets of its entries are loaded
        /// when they are requested. Completes with <see langword="null"/> when Localization is not configured.
        /// </summary>
        public static Task<AssetTable> LoadAssetTable(string table)
        {
            if (!HasSettings())
                return Task.FromResult<AssetTable>(null);
            return ToTask(LocalizationSettings.AssetDatabase.GetTableAsync(table));
        }

        /// <summary>
        /// Loads an asset table and the assets of all its entries for the selected locale without blocking, so later
        /// lookups do not wait. A table whose Preload Asset Table metadata is No Preload loads without its assets.
        /// Completes with <see langword="null"/> when Localization is not configured.
        /// </summary>
        public static async Task<AssetTable> LoadAssetTableEntries(string table)
        {
            if (!HasSettings())
                return null;

            await ToTask(LocalizationSettings.AssetDatabase.PreloadTables(table));
            return await LoadAssetTable(table);
        }

        /// <summary>
        /// Awaits a Localization operation through its completion callback, which also works on WebGL. The result is
        /// read inside the callback because Localization may release the operation right after it completes.
        /// </summary>
        static Task<T> ToTask<T>(AsyncOperationHandle<T> handle)
        {
            if (!handle.IsValid())
                return Task.FromException<T>(new System.InvalidOperationException("The localization operation is not valid."));

            var source = new TaskCompletionSource<T>();
            handle.Completed += completed =>
            {
                if (completed.Status == AsyncOperationStatus.Succeeded)
                    source.TrySetResult(completed.Result);
                else
                    source.TrySetException(Failure(completed.Status, completed.OperationException));
            };
            return source.Task;
        }

        /// <summary>Untyped variant of <see cref="ToTask{T}(AsyncOperationHandle{T})"/>.</summary>
        static Task ToTask(AsyncOperationHandle handle)
        {
            if (!handle.IsValid())
                return Task.FromException(new System.InvalidOperationException("The localization operation is not valid."));

            var source = new TaskCompletionSource<bool>();
            handle.Completed += completed =>
            {
                if (completed.Status == AsyncOperationStatus.Succeeded)
                    source.TrySetResult(true);
                else
                    source.TrySetException(Failure(completed.Status, completed.OperationException));
            };
            return source.Task;
        }

        static System.Exception Failure(AsyncOperationStatus status, System.Exception exception)
        {
            return exception ?? new System.InvalidOperationException(
                "The localization operation failed with status " + status + ".");
        }

        static object[] ToArray(IList<object> values)
        {
            var array = new object[values.Count];
            values.CopyTo(array, 0);
            return array;
        }

        /// <summary>Wraps any value as a smart string local variable.</summary>
        internal sealed class ObjectValueVariable : IVariable
        {
            readonly object m_Value;

            public ObjectValueVariable(object value)
            {
                m_Value = value;
            }

            public object GetSourceValue(ISelectorInfo selector) => m_Value;
        }
    }
}

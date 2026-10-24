using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Severnbli.LocalizationCodegen
{
    /// <summary>
    /// UniTask entry points used by generated <c>...Async</c> members. Compiled only when UniTask
    /// (<c>com.cysharp.unitask</c>) is installed, or when <c>LOCALIZATION_CODEGEN_UNITASK</c> is defined.
    /// </summary>
    /// <remarks>
    /// The operations come from Unity Localization, which releases them automatically, so nothing here needs to be
    /// released by the caller. Cancelling only stops the wait: Localization still finishes loading the table.
    /// </remarks>
    public static class LocalizedTablesUniTask
    {
        /// <summary>Loads the translated string for the selected locale without blocking.</summary>
        public static UniTask<string> GetStringAsync(string table, string key, CancellationToken cancellationToken = default)
            => GetStringAsync(table, key, null, null, cancellationToken);

        /// <summary>
        /// Loads the translated string and applies positional and named smart string arguments, with the same
        /// semantics as the synchronous <see cref="LocalizedTables"/> methods. Either argument set may be null.
        /// </summary>
        public static UniTask<string> GetStringAsync(
            string table,
            string key,
            IList<object> positional,
            IDictionary<string, object> named,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return UniTask.FromCanceled<string>(cancellationToken);
            if (!LocalizedTables.HasSettings())
                return UniTask.FromResult(key);

            var hasPositional = positional != null && positional.Count > 0;
            var hasNamed = named != null && named.Count > 0;

            IList<object> arguments;
            IVariableGroup localVariables = null;
            if (hasPositional && hasNamed)
            {
                // Same as LocalizedTables.GetStringMixed: named values become local variables.
                arguments = positional;
                localVariables = new NamedVariables(named);
            }
            else if (hasNamed)
            {
                // Same as LocalizedTables.GetString(table, key, named): the dictionary is the single argument.
                arguments = new object[] { named };
            }
            else
            {
                arguments = hasPositional ? positional : Array.Empty<object>();
            }

            var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(
                table, key, arguments, null, FallbackBehavior.UseProjectSettings, localVariables);
            return ToUniTask(handle, cancellationToken);
        }

        /// <summary>Loads the localized asset for the selected locale without blocking.</summary>
        public static UniTask<T> GetAssetAsync<T>(string table, string key, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            if (cancellationToken.IsCancellationRequested)
                return UniTask.FromCanceled<T>(cancellationToken);
            if (!LocalizedTables.HasSettings())
                return UniTask.FromResult<T>(null);

            var handle = LocalizationSettings.AssetDatabase.GetLocalizedAssetAsync<T>(table, key);
            return ToUniTask(handle, cancellationToken);
        }

        /// <summary>
        /// Awaits a Localization operation. The result is read inside the completion callback because Localization
        /// releases the operation right after it completes.
        /// </summary>
        internal static UniTask<T> ToUniTask<T>(AsyncOperationHandle<T> handle, CancellationToken cancellationToken)
        {
            if (!handle.IsValid())
                return UniTask.FromException<T>(new InvalidOperationException("The localization operation is not valid."));
            if (handle.IsDone)
                return FromCompleted(handle);

            var source = new UniTaskCompletionSource<T>();
            var registration = cancellationToken.CanBeCanceled
                ? cancellationToken.RegisterWithoutCaptureExecutionContext(() => source.TrySetCanceled(cancellationToken))
                : default;

            handle.Completed += completed =>
            {
                registration.Dispose();
                if (completed.Status == AsyncOperationStatus.Succeeded)
                    source.TrySetResult(completed.Result);
                else
                    source.TrySetException(Failure(completed));
            };

            return source.Task;
        }

        static UniTask<T> FromCompleted<T>(AsyncOperationHandle<T> handle)
        {
            return handle.Status == AsyncOperationStatus.Succeeded
                ? UniTask.FromResult(handle.Result)
                : UniTask.FromException<T>(Failure(handle));
        }

        static Exception Failure<T>(AsyncOperationHandle<T> handle)
        {
            return handle.OperationException ??
                   new InvalidOperationException("The localization operation failed with status " + handle.Status + ".");
        }

        /// <summary>Exposes named arguments as smart string local variables.</summary>
        sealed class NamedVariables : IVariableGroup
        {
            readonly IDictionary<string, object> m_Values;

            public NamedVariables(IDictionary<string, object> values)
            {
                m_Values = values;
            }

            public bool TryGetValue(string key, out IVariable value)
            {
                object raw;
                if (key != null && m_Values.TryGetValue(key, out raw))
                {
                    value = new LocalizedTables.ObjectValueVariable(raw);
                    return true;
                }

                value = null;
                return false;
            }
        }
    }

    /// <summary>UniTask helpers for Unity Localization references, such as the members of a generated <c>Ref</c> class.</summary>
    public static class LocalizedStringUniTaskExtensions
    {
        /// <summary>Loads the string of a <see cref="LocalizedString"/>, using its arguments and local variables.</summary>
        public static UniTask<string> GetLocalizedStringUniTask(
            this LocalizedString reference,
            CancellationToken cancellationToken = default)
        {
            if (reference == null)
                throw new ArgumentNullException(nameof(reference));
            if (cancellationToken.IsCancellationRequested)
                return UniTask.FromCanceled<string>(cancellationToken);
            if (!LocalizedTables.HasSettings())
                return UniTask.FromResult(reference.TableEntryReference.Key);

            return LocalizedTablesUniTask.ToUniTask(reference.GetLocalizedStringAsync(), cancellationToken);
        }
    }
}

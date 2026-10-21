using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

namespace Severnbli.LocalizationCodegen.Editor
{
    /// <summary>
    /// Coalesces localization editor events into one debounced generation pass.
    /// The dirty flag lives in <see cref="SessionState"/> so it survives a domain reload.
    /// </summary>
    [InitializeOnLoad]
    internal static class CodegenHooks
    {
        const string DirtyKey = "LocalizationCodegen.Dirty";
        const string WizardShownKey = "LocalizationCodegen.WizardShown";

        const double DebounceSeconds = 0.5;

        static double s_LastDirtyTime;
        static bool s_StalenessCheckPending = true;

        static CodegenHooks()
        {
            var settings = CodegenSettings.instance;
            settings.EnsureMigrated();

            var events = LocalizationEditorSettings.EditorEvents;
            events.CollectionAdded += _ => MarkDirtyExternal();
            events.CollectionRemoved += _ => MarkDirtyExternal();
            events.CollectionModified += (_, __) => MarkDirtyExternal();
            events.TableEntryAdded += (_, __) => MarkDirtyExternal();
            events.TableEntryRemoved += (_, __) => MarkDirtyExternal();
            events.TableEntryModified += _ => MarkDirtyExternal();
            events.AssetTableEntryAdded += (_, __, ___) => MarkDirtyExternal();
            events.AssetTableEntryRemoved += (_, __, ___, ____) => MarkDirtyExternal();
            events.LocaleAdded += _ => MarkDirtyExternal();
            events.LocaleRemoved += _ => MarkDirtyExternal();
            events.LocaleSortOrderChanged += (_, __) => MarkDirtyExternal();

            if (SessionState.GetBool(DirtyKey, false))
                s_LastDirtyTime = EditorApplication.timeSinceStartup;

            EditorApplication.update += Tick;
            EditorApplication.delayCall += ShowWizardOnce;
        }

        /// <summary>Marks the generated output as stale. Safe to call from asset postprocessors.</summary>
        internal static void MarkDirtyExternal()
        {
            SessionState.SetBool(DirtyKey, true);
            s_LastDirtyTime = EditorApplication.timeSinceStartup;
        }

        static void Tick()
        {
            if (Application.isBatchMode)
                return;

            if (s_StalenessCheckPending &&
                !EditorApplication.isCompiling &&
                !EditorApplication.isUpdating &&
                !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                s_StalenessCheckPending = false;
                CheckStaleness();
            }

            if (!SessionState.GetBool(DirtyKey, false))
                return;

            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorApplication.timeSinceStartup - s_LastDirtyTime < DebounceSeconds)
                return;

            var settings = CodegenSettings.instance;
            if (!settings.SetupCompleted || !settings.AutoRegenerate)
            {
                SessionState.SetBool(DirtyKey, false);
                return;
            }

            SessionState.SetBool(DirtyKey, false);
            CodeGenerator.Run(new GenerateOptions { IsAutomatic = true });
        }

        static void CheckStaleness()
        {
            try
            {
                var settings = CodegenSettings.instance;
                if (!settings.SetupCompleted || !settings.AutoRegenerate || string.IsNullOrEmpty(settings.LastModelHash))
                    return;

                var current = CodeGenerator.ComputeModelHash();
                if (current != null && current != settings.LastModelHash)
                    MarkDirtyExternal();
            }
            catch
            {
                // Tables may not be imported yet; the next editor event will mark us dirty again.
            }
        }

        static void ShowWizardOnce()
        {
            if (Application.isBatchMode)
                return;
            if (CodegenSettings.instance.SetupCompleted)
                return;
            if (SessionState.GetBool(WizardShownKey, false))
                return;

            SessionState.SetBool(WizardShownKey, true);
            SetupWizard.Open();
        }
    }
}

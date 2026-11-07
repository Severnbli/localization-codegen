using UnityEditor;
using UnityEngine;

namespace Severnbli.LocalizationCodegen.Editor
{
    /// <summary>
    /// Coalesces dirty marks into one debounced generation pass. Table edits mark the output dirty only once the
    /// table assets are saved (see <see cref="CodegenAssetPostprocessor"/>), not on every in-memory edit.
    /// The dirty flag lives in <see cref="SessionState"/> so it survives a domain reload.
    /// </summary>
    [InitializeOnLoad]
    internal static class CodegenHooks
    {
        const string DirtyKey = "LocalizationCodegen.Dirty";
        const string WizardShownKey = "LocalizationCodegen.WizardShown";
        const string TablesDeletedKey = "LocalizationCodegen.TablesDeleted";

        const double DebounceSeconds = 0.5;

        static double s_LastDirtyTime;
        static bool s_StalenessCheckPending = true;

        static CodegenHooks()
        {
            var settings = CodegenSettings.instance;
            settings.EnsureMigrated();

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

        /// <summary>Marks the output stale after table collection assets were deleted, so their files go too.</summary>
        internal static void MarkTablesDeleted()
        {
            SessionState.SetBool(TablesDeletedKey, true);
            MarkDirtyExternal();
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
            var tablesDeleted = SessionState.GetBool(TablesDeletedKey, false);
            SessionState.SetBool(DirtyKey, false);
            SessionState.SetBool(TablesDeletedKey, false);

            if (!settings.SetupCompleted || !settings.AutoRegenerate)
                return;

            CodeGenerator.Run(new GenerateOptions { IsAutomatic = true, TablesDeleted = tablesDeleted });
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

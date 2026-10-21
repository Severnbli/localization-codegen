using System;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace Severnbli.LocalizationCodegen.Editor
{
    /// <summary>
    /// Detects table assets changed outside the editor (git pull, merge, revert) and marks the output stale.
    /// </summary>
    internal sealed class CodegenAssetPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            try
            {
                var settings = CodegenSettings.instance;
                if (!settings.SetupCompleted || !settings.AutoRegenerate)
                    return;

                if (ContainsLocalizationAsset(importedAssets) ||
                    ContainsLocalizationAsset(movedAssets) ||
                    ContainsAssetPath(deletedAssets) ||
                    ContainsAssetPath(movedFromAssetPaths))
                {
                    CodegenHooks.MarkDirtyExternal();
                }
            }
            catch
            {
                // Asset postprocessing must never break an import.
            }
        }

        static bool ContainsLocalizationAsset(string[] paths)
        {
            if (paths == null)
                return false;

            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path))
                    continue;

                var type = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (type == null)
                    continue;

                if (typeof(LocalizationTableCollection).IsAssignableFrom(type) ||
                    typeof(SharedTableData).IsAssignableFrom(type) ||
                    typeof(StringTable).IsAssignableFrom(type) ||
                    typeof(AssetTable).IsAssignableFrom(type))
                    return true;
            }
            return false;
        }

        static bool ContainsAssetPath(string[] paths)
        {
            if (paths == null)
                return false;

            foreach (var path in paths)
            {
                if (!string.IsNullOrEmpty(path) &&
                    path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}

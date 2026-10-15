using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Severnbli.LocalizationCodegen
{
    /// <summary>
    /// Entry points used by generated table classes. Wraps Unity Localization static database calls.
    /// </summary>
    public static class LocalizedTables
    {
        public static string GetString(string table, string key)
        {
            return LocalizationSettings.StringDatabase.GetLocalizedString(table, key);
        }

        public static string GetString(string table, string key, params object[] arguments)
        {
            return LocalizationSettings.StringDatabase.GetLocalizedString(table, key, null, FallbackBehavior.UseProjectSettings, arguments);
        }

        public static T GetAsset<T>(string table, string key) where T : Object
        {
            return LocalizationSettings.AssetDatabase.GetLocalizedAsset<T>(table, key);
        }
    }
}

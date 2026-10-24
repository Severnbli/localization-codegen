using System;

namespace Severnbli.LocalizationCodegen.Editor
{
    /// <summary>Detects the optional UniTask runtime assembly that generated <c>...Async</c> members call.</summary>
    internal static class UniTaskSupport
    {
        public const string RuntimeAssemblyName = "com.severnbli.localization-codegen.UniTask";
        public const string UniTaskAssemblyName = "UniTask";

        public const string MissingMessage =
            "UniTask members were not generated: the " + RuntimeAssemblyName + " assembly is not compiled. Install " +
            "UniTask (com.cysharp.unitask) as a package, or define LOCALIZATION_CODEGEN_UNITASK when UniTask lives " +
            "under Assets.";

        /// <summary>
        /// True when the add-on assembly is compiled. Its asmdef only compiles when UniTask is installed, so this
        /// also means UniTask itself is available to generated code.
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (string.Equals(assembly.GetName().Name, RuntimeAssemblyName, StringComparison.Ordinal))
                        return true;
                }
                return false;
            }
        }
    }
}

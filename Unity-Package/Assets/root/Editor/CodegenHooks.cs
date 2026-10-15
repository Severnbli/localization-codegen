using UnityEditor;
using UnityEditor.Localization;

namespace Severnbli.LocalizationCodegen.Editor
{
    [InitializeOnLoad]
    static class CodegenHooks
    {
        static bool s_Pending;

        static CodegenHooks()
        {
            var events = LocalizationEditorSettings.EditorEvents;
            events.CollectionAdded += _ => Schedule();
            events.CollectionRemoved += _ => Schedule();
            events.CollectionModified += (_, __) => Schedule();
            events.TableEntryAdded += (_, __) => Schedule();
            events.TableEntryRemoved += (_, __) => Schedule();
            events.TableEntryModified += _ => Schedule();
            events.AssetTableEntryAdded += (_, __, ___) => Schedule();
            events.AssetTableEntryRemoved += (_, __, ___, ____) => Schedule();

            EditorApplication.delayCall += () =>
            {
                if (!CodegenSettings.instance.SetupCompleted)
                    SetupWizard.Open();
            };
        }

        // Many events fire in bursts; coalesce them into one generation pass.
        static void Schedule()
        {
            if (s_Pending || !CodegenSettings.instance.SetupCompleted || !CodegenSettings.instance.AutoRegenerate)
                return;

            s_Pending = true;
            EditorApplication.delayCall += () =>
            {
                s_Pending = false;
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    Schedule();
                    return;
                }
                CodeGenerator.Generate();
            };
        }
    }
}

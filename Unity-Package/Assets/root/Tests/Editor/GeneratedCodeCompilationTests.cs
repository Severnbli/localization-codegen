using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine.TestTools;

namespace Severnbli.LocalizationCodegen.Editor.Tests
{
    /// <summary>
    /// Compiles emitted code with Unity's compiler, with warnings as errors and XML documentation enabled, so a change
    /// in the emitter that produces invalid or warning-prone C# fails here instead of in a user's project.
    /// </summary>
    public class GeneratedCodeCompilationTests
    {
        const string WorkRoot = "Temp/LocalizationCodegenCompileCheck";
        const double TimeoutSeconds = 120;

        // Assemblies generated code may reference, besides the engine modules.
        static readonly string[] s_ReferencedAssemblies =
        {
            "com.severnbli.localization-codegen.Runtime",
            "Unity.Localization",
            "Unity.ResourceManager",
            UniTaskSupport.RuntimeAssemblyName,
            UniTaskSupport.UniTaskAssemblyName
        };

        [UnityTest]
        public IEnumerator GeneratedCode_Compiles()
        {
            return Compile(uniTask: false);
        }

        [UnityTest]
        public IEnumerator GeneratedCodeWithUniTask_Compiles()
        {
            if (!UniTaskSupport.IsAvailable)
                Assert.Ignore("UniTask is not installed in this project.");
            return Compile(uniTask: true);
        }

        static IEnumerator Compile(bool uniTask)
        {
            var folder = Path.Combine(WorkRoot, uniTask ? "unitask" : "plain");
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);

            var sources = new List<string>();
            foreach (var variant in Variants(uniTask))
            {
                var models = BuildModels();
                CodeEmitter.AssignClassNames(models, variant.Options);
                foreach (var model in models)
                {
                    var path = Path.Combine(folder, variant.Name + "_" + model.ClassName.TrimStart('@') + ".g.cs");
                    File.WriteAllText(path, CodeEmitter.Emit(model, variant.Options));
                    sources.Add(path);
                }
            }

            // AssemblyBuilder is obsolete from Unity 6.5 (CS0618) but still the only in-editor way to compile with
            // Unity's own compiler and references; replace it once Unity ships the Roslyn-based alternative.
#pragma warning disable CS0618
            var builder = new AssemblyBuilder(Path.Combine(folder, "CompileCheck.dll"), sources.ToArray())
            {
                referencesOptions = ReferencesOptions.UseEngineModules,
                additionalReferences = ReferencePaths(uniTask),
                compilerOptions =
                {
                    AdditionalCompilerArguments = new[]
                    {
                        "-warnaserror+",
                        "-doc:" + Path.GetFullPath(Path.Combine(folder, "CompileCheck.xml")),
                        // Missing XML comments are expected for members without a preview.
                        "-nowarn:1591"
                    }
                }
            };

#pragma warning restore CS0618

            CompilerMessage[] messages = null;
            builder.buildFinished += (_, result) => messages = result;

            var started = EditorApplication.timeSinceStartup;
            while (!builder.Build())
            {
                // Build() refuses to start while Unity itself is compiling.
                if (EditorApplication.timeSinceStartup - started > TimeoutSeconds)
                    Assert.Fail("AssemblyBuilder could not start.");
                yield return null;
            }

            while (messages == null)
            {
                if (EditorApplication.timeSinceStartup - started > TimeoutSeconds)
                    Assert.Fail("Compiling the generated code timed out.");
                yield return null;
            }

            var problems = messages
                .Where(m => m.type == CompilerMessageType.Error || m.type == CompilerMessageType.Warning)
                .Select(m => m.message)
                .ToList();
            Assert.IsEmpty(problems, "Generated code must compile without errors or warnings:\n" + string.Join("\n", problems));
        }

        static string[] ReferencePaths(bool uniTask)
        {
            var available = CompilationPipeline.GetAssemblies(AssembliesType.Player)
                .ToDictionary(a => a.name, a => a.outputPath, StringComparer.Ordinal);

            var paths = new List<string>();
            foreach (var name in s_ReferencedAssemblies)
            {
                var isUniTask = name == UniTaskSupport.RuntimeAssemblyName || name == UniTaskSupport.UniTaskAssemblyName;
                if (isUniTask && !uniTask)
                    continue;

                string path;
                if (available.TryGetValue(name, out path))
                {
                    paths.Add(Path.GetFullPath(path));
                    continue;
                }

                // Precompiled package assemblies are not part of the compilation pipeline.
                var precompiled = CompilationPipeline.GetPrecompiledAssemblyPaths(
                        CompilationPipeline.PrecompiledAssemblySources.All)
                    .FirstOrDefault(p => string.Equals(Path.GetFileNameWithoutExtension(p), name, StringComparison.Ordinal));
                Assert.IsNotNull(precompiled, "Could not find the " + name + " assembly to reference.");
                paths.Add(precompiled);
            }
            return paths.ToArray();
        }

        struct Variant
        {
            public string Name;
            public EmitOptions Options;
        }

        static IEnumerable<Variant> Variants(bool uniTask)
        {
            // Every option that changes the shape of the output, in a namespace.
            yield return new Variant
            {
                Name = "Full",
                Options = new EmitOptions
                {
                    Namespace = "CompileCheck.Full",
                    Refs = true,
                    XmlDocs = true,
                    SmartStrings = SmartStringMode.MethodsAndRefs,
                    SmartParamTypes = SmartParamTypeMode.Inferred,
                    KeySeparators = "/",
                    UniTask = uniTask
                }
            };

            // PascalCase names in the global namespace.
            yield return new Variant
            {
                Name = "Global",
                Options = new EmitOptions
                {
                    Namespace = null,
                    Refs = true,
                    XmlDocs = true,
                    SmartStrings = SmartStringMode.Methods,
                    Naming = NamingConvention.PascalCase,
                    UniTask = uniTask
                }
            };

            // Default options.
            yield return new Variant
            {
                Name = "Legacy",
                Options = new EmitOptions { Namespace = "CompileCheck.Legacy", UniTask = uniTask }
            };
        }

        static List<TableModel> BuildModels()
        {
            var strings = new TableModel { Name = "Ui" };
            strings.Entries.Add(new EntryModel { Id = 1, Key = "Title", Preview = "Hello & <b>welcome</b>" });
            strings.Entries.Add(new EntryModel { Id = 2, Key = "TitleAsync" });
            strings.Entries.Add(new EntryModel { Id = 3, Key = "Menu/Start" });
            strings.Entries.Add(new EntryModel { Id = 4, Key = "Menu/Quit" });
            strings.Entries.Add(new EntryModel { Id = 5, Key = "class" });
            strings.Entries.Add(new EntryModel { Id = 6, Key = "ToString" });
            strings.Entries.Add(new EntryModel { Id = 7, Key = "Ref" });

            var positional = new EntryModel { Id = 8, Key = "Score", Preview = "{0} of {1}" };
            positional.Params.Add(new SmartParam { IsPositional = true, Index = 0, SourceName = "0", ClrName = "arg0", TypeName = "object" });
            positional.Params.Add(new SmartParam { IsPositional = true, Index = 1, SourceName = "1", ClrName = "arg1", TypeName = "object" });
            strings.Entries.Add(positional);

            var named = new EntryModel { Id = 9, Key = "Welcome" };
            named.Params.Add(new SmartParam { SourceName = "player-name", ClrName = "playerName", TypeName = "object" });
            named.Params.Add(new SmartParam { SourceName = "count", ClrName = "count", TypeName = "int", FormatterName = "plural" });
            named.Params.Add(new SmartParam { SourceName = "cancellationToken", ClrName = "cancellationToken", TypeName = "object" });
            strings.Entries.Add(named);

            var mixed = new EntryModel { Id = 10, Key = "Mixed" };
            mixed.Params.Add(new SmartParam { IsPositional = true, Index = 0, SourceName = "0", ClrName = "arg0", TypeName = "object" });
            mixed.Params.Add(new SmartParam
            {
                SourceName = "items", ClrName = "items", TypeName = "System.Collections.IEnumerable", FormatterName = "list"
            });
            strings.Entries.Add(mixed);

            var assets = new TableModel { Name = "Ui", IsAsset = true };
            assets.Entries.Add(new EntryModel { Id = 1, Key = "Logo", AssetTypeName = "UnityEngine.Sprite" });
            assets.Entries.Add(new EntryModel { Id = 2, Key = "Theme", AssetTypeName = "UnityEngine.AudioClip" });
            assets.Entries.Add(new EntryModel { Id = 3, Key = "Anything", AssetTypeName = "UnityEngine.Object" });
            assets.Entries.Add(new EntryModel { Id = 4, Key = "Mesh", AssetTypeName = "UnityEngine.Mesh" });
            // No dedicated Localized* type: uses the generic LocalizedAsset<T>.
            assets.Entries.Add(new EntryModel { Id = 5, Key = "Background", AssetTypeName = "UnityEngine.Texture2D" });
            // With key separators this creates a nested class Sprite, so the asset type must stay qualified.
            assets.Entries.Add(new EntryModel { Id = 6, Key = "Sprite/Icon", AssetTypeName = "UnityEngine.Sprite" });

            return new List<TableModel> { strings, assets };
        }
    }
}

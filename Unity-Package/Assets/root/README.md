<div align="center">

<img width="520" alt="Localization Codegen" src="https://github.com/user-attachments/assets/c43737d6-e7a0-416a-9aa5-fa3ca2466529" />

<h1>Localization Codegen</h1>

<p>
  <strong>Strongly typed C# access to your Unity Localization tables</strong><br/>
  Write code instead of hitting the localization database with magic strings
</p>

<p>
  <a href="https://openupm.com/packages/com.severnbli.localization-codegen/">
    <img alt="OpenUPM" src="https://img.shields.io/npm/v/com.severnbli.localization-codegen?label=openupm&registry_uri=https://package.openupm.com&color=blue" />
  </a>
  <a href="https://openupm.com/packages/com.severnbli.localization-codegen/">
    <img alt="Downloads" src="https://img.shields.io/badge/dynamic/json?color=brightgreen&label=downloads&query=%24.downloads&suffix=%2Fmonth&url=https%3A%2F%2Fpackage.openupm.com%2Fdownloads%2Fpoint%2Flast-month%2Fcom.severnbli.localization-codegen" />
  </a>
  <a href="https://unity.com/">
    <img alt="Unity 2021.3+" src="https://img.shields.io/badge/Unity-2021.3%2B-black?logo=unity" />
  </a>
  <a href="https://github.com/Cysharp/UniTask">
    <img alt="UniTask optional" src="https://img.shields.io/badge/UniTask-optional-orange" />
  </a>
  <a href="LICENSE">
    <img alt="MIT License" src="https://img.shields.io/badge/license-MIT-green" />
  </a>
</p>

<p>
  <a href="#quick-start">Quick start</a> •
  <a href="#installation">Installation</a> •
  <a href="#usage">Usage</a> •
  <a href="#settings">Settings</a> •
  <a href="#ci--batch-mode">CI</a> •
  <a href="CHANGELOG.md">Changelog</a>
</p>

</div>

---

## Overview

Localization Codegen reads your [Unity Localization](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/index.html) table collections and generates plain C# classes for them – one static class per collection, one member per key. No runtime reflection, no string lookups in your game code, and full autocomplete plus compile-time safety.

```csharp
// Before
label.text = LocalizationSettings.StringDatabase.GetLocalizedString("Ui", "HelloWorld");

// After
label.text = Ui.HelloWorld;
```

| Feature | Description |
|---|---|
| 🧩 **Typed access** | One static class per string or asset table collection, one member per key |
| 💬 **Smart strings** | Entries with placeholders become methods with typed positional and named parameters |
| 🔗 **Reference members** | Optional `Ref` class with `LocalizedString` / `LocalizedAsset<T>` references that follow locale changes |
| ⚡ **UniTask** | Optional `…Async` members returning `UniTask`, with cancellation |
| 🔁 **Automatic regeneration** | Debounced regeneration after table edits, including changes pulled from version control |
| 🧪 **CI ready** | Batch mode entry points to regenerate, or to fail when generated code is out of date |
| 🪄 **First-run wizard** | A setup wizard opens after the package is first imported |

## Quick start

1. Install the package with [OpenUPM](#option-1--openupm-recommended):

   ```bash
   openupm add com.severnbli.localization-codegen
   ```

2. Finish the setup wizard. It picks a namespace and an output folder and runs the first generation.
3. Use the generated classes:

   ```csharp
   using Localization;

   label.text = Ui.HelloWorld;
   ```

Edit your tables as usual – the classes regenerate on their own.

## Installation

Pick **one** variant. Do not mix them: the UPM package and the `.unitypackage` variants both provide assemblies named `com.severnbli.localization-codegen.Runtime` / `.Editor`, so installing two of them causes duplicate assembly errors.

| Variant | Updates | Dependencies | Version tracking |
|---|---|---|---|
| [OpenUPM](#option-1--openupm-recommended) | Package Manager | automatic | manifest + lock file |
| [Signed tarball](#option-2--signed-tarball-tgz) | manual URL edit | automatic | manifest entry |
| [Installer `.unitypackage`](#option-3--installer-unitypackage) | one-shot manifest edit | automatic | manifest + lock file |
| [Standard `.unitypackage`](#option-4--standard-unitypackage-manual-versioning) | manual re-import | manual | **you** |

The setup wizard opens after the first compilation in every case.

### Option 1 – OpenUPM (recommended)

The package is published on [OpenUPM](https://openupm.com/packages/com.severnbli.localization-codegen/) as `com.severnbli.localization-codegen`.

1. Install the [OpenUPM CLI](https://github.com/openupm/openupm-cli#installation) (requires Node.js):

   ```bash
   npm install -g openupm-cli
   ```

2. Open a terminal in your Unity project folder (the one containing `Assets/` and `Packages/`) and run:

   ```bash
   openupm add com.severnbli.localization-codegen
   ```

3. Switch back to Unity. The package resolves, compiles, and the setup wizard opens.

> [!TIP]
> `openupm add` handles the scoped registry, dependency resolution and `manifest.json` changes for you. You can also add the package from **Window > Package Manager** if the OpenUPM registry is already configured.

<details>
<summary><strong>Manual installation (without the CLI)</strong></summary>

<br/>

Add the scoped registry and the dependency to `Packages/manifest.json`:

```json
{
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.severnbli.localization-codegen"
      ]
    }
  ],
  "dependencies": {
    "com.severnbli.localization-codegen": "1.2.0"
  }
}
```

Then let Unity refresh the project. Replace the version with the latest one shown on the badge above.

</details>

Package Manager resolves `com.unity.localization` for you and keeps the version in `Packages/packages-lock.json`.

### Option 2 – Signed tarball (`.tgz`)

Every release ships a signed UPM tarball, `tarball-com.severnbli.localization-codegen-<version>.tgz`, which is the same artifact OpenUPM consumes. Add it as a file dependency:

```json
{
  "dependencies": {
    "com.severnbli.localization-codegen": "https://github.com/Severnbli/localization-codegen/releases/download/<version>/tarball-com.severnbli.localization-codegen-<version>.tgz"
  }
}
```

Unity resolves the package's own dependencies from the tarball, so `com.unity.localization` is pulled in as usual. Packages installed from a URL are not updated automatically – editing the URL or version by hand is the update. It also works for offline machines: download the file once and reference it with a `file:` path. The `.attestation.p7m` signature inside the archive is what OpenUPM verifies; Unity itself does not.

### Option 3 – Installer `.unitypackage`

Download `installer-com.severnbli.localization-codegen-<version>.unitypackage` from the release and import it (**Assets > Import Package > Custom Package**). It contains only an editor script under `Assets/Editor/Localization Codegen Installer/`, which on the next domain reload:

- registers the OpenUPM scoped registry in `Packages/manifest.json`, and
- pins `com.severnbli.localization-codegen` to the installer's version.

Package Manager then downloads and manages the real package, so dependencies resolve automatically and updates work as in Option 1. The installer never downgrades: it only writes the version when the installer's version is newer than the one already pinned.

### Option 4 – Standard `.unitypackage` (manual versioning)

Download `com.severnbli.localization-codegen-<version>.unitypackage` from the release and import it. Sources land in `Assets/Plugins/LocalizationCodegen/`. Committed `.meta` GUIDs are preserved, so script references survive a re-import and a later upgrade.

This variant bypasses Package Manager, so **versioning is manual**:

- No `package.json` is included, so Unity does not know which version is installed and Package Manager cannot list, update or uninstall it. Record the version yourself – keep the versioned release file or note it in your changelog.
- To update, delete `Assets/Plugins/LocalizationCodegen/` and import the newer `.unitypackage`. There are no partial upgrades, and stale files are not cleaned up.
- Install the `com.unity.localization` dependency yourself. Nothing resolves it for you.

## Requirements

| Requirement | Version |
|---|---|
| Unity | 2021.3 or newer |
| `com.unity.localization` | 1.5.x (automatic with the UPM variants, manual with the standard `.unitypackage`) |
| `com.cysharp.unitask` | 2.x, optional – only for [UniTask members](#unitask) |

## Usage

### Generated classes

Given a string table collection named `Ui` containing the key `HelloWorld`, the generator produces `Ui.g.cs`:

```csharp
// <auto-generated by Localization Codegen>
// Table collection: Ui
using __LT = global::Severnbli.LocalizationCodegen.LocalizedTables;

namespace Localization
{
    public static class Ui
    {
        public const string TableName = "Ui";

        public static string HelloWorld => __LT.GetString(TableName, "HelloWorld");
    }
}
```

The `__LT` alias is declared once per file. All other types (`LocalizedString`, asset types, `Dictionary<,>`) stay fully `global::`-qualified, so your own types can never shadow them.

Use it anywhere from your game code:

```csharp
using Localization;

label.text = Ui.HelloWorld;
```

For **asset table collections**, each member is typed from the entry's asset type (falling back to `UnityEngine.Object`) and returns the localized asset:

```csharp
Sprite icon = Icons.Sword;
```

### Naming rules

Generated identifiers always stay valid C#:

- Invalid characters become `_`.
- Names starting with a digit get a `_` prefix.
- C# keywords get an `@` prefix.
- Collisions get a numeric suffix, assigned by entry id so older keys keep their names.
- Class names collide case-insensitively (`Ui` and `UI` get separate files on Windows/macOS).
- Reserved names (`System`, `UnityEngine`, `Unity`, the first namespace segment, …) get a `Table` suffix.
- Windows device names (`CON`, `NUL`, `COM1`, …) get a trailing `_`.

With **Naming Convention = PascalCase** and optional **Key Separators**, `player name` becomes `PlayerName` and `Menu/Start` becomes the nested class `Menu.Start`. These options are off by default, so upgraded projects keep their existing API.

### Smart strings

Enable **Smart Strings = Methods** to turn entries that need arguments into methods:

```csharp
// "{0}, you have {count:plural:one item|{} items}"
public static string Greeting(object arg0, int count)
    => __LT.GetStringMixed(TableName, "Greeting", new object[] { arg0 },
        new global::System.Collections.Generic.Dictionary<string, object> { ["count"] = count });
```

- Positional placeholders (`{0}`) become `arg0`, `arg1`, … (gaps are filled).
- Named placeholders (`{player-name}`, `{player.Name}`) become camelCase parameters. The original name is preserved for the runtime lookup.
- `{}` (the current value inside a formatter) is not a parameter.
- Variables provided by a **Global Variables Group** are not parameters; Unity resolves them.
- **Smart Parameter Types = Inferred** uses `int` for `plural`, `IEnumerable` for `list`, otherwise `object`.
- Placeholder differences between locales are reported as warnings. Enable **Fail CI On Smart String Mismatch** to make `CheckBatch` fail on them.
- **MethodsAndRefs** additionally emits `Ref.Greeting(...)` returning a `LocalizedString` whose arguments and local variables are set.

> [!NOTE]
> Smart strings are opt-in. Upgraded projects default to `Off`; new projects default to `Methods` when the setup wizard completes.

### Reference members

The regular members return a **value**: `Ui.HelloWorld` is the text in the current locale at the moment you read it. When the player switches language, a label you filled that way keeps the old text.

Enable **Generate Reference Members** to also get a nested `Ref` class that returns a **reference** to the entry instead – Unity's own `LocalizedString` (or `LocalizedAsset<T>` for asset tables), which knows which entry it points at and can follow locale changes:

```csharp
public static class Ref
{
    public static global::UnityEngine.Localization.LocalizedString HelloWorld
        => new global::UnityEngine.Localization.LocalizedString(TableName, "HelloWorld");
}
```

Use it wherever Unity Localization expects a reference – still without magic strings:

```csharp
// A LocalizeStringEvent component updates its label whenever the locale changes.
localizeStringEvent.StringReference = Ui.Ref.HelloWorld;

// Or react to locale changes yourself.
var title = Ui.Ref.HelloWorld;
title.StringChanged += text => label.text = text;
```

Asset entries use `LocalizedSprite`, `LocalizedAudioClip`, `LocalizedAsset<T>` and the other built-in localized asset types when available. Each access creates a new reference, so keep the one you subscribed to.

### UniTask

Install [UniTask](https://github.com/Cysharp/UniTask) (`com.cysharp.unitask` 2.x) and enable **UniTask Members** to emit an awaitable variant next to every member:

```csharp
public static string HelloWorld => __LT.GetString(TableName, "HelloWorld");

public static global::Cysharp.Threading.Tasks.UniTask<string> HelloWorldAsync(global::System.Threading.CancellationToken cancellationToken = default)
    => global::Severnbli.LocalizationCodegen.LocalizedTablesUniTask.GetStringAsync(TableName, "HelloWorld", cancellationToken);
```

```csharp
label.text = await Ui.HelloWorldAsync(this.GetCancellationTokenOnDestroy());
Sprite icon = await Icons.SwordAsync();
```

- The async members never block on table loading, so they also work where synchronous loads are not supported (WebGL).
- Smart string methods keep their parameters (`await Ui.GreetingAsync(player, count)`), and asset entries return `UniTask<T>`.
- Cancelling stops the wait; Localization still finishes loading the table in the background.
- Turning the option on never renames existing members. A key that already ends in `Async` keeps its name, and the generated variant gets a suffix (`TitleAsync_2`).
- `LocalizedString.GetLocalizedStringUniTask()` awaits any `LocalizedString`, including `Ref` members.

> [!IMPORTANT]
> The API lives in the optional `com.severnbli.localization-codegen.UniTask` assembly, which compiles only when UniTask is installed as a package. If UniTask lives under `Assets/`, add the scripting define `LOCALIZATION_CODEGEN_UNITASK`. Without that assembly the option is skipped with a warning, because the generated code would not compile.

## Settings

Open **Project Settings > Localization Codegen**, or **Window > Localization Codegen** for the standalone window. Settings are stored in `ProjectSettings/LocalizationCodegenSettings.asset`.

| Setting | Default | Description |
|---|---|---|
| Namespace | `Localization` | Namespace of generated classes. Empty or whitespace means the global namespace. |
| Output Folder | `Assets/Generated/Localization` | Must be inside `Assets`. Changing it removes generated files from the old folder. |
| String Tables | on | Generate classes for string table collections. Disabling deletes the matching files on the next run. |
| Asset Tables | on | Generate classes for asset table collections. Disabling deletes the matching files on the next run. |
| Regenerate On Changes | on | Debounced automatic regeneration after table edits and external (VCS) changes. |
| Use Runtime Alias | on | Emit one `using __LT = ...;` instead of repeating the runtime type on every member. |
| Generate Reference Members | off | Emit the nested [`Ref` class](#reference-members). |
| XML Documentation | off | Add XML docs with the default locale text and smart string parameters. |
| Smart Strings | off (upgraded) / `Methods` (new) | `Off`, `Methods` or `MethodsAndRefs`. |
| Smart Parameter Types | `Object` | `Object` or `Inferred`. |
| Naming Convention | `Keep` | `Keep` or `PascalCase`. |
| Key Separators | empty | Characters that create nested classes, e.g. `/`. |
| UniTask Members | off | Also emit [`…Async` members](#unitask) returning `UniTask`. Requires UniTask. |
| Generate .asmdef | off | Write an assembly definition into the output folder. |
| Fail CI On Smart String Mismatch | off | Make `CheckBatch` fail on locale placeholder mismatches. |

The window also has **Regenerate Now**, **Open Output Folder** and **Run Setup Wizard** buttons, and lists the warnings of the last run.

## CI / batch mode

```bash
# Regenerate and fail on any error
Unity -batchmode -nographics -projectPath . \
  -executeMethod Severnbli.LocalizationCodegen.Editor.CodeGenerator.GenerateBatch

# Verify that generated code is up to date (no files are written)
Unity -batchmode -nographics -projectPath . \
  -executeMethod Severnbli.LocalizationCodegen.Editor.CodeGenerator.CheckBatch
```

`CheckBatch` compares the generated files on disk with the tables, so it also catches generated files that were edited by hand. The same actions are available under **Tools > Localization Codegen > Regenerate** and **Tools > Localization Codegen > Verify (Dry run)**. The setup wizard is never opened in batch mode.

For clean diffs, add this to `.gitattributes`:

```gitattributes
*.g.cs text eol=lf
```

## Good to know

- Only files in the output folder whose first line is `// <auto-generated by Localization Codegen>` are created, overwritten or deleted. Don't edit generated files by hand.
- A manifest in the project settings remembers the last output folder, so changing the folder or namespace does not leave duplicate types behind.
- If you move generated code into your own assembly definition, enable **Generate .asmdef**, or reference `Unity.Localization` and `com.severnbli.localization-codegen.Runtime` manually (plus `UniTask` and `com.severnbli.localization-codegen.UniTask` for UniTask members).
- The synchronous members block until the table is loaded and are not supported by Addressables on WebGL. Use [UniTask members](#unitask), or `LocalizedTables.GetStringAsync` / `GetAssetAsync`, there.
- If `LocalizationSettings` are missing at runtime, generated members return the table key (and log once) instead of throwing.
- The generator is editor-only; generated C# files have no runtime dependency on the generator assembly.

## License

[MIT](LICENSE) © Usevalad Buben

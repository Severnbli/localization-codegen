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
  <a href="LICENSE">
    <img alt="MIT License" src="https://img.shields.io/badge/license-MIT-green" />
  </a>
</p>

</div>

---

## Overview

Localization Codegen reads your [Unity Localization](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/index.html) table collections and generates plain C# classes for them — one static class per collection, one property per key. No runtime reflection, no string lookups in your game code, and full autocomplete plus compile-time safety.

```csharp
// Before
label.text = localizationDatabase.GetLocalizedString("Ui", "HelloWorld");

// After
label.text = Ui.HelloWorld;
```

| Feature | Description |
|---|---|
| 🪄 **First-run wizard** | A setup wizard opens automatically after the package is first imported |
| 🧩 **Typed access** | One static class per string or asset table collection, one property per key |
| 🔁 **Automatic regeneration** | Regenerates when table collections, tables or entries change |
| 🛠️ **Editor tooling** | Dedicated window with manual regeneration and settings |

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
    "com.severnbli.localization-codegen": "1.0.4"
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

## Usage

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

The `__LT` alias is declared once per file. All other types (`LocalizedString`, asset types, `Dictionary<,>`)
stay fully `global::`-qualified so user types cannot be shadowed.

Use it anywhere from your game code:

```csharp
using Localization;

label.text = Ui.HelloWorld;
```

For **asset table collections**, each property is typed from the entry's asset type (falling back to `UnityEngine.Object`) and returns the localized asset:

```csharp
Sprite icon = Icons.Sword;
```

### Naming rules

Generated identifiers stay valid C#:

- Invalid characters become `_`.
- Names starting with a digit get a `_` prefix.
- C# keywords get an `@` prefix.
- Collisions get a numeric suffix, assigned by entry id so older keys keep their names.
- Class names collide case-insensitively (`Ui` and `UI` get separate files on Windows/macOS).
- Reserved names (`System`, `UnityEngine`, `Unity`, the first namespace segment, …) get a `Table` suffix.
- Windows device names (`CON`, `NUL`, `COM1`, …) get a trailing `_`.

With **Naming Convention = PascalCase** and optional **Key Separators**, `player name` becomes `PlayerName`
and `Menu/Start` becomes the nested class `Menu.Start`. These options are off by default so upgraded projects
keep their existing API.

### Smart strings

Enable **Smart Strings = Methods** to turn entries that need arguments into methods:

```csharp
// "{0}, you have {count:plural:one item|{} items}"
public static string Greeting(object arg0, int count)
    => __LT.GetStringMixed(TableName, "Greeting", new object[] { arg0 },
        new global::System.Collections.Generic.Dictionary<string, object> { ["count"] = count });
```

- Positional placeholders (`{0}`) become `arg0`, `arg1`, … (gaps are filled).
- Named placeholders (`{player-name}`, `{player.Name}`) become camelCase parameters. The original name is
  preserved for the runtime lookup.
- `{}` (the current value inside a formatter) is not a parameter.
- Variables provided by a **Global Variables Group** are not parameters; Unity resolves them.
- **Smart Parameter Types = Inferred** uses `int` for `plural`, `IEnumerable` for `list`, otherwise `object`.
- Placeholder differences between locales are reported as warnings. Enable
  **Fail CI On Smart String Mismatch** to make `CheckBatch` fail on them.
- **MethodsAndRefs** additionally emits `Ref.Greeting(...)` returning a `LocalizedString` whose arguments
  and local variables are set.

Smart strings are opt-in. Upgraded projects default to `Off`; new projects default to `Methods` when the
setup wizard completes.

### Reference members

Enable **Generate Reference Members** to emit a nested `Ref` class:

```csharp
public static class Ref
{
    public static global::UnityEngine.Localization.LocalizedString HelloWorld
        => new global::UnityEngine.Localization.LocalizedString(TableName, "HelloWorld");
}
```

Asset entries use `LocalizedSprite`, `LocalizedAudioClip`, `LocalizedAsset<T>`, and the other built-in
localized asset types when available.

## Settings

Open **Project Settings > Localization Codegen**, or **Window > Localization Codegen** for the standalone
window. Settings are stored in `ProjectSettings/LocalizationCodegenSettings.asset`.

| Setting | Default | Description |
|---|---|---|
| Namespace | `Localization` | Namespace of generated classes. Empty or whitespace means the global namespace. |
| Output Folder | `Assets/Generated/Localization` | Must be inside `Assets`. Changing it removes generated files from the old folder. |
| String Tables | on | Generate classes for string table collections. Disabling deletes the matching files on the next run. |
| Asset Tables | on | Generate classes for asset table collections. Disabling deletes the matching files on the next run. |
| Regenerate On Changes | on | Debounced automatic regeneration after table edits and external (VCS) changes. |
| Use Runtime Alias | on | Emit one `using __LT = ...;` instead of repeating the runtime type on every member. |
| Generate Reference Members | off | Emit the nested `Ref` class. |
| XML Documentation | off | Add XML docs with the default locale text and smart string parameters. |
| Smart Strings | off (upgraded) / `Methods` (new) | `Off`, `Methods` or `MethodsAndRefs`. |
| Smart Parameter Types | `Object` | `Object` or `Inferred`. |
| Naming Convention | `Keep` | `Keep` or `PascalCase`. |
| Key Separators | empty | Characters that create nested classes, e.g. `/`. |
| Generate .asmdef | off | Write an assembly definition into the output folder. |
| Fail CI On Smart String Mismatch | off | Make `CheckBatch` fail on locale placeholder mismatches. |

## CI / batch mode

```bash
# Regenerate and fail on any error
Unity -batchmode -nographics -projectPath . \
  -executeMethod Severnbli.LocalizationCodegen.Editor.CodeGenerator.GenerateBatch

# Verify that generated code is up to date (no files are written)
Unity -batchmode -nographics -projectPath . \
  -executeMethod Severnbli.LocalizationCodegen.Editor.CodeGenerator.CheckBatch
```

The same actions are available under **Tools > Localization Codegen > Regenerate** and
**Tools > Localization Codegen > Verify (Dry run)**. The setup wizard is never opened in batch mode.

For clean diffs, add this to `.gitattributes`:

```gitattributes
*.g.cs text eol=lf
```

## Good to know

- Only files in the output folder whose first line is `// <auto-generated by Localization Codegen>` are created, overwritten or deleted. Don't edit generated files by hand.
- A manifest in the project settings remembers the last output folder, so changing the folder or namespace does not leave duplicate types behind.
- If you move generated code into your own assembly definition, enable **Generate .asmdef** or reference `Unity.Localization` and `com.severnbli.localization-codegen.Runtime` manually.
- Generated members use the synchronous localization APIs. They block until the table is loaded and are not
  supported by Addressables on WebGL. Use `LocalizedTables.GetStringAsync` / `GetAssetAsync` there.
- If `LocalizationSettings` are missing at runtime, generated members return the table key (and log once)
  instead of throwing.
- The generator is editor-only; generated C# files have no runtime dependency on the generator assembly.

## License

[MIT](LICENSE) © Usevalad Buben

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

### OpenUPM (recommended)

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

## Requirements

| Requirement | Version |
|---|---|
| Unity | 2021.3 or newer |
| `com.unity.localization` | 1.5.x (installed automatically as a dependency) |

## Usage

Given a string table collection named `Ui` containing the key `HelloWorlf`, the generator produces `Ui.g.cs`:

```csharp
namespace Localization
{
    public static class Ui
    {
        public const string TableName = "Ui";

        public static string HelloWorld =>
            LocalizedTables.GetString(TableName, "HelloWorld");
    }
}
```

Use it anywhere from your game code:

```csharp
using Localization;

label.text = Ui.HelloWorld;
```

For **asset table collections**, each property is typed from the entry's asset type (falling back to `UnityEngine.Object`) and returns the localized asset:

```csharp
Sprite icon = Icons.Sword;
```

For strings with arguments or smart strings, use `LocalizedTables.GetString(Ui.TableName, "Key", args)`.

### Naming rules

Generated identifiers stay valid C#:

- Invalid characters become `_`.
- Names starting with a digit get a `_` prefix.
- C# keywords get an `@` prefix.
- Collisions get a numeric suffix.

## Settings

Open **Window > Localization Codegen**. Settings are stored in `ProjectSettings/LocalizationCodegenSettings.asset`.

| Setting | Default | Description |
|---|---|---|
| Namespace | `Localization` | Namespace of generated classes (leave empty for the global namespace). |
| Output Folder | `Assets/Generated/Localization` | Must be inside `Assets`. |
| String Tables | on | Generate classes for string table collections. |
| Asset Tables | on | Generate classes for asset table collections. |
| Regenerate On Changes | on | Regenerate automatically when collections change. |

Buttons:

- **Regenerate Now** — regenerate the code immediately.
- **Run Setup Wizard** — reopen the first-run wizard.

## Good to know

- Only files in the output folder whose first line is `// <auto-generated by Localization Codegen>` are created, overwritten or deleted. Don't edit generated files by hand.
- If you move generated code into your own assembly definition, reference `Unity.Localization` and `com.severnbli.localization-codegen.Runtime`.

## License

[MIT](LICENSE) © Usevalad Buben

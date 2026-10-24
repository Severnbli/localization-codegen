# Changelog

## [1.2.1] - 2026-10-24

### Added
- Picking an output folder inside another assembly definition (directly or through an `.asmref`) offers to add
  the references the generated code needs to that `.asmdef`, or to give the code its own `.asmdef`. The
  **Output** settings show which assembly the code compiles into, and generation warns about missing references.

### Changed
- Restyled settings: collapsible sections, consistent label widths, options that do not apply are greyed out,
  and a shared toolbar (Regenerate, Verify, Open Output Folder, Setup Wizard) with a last-run summary. Every
  setting can be found through the Project Settings search.

### Fixed
- A folder picked with **Browse** was not saved.
- `AssemblyBuilder` obsolete warning in the editor tests on Unity 6.5.

## [1.2.0] - 2026-10-24

### Added
- Optional UniTask support. Enable **UniTask Members** to emit a `...Async` method returning `UniTask` next to
  every string and asset member, with a `CancellationToken` and the same smart string parameters.
- `LocalizedTablesUniTask` (`GetStringAsync`, `GetAssetAsync`) and the `LocalizedString.GetLocalizedStringUniTask()`
  extension in the optional `com.severnbli.localization-codegen.UniTask` assembly, which compiles only when UniTask
  (`com.cysharp.unitask`) is installed or `LOCALIZATION_CODEGEN_UNITASK` is defined.
- Editor tests compile the generated code with Unity's compiler, with warnings as errors.

### Fixed
- `Ref` members of asset table entries did not compile: `LocalizedSprite`, `LocalizedAsset<T>` and the other
  localized asset types have no `(table, entry)` constructor.

## [1.1.1] - 2026-10-24

### Fixed
- `Verify (Dry run)` and `CheckBatch` compare the generated code on disk with the tables, so a
  hand-edited generated file is reported as out of date.
- Generated files stay tracked for cleanup when a run skips cleanup (a collection failed to read or
  no tables were found), and hashes of removed collections are no longer kept forever.
- XML documentation is truncated before escaping, so an entity such as `&amp;` is never cut in half.
- `Ref` members avoid inherited `object` member names, like the table class members.
- The installer registers the package's own OpenUPM scope (it registered unrelated template scopes,
  so the package could not be resolved) and installs the current package version.

## [1.1.0] - 2026-10-21

### Added
- Optional smart string support (`Smart Strings`): entries with placeholders become methods with
  positional and named parameters. Methods can also be emitted in the nested `Ref` class.
- Optional `Ref` class with `LocalizedString`, `LocalizedAsset<T>` and built-in localized asset
  properties.
- Optional XML documentation containing the default locale text.
- Naming options: `PascalCase` and key separators that create nested classes.
- Project Settings page (`Project/Localization Codegen`).
- Optional generated `.asmdef` for output folders owned by another assembly.
- Batch mode entry points: `GenerateBatch` and `CheckBatch` (`Tools/Localization Codegen` menu items
  are also available).
- Incremental generation using a per-collection hash.
- Aggregated diagnostics in one console message and in the editor window.
- Editor tests and runtime tests.

### Fixed
- A1: consistent handling of the global namespace, `global::` qualification, nested public asset
  types and reserved class names.
- A2: stale generated files are removed after changing the output folder or namespace.
- A3: case-insensitive file-name collisions (`Ui` / `UI`) no longer overwrite each other; Windows
  device names are avoided.
- A4: complete string-literal and comment escaping (U+0085, U+2028, U+2029, C1 controls, lone
  surrogates).
- A5: one path validation implementation for the generator, window, wizard and settings.
- A6: pending regeneration survives domain reloads; regeneration is debounced, suspended while
  compiling/playing, and detects external table edits.
- A7: the wizard is shown at most once per session, never in batch mode, and Skip disables automatic
  regeneration.
- A8: asset type detection uses player assemblies and supports public nested types.
- A9: CRLF checkouts are no longer rewritten on every run.
- A10: deterministic class/member names, with string tables winning over asset tables.
- A11: one broken collection no longer aborts the run; cleanup is skipped on failure and an empty
  transient collection list does not delete everything automatically.
- A12: structured `GenerateResult`, quieter logging, and `AssetDatabase` batching.
- A13: member names avoid inherited `object` members and surrogate pairs are handled correctly.
- A14: runtime helper guards missing `LocalizationSettings` and exposes asynchronous lookups.
- A15: consistent test assembly constraints, package metadata and documentation.

### Changed
- Generated members reference the runtime helper through a single `__LT` file-level alias by default.
  Set `UseRuntimeAlias` to false to restore the previous verbose output.
- Upgraded projects keep `Smart Strings = Off`, so existing generated APIs do not change.

## [1.0.1] - 2025-03-29
### Fixed
- Resolved a bug causing crashes when initializing the package on Unity 2021.3.
- Fixed incorrect behavior in the custom editor window.

## [1.0.0] - 2025-03-15
### Added
- Initial release of the Unity package.
- Added core functionality for A, B, and C.
- Included documentation and example scenes.

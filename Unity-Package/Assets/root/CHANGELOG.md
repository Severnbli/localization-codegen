# Changelog

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
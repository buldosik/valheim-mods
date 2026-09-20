---
name: valheim-mod-development
description: 'Create, modify, review, and debug Valheim mods built with BepInEx, Harmony, Unity, and .NET Framework. Use for plugin lifecycle, game API compatibility, reflection, patches, inventory/container behavior, multiplayer safety, dependency checks, and build validation.'
argument-hint: '[modification or bug to implement]'
---

# Valheim Mod Development

## Purpose

Use this skill for source changes in Valheim BepInEx plugins. Prefer the smallest change that preserves the plugin API, game version, dependency versions, and behavior of custom-container mods.

## Before editing

1. Read the target plugin, project file, build script, README, and any review notes that cover the affected behavior.
2. Identify the exact game/mod versions and assembly that define the API. Do not infer signatures from a different Valheim or dependency version.
3. Locate the owning code path: plugin lifecycle, Harmony patch, callback, inventory operation, or persistence path.
4. State one falsifiable hypothesis about the bug or requested behavior and one cheap check that could disprove it.
5. Check whether the behavior runs on the local client, the server/owner, or both. Treat inventory writes and container saves as multiplayer-sensitive.

## Implementation rules

- Target the framework already declared by the project. Do not add NuGet replacements for Unity, Valheim, BepInEx, or mod assemblies.
- Preserve `BepInPlugin` identifiers and dependency declarations unless the task explicitly changes release compatibility.
- Prefer public Valheim APIs. Use reflection only when the required API is private or when framework incompatibility makes a direct reference unsafe.
- When using reflection, validate the type, method name, parameter types, return type, and null/error paths. Log an actionable failure and keep the plugin loadable when an optional API is unavailable.
- Register callbacks and patches once, retain enough state to unregister them on destruction, and make repeated initialization harmless.
- Keep discovery and ownership in the dependency that owns it. For CraftFromContainers integrations, use its container discovery rather than reimplementing range, access, type filters, or key handling.
- For inventory reads, avoid mutating live item data unless the API explicitly requires it. Clone items before presenting adjusted counts such as `LeaveOne`.
- Make `Count`, preview/list, and removal callbacks agree on filters, item identity, reserved quantities, world level, quality, and custom data.
- Treat a successful in-memory removal as different from durable/networked persistence. Do not call an operation transactional unless ownership, synchronization, save success, and conflict behavior are verified in-game.
- Catch failures at external boundaries such as reflection, mod callbacks, container access, and save operations. Do not swallow errors without a useful log message.
- Do not change unrelated release metadata or copy generated DLLs into source folders during development.

## Validation workflow

1. Run the narrowest relevant check immediately after the edit.
2. Compile the project with the installed game references. Use the command documented by that mod's build script, for example:

   ```powershell
   dotnet build .\EpicLootCraftFromContainers.csproj -c Release --no-restore -p:ValheimDir='D:\Steam\steamapps\common\Valheim'
   ```

   Use `build.ps1` when the local PowerShell execution policy permits it.
3. For inventory or integration changes, manually test the affected path with vanilla containers and every supported custom-container provider.
4. Test disabled dependencies, missing optional APIs, empty inventories, insufficient materials, reserved-one settings, and repeated plugin load/unload where applicable.
5. For writes, test persistence after leaving/rejoining the world and two clients interacting with the same container. If this cannot be tested, document it as an unverified risk.
6. Report build output, manual scenarios, versions tested, and remaining risks. Never imply that compilation proves in-game or multiplayer correctness.

## Review checklist

- Does the code use the API version actually present in the local references and game installation?
- Can a missing dependency, method, container, player, or inventory cause a null reference or plugin load failure?
- Are callbacks consistent about what counts as a valid item?
- Can a callback partially modify state and still report full success?
- Is a save or network ownership assumption being made without a test?
- Are logs actionable without flooding the game log on every callback?
- Does the change preserve custom-container interception and existing configuration semantics?

## Per-project references

Read the target mod's own `Plugin.cs`, project file, build script, README, and `REVIEW.md`. Treat unresolved review findings as current constraints until a focused in-game test disproves them.

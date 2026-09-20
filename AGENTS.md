# Valheim mod development workspace

This workspace contains multiple Valheim mod projects. Apply these rules to every mod unless a nearer `AGENTS.md` adds project-specific constraints.

## Repository layout

- Each mod should keep its source, project file, build script, README, and review notes in its own directory.
- `lib/` contains local game/mod reference assemblies and should not be replaced with NuGet packages.
- `bin/`, `obj/`, and `dist/` are generated output and must not be committed.
- `Dll/` and packaged release directories are release inputs/outputs, not source code.

## Development rules

- Identify the target Valheim, BepInEx, Unity, and dependency versions before changing API usage.
- Preserve plugin GUIDs, dependency declarations, target frameworks, and public behavior unless the task explicitly changes compatibility.
- Prefer public Valheim and dependency APIs. Use Harmony or reflection only when the owning API requires it, and validate signatures and failure paths.
- Keep discovery and ownership in the dependency that owns it. Do not duplicate container, player, range, access, or permission logic without a demonstrated reason.
- Treat inventory writes, ownership changes, and saves as multiplayer-sensitive. A successful local mutation is not proof of durable or synchronized state.
- Keep preview, count, and removal paths consistent about filters, item identity, reserved quantities, quality, world level, and custom data.
- Preserve interception points used by custom-container mods.
- Keep logs actionable and avoid logging repeatedly from hot callbacks.

## Codex workflow

For Valheim-specific implementation, review, or debugging, load `.github/skills/valheim-mod-development/SKILL.md` and follow its checklist.

1. Read the target mod's README, review notes, project file, build script, and affected source path.
2. State one local behavior hypothesis and one cheap validation that could disprove it.
3. Make the smallest compatible edit.
4. Run the narrowest relevant build or test immediately after editing.
5. Report tested versions, build output, manual scenarios, and unverified multiplayer risks.

## Adding a new mod

Create a dedicated directory with:

- source and project files
- a repeatable `build.ps1` or equivalent build command
- a README with dependencies and manual test scenarios
- review notes for known compatibility or multiplayer risks
- local references kept outside Git or explicitly documented

Do not copy generated DLLs or a second `.git` directory into the new mod folder.

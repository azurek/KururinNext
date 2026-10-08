---
name: godot-dotnet-build-test-run
description: 'Build, test, export, launch, or troubleshoot the KururinNext Godot 4.7.2 .NET/C# project and its Windows executable.'
argument-hint: '[build|test|export|run|all]'
user-invocable: true
---

# KururinNext Build, Test, Export, and Run

Use this skill for build verification, unit tests, Godot project smoke checks, Windows exports, or launching the generated executable.

## Project Facts

- This is a Godot 4.7.2 .NET project targeting .NET 8.0.
- `KururinNext.sln` includes the game and `Tests/KururinNext.Tests.csproj`.
- `project.godot` selects `Main.tscn`; `export_presets.cfg` defines the `Windows Desktop` preset and embeds project data.
- `appsettings.json` contains gameplay tuning such as `gameplay.collision_recoil_radians`; the Windows export preset explicitly includes this file.
- This machine's Godot console executable is `e:\Git\godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`. In PowerShell set `$Godot` to that path and invoke it with `& $Godot`.
- `dotnet build` compiles projects but does not produce the standalone game executable. Godot export creates `build/KururinNext.exe`.
- `package.json` is not applicable unless a separate Node.js tool is added. Do not introduce it as a substitute for the .NET or Godot configuration.

## Workflow

Run commands from the repository root in PowerShell.

1. Verify prerequisites: .NET 8 SDK, Godot 4.7.2 .NET, and the matching Godot export templates for exporting. In PowerShell set the known Godot path:

   ```powershell
   $Godot = 'e:\Git\godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
   ```

   Use `& $Godot` to invoke it; do not rely on `godot` being on `PATH`.
2. Build both solution projects:

   ```powershell
   dotnet build .\KururinNext.sln
   ```

3. Run the unit tests:

   ```powershell
   dotnet test .\KururinNext.sln
   ```

4. When a Godot runtime is available, smoke-check the source project:

   ```powershell
   & $Godot --headless --path . --quit-after 3
   ```

   Do not report this check as passed if the Godot executable is unavailable or the process exits with an error.
5. To create the standalone Windows executable, ensure the matching export templates are installed and export the configured preset:

   ```powershell
   & $Godot --headless --path . --export-release "Windows Desktop" "build/KururinNext.exe"
   ```

   Confirm the command succeeds and `Test-Path .\build\KururinNext.exe` returns `True`.
6. Run the exported game normally:

## Gameplay Tuning

Adjust `gameplay.collision_recoil_radians` in the repository-root `appsettings.json` to tune the rotor's backward recoil on impact. The value is in radians, defaults to `0.7`, and is clamped to the range 0 through pi. The game reads the file at startup, so relaunch after changing it; re-export to distribute the changed value.

   ```powershell
   .\build\KururinNext.exe
   ```

   A bounded headless startup check is:

   ```powershell
   .\build\KururinNext.exe --headless --quit-after 3
   ```

   Exported executables may reject `--path` overrides; do not pass `--path .` to the exported game. This check validates the exported artifact, not interactive gameplay. Manually play the exported build when gameplay behavior needs verification.

## Reporting

- Report build, tests, source-project launch, export, and executable launch as separate checks.
- Do not claim the exported executable contains current source changes unless it was exported after those changes.
- If Godot or export templates are unavailable, report that limitation and give the exact command needed after installation.
- Keep fixes scoped to the failing check; preserve user changes and do not add Node tooling unless the project actually needs it.
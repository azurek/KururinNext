# KururinNext
Spiritual successor to Kuru Kuru Kururin, built with Godot .NET and C#.

## Build, test, export, and run

Prerequisites: .NET 8 SDK and Godot 4.7.2 .NET, including the matching export templates. Run these commands from the repository root in PowerShell:

This machine's Godot console executable is:

```powershell
$Godot = 'e:\Git\godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
```

```powershell
dotnet build .\KururinNext.sln
dotnet test .\KururinNext.sln
```

`dotnet build` compiles the game and test projects. `dotnet test` runs the xUnit tests. Neither command creates the standalone game executable.

To run the project through Godot, open `project.godot` in Godot 4.7.2 .NET and press F6/F5 or Run Project. A headless source-project smoke check is:

```powershell
& $Godot --headless --path . --quit-after 3
```

If `godot` is not on `PATH`, replace it with the full path to the Godot 4.7.2 .NET console executable. To export the configured Windows Desktop preset, make sure the matching Godot export templates are installed, then run:

```powershell
& $Godot --headless --path . --export-release "Windows Desktop" "build/KururinNext.exe"
```

The preset embeds the game data in `build/KururinNext.exe`. Launch it normally with:

```powershell
.\build\KururinNext.exe
```

For a short headless smoke check of the exported executable:

```powershell
.\build\KururinNext.exe --headless --quit-after 3
```

Do not add a `--path` override to the exported executable; exported builds may not support project-path overrides. Move with WASD/arrows or a controller stick/D-pad, adjust continuous rotor spin with Q/E or the controller bumpers, retry with R/controller Y, and pause with Escape/P/controller Start. These bindings are configurable in Project Settings > Input Map.

To tune gameplay, edit [appsettings.json](appsettings.json). `gameplay.collision_recoil_radians` controls collision recoil (default `0.7` radians, clamped to 0 through pi). `gameplay.rotor_half_length` controls the rotor arm's center-to-tip length in pixels (default `51`, clamped to 10 through 150). Both settings are loaded at startup and included in Windows exports.

## Level editor and data

Choose **LEVEL EDITOR (DEV)** from the main menu. The editor can create, open, validate, playtest, and save levels. Bundled examples live in `Levels/`; user-authored levels are saved to `user://levels/<id>.json`. **SAVE** writes the current level ID, while **SAVE AS** uses the ID field as a new destination. Playtest returns to the same editor state without saving or recording stage progression.

Level files are versioned JSON (`version: 1`) with a level `id`, display `name`, `corridorHalfWidth`, `length`, and an `elements` array. Elements use `type`, center-position `x`/`y`, and `width`/`height`; wall rotation and diamond radius are supported, while gates use `width` as the opening width. Marker `width` is its trigger radius. Supported types are `wall`, `gate`, `diamond`, `piston`, `start`, `checkpoint`, and `finish`. A level must have exactly one of each marker. Unknown types, malformed JSON, unsupported versions, and invalid dimensions are rejected before save or playtest.

The first progression stage keeps its `course-01` ID and now loads `Levels/course-01.json`. A valid file at `user://levels/course-01.json` overrides the bundled level; if that override is invalid, the bundled copy is used. Other progression stages continue using their existing runtime builders.

This is a Godot .NET/C# project, so `package.json` is not its build or dependency manifest. Use the solution and project files for .NET, and Godot's project and export configuration for engine tasks. Add `package.json` only if a separate Node.js tool is introduced.

For future Copilot build, test, export, or run tasks, invoke `/godot-dotnet-build-test-run` or read the [workspace skill](.github/skills/godot-dotnet-build-test-run/SKILL.md).

## Development prompts

Run these workspace prompts in order from Copilot Chat (type `/` and choose the prompt). Each prompt is one incremental implementation task:

1. [01 - Create the Godot .NET project](.github/prompts/01-project-foundation.prompt.md)
2. [02 - Build the main menu](.github/prompts/02-main-menu.prompt.md)
3. [03 - Make the first level playable](.github/prompts/03-first-playable-level.prompt.md)
4. [04 - Add world and stage progression](.github/prompts/04-progression.prompt.md)
5. [05 - Build a level editor](.github/prompts/05-level-editor.prompt.md)
6. [06 - Add gameplay mechanics](.github/prompts/06-gameplay-mechanics.prompt.md)
7. [07 - Create a second world and plan expansion](.github/prompts/07-world-expansion.prompt.md)
8. [08 - Polish and verify Windows/Linux builds](.github/prompts/08-cross-platform-release.prompt.md)

The game should use original names, art, audio, level layouts, and other assets. Treat the original game as inspiration for the rotating-barrier navigation concept, not as a source of extracted assets or copied levels.

# KururinNext
Spiritual successor to Kuru Kuru Kururin, built with Godot .NET and C#.

## Run the project

Open this folder's `project.godot` with Godot 4.7.2 .NET. The C# project targets .NET 8.0 and uses Godot's matching 4.7.2 .NET SDK package. To build from a terminal with the .NET SDK installed, run `dotnet build KururinNext.csproj` from this folder. To launch the main scene headlessly for a smoke check, run `godot --headless --path . --quit-after 3` from this folder (or replace `godot` with the path to your Godot .NET console executable). In the editor, press F6/F5 or use the Run Project button to see the startup screen.

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

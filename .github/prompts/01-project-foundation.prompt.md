---
description: "Create the initial Godot .NET C# project in the open KururinNext folder"
name: "01 - Create Godot .NET project"
argument-hint: "Optional: Godot version or project constraints"
agent: "agent"
---

Create the initial playable-project foundation for KururinNext in the currently open workspace folder.

Before editing:
- Inspect the current files and git status. Preserve existing user work; do not replace the repository or create a nested project directory.
- Identify the installed Godot version, whether it is the .NET-enabled build, and the installed .NET SDK. Use versions compatible with each other instead of guessing a target framework.
- If the required Godot .NET build or SDK is missing, explain the exact prerequisite and stop before making a broken project.

Implement:
- Initialize this folder as a Godot .NET project using the installed Godot editor or its supported project-generation workflow.
- Set the project identity to KururinNext and keep the project structure conventional and small.
- Add a minimal C#-backed main scene that launches and visibly confirms the project runs. Do not implement game mechanics yet.
- Add appropriate ignore rules for generated Godot and .NET files without ignoring source assets or project files.
- Add only the project documentation needed to explain how to open and run it.

Acceptance criteria:
- The project opens in the installed Godot .NET editor without project-configuration errors.
- The C# project builds with the installed SDK, and the main scene can run.
- The setup remains suitable for Godot exports to both Windows and Linux; do not add OS-specific dependencies.
- Report the detected Godot and .NET versions, files created, and verification performed. Do not claim a check passed unless it was run.
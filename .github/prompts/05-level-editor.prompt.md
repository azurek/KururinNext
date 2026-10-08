---
description: "Build an in-game level editor with validation, playtest, and save/load"
name: "05 - Build a level editor"
argument-hint: "Optional: editor workflow or file format preferences"
agent: "agent"
---

Add a usable in-game level editor to the existing Godot .NET game. This is a game-integrated editor for authoring KururinNext levels, not a replacement for the Godot editor.

First inspect how the first level is represented and loaded. Propose the smallest data format that fits the current project, then implement it consistently. Preserve the existing playable level by migrating it or providing a compatible loader; do not silently break progression.

Editor capabilities:
- Create, open, and save level data using a documented, versionable format and Godot's user-data or project-resource conventions appropriate for editable content.
- Place, select, move, rotate, and remove the gameplay geometry and level markers already supported by the runtime, including start, checkpoint, and finish.
- Provide clear selection feedback, grid/snap controls, undo/redo for common edits, and keyboard/controller-friendly access where practical.
- Validate required markers and invalid geometry, show actionable errors, and prevent malformed levels from crashing the game.
- Playtest the current level and return to editing without losing unsaved work; clearly distinguish Save from Save As if both exist.
- Include one small test level and a validation path for save/load round trips.

Keep editor-only actions out of normal player flow except for a deliberate menu entry suitable for development. Do not allow level data to execute arbitrary code.

Acceptance criteria:
- Create/edit/save/reopen/playtest works end to end, including a save/load round-trip check.
- Invalid or incomplete levels produce readable validation feedback.
- Existing first-level play and progression still work after the format integration.
- Run focused tests and a Godot/.NET build; describe any manual checks still needed.
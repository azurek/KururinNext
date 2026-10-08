---
description: "Implement the first navigable title menu for KururinNext"
name: "02 - Build the main menu"
argument-hint: "Optional: menu options or visual direction"
agent: "agent"
---

Build the game's first main menu on top of the existing Godot .NET project.

Before editing, inspect the project, current main scene, input setup, and existing conventions. Preserve working project setup and avoid unrelated refactors. If the project foundation is missing, report that this prompt depends on `/01 - Create Godot .NET project` rather than scaffolding a competing structure.

Implement a focused title screen with:
- A clear KururinNext title and Play, Options, and Quit actions.
- Keyboard and controller navigation, visible focus, and sensible initial focus.
- An options view with practical audio controls that persist across launches if the project architecture supports persistence cleanly.
- A transition from Play to a clearly identified temporary gameplay entry point; do not fake a completed level.
- Layout that remains usable at common desktop resolutions and scales without clipping.

Keep presentation and assets original. Use Godot's existing UI patterns and C# conventions; do not add packages unless the project genuinely needs them.

Acceptance criteria:
- The app starts at the menu, all actions work, and the temporary Play destination can return to the menu.
- Menu navigation works with keyboard and controller, with no focus trap.
- Build and run the project using the available Godot/.NET tools and report exactly what was verified.
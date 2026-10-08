---
description: "Implement the rotating-barrier controls and first playable KururinNext level"
name: "03 - Make the first level playable"
argument-hint: "Optional: desired control scheme or level length"
agent: "agent"
---

Replace the temporary Play destination with a small, complete first gameplay loop in the existing Godot .NET project.

Inspect the current menu and project architecture first. Keep the menu working and reuse its Play flow. If the menu milestone is not present, do not create a second competing application entry point.

Implement:
- A controllable character carrying a rotating bar/propeller whose swept shape must fit through passages. Use original character and object design; make rotation and movement readable.
- Responsive keyboard controls and controller support, with input actions configurable through Godot's input map. Separate movement, rotation, collision, and presentation responsibilities enough to tune them independently.
- A short original first level with a start, several passages of varied clearance, a checkpoint, and a finish.
- Clear collision feedback, a restart action, checkpoint recovery, and a finish state that returns to the menu or advances through an explicit button.
- A minimal HUD for pause/retry and clear status feedback; prevent gameplay input from leaking into paused menus.

Use Godot physics and collision shapes consistently. Avoid brittle per-frame manual collision checks, copied level layouts, or external game assets.

Acceptance criteria:
- A player can start from the menu, navigate the course, fail and retry, recover at the checkpoint, and finish the level.
- The bar visibly rotates and collision matches its intended swept geometry closely enough to be fair.
- Validate through a Godot/.NET build and the most relevant runnable or automated check available; report any checks that require manual play.
---
description: "Add world and stage selection with saved completion progress"
name: "04 - Add world and stage progression"
argument-hint: "Optional: progression or save-data constraints"
agent: "agent"
---

Add a small, data-driven world/stage selection and progression flow to the existing game.

Inspect the menu, first level, and current scene-loading and save-data code. Preserve the playable loop. Do not duplicate scene routing or create a parallel save system if one already exists.

Implement:
- A world/stage model that identifies stages, display names, scenes or level data, and unlock requirements.
- A stage-select screen reachable from the menu, with locked/unlocked/completed states and a clear way back.
- Completion handling that unlocks the next stage and persists progress across application restarts using a small, versionable save format.
- Safe handling for a missing, malformed, or older save file: start with valid default progress rather than crashing.
- Register the existing first level as the first stage without changing its gameplay behavior.

Keep this milestone small: provide enough structure for multiple worlds, but do not author the second world yet. Keep save paths portable across Windows and Linux by using Godot's user-data APIs.

Acceptance criteria:
- A fresh save can play the first stage; completing it unlocks the next configured stage.
- Progress survives restart, and missing or invalid save data does not prevent startup.
- Build and run the relevant flow or tests, and state what was verified.
---
description: "Polish the playable game and verify Godot .NET builds and exports for Windows and Linux"
name: "08 - Verify Windows and Linux builds"
argument-hint: "Optional: target Godot export templates or release constraints"
agent: "agent"
---

Prepare the current game for a reliable desktop playtest on Windows and Linux, focusing on validation and release readiness rather than adding new gameplay.

Inspect the installed Godot .NET version, project configuration, existing export presets, and available export templates. Preserve existing user files and settings. Do not claim a platform export works unless it was actually produced or tested.

Implement only necessary fixes and documentation for:
- Consistent startup, scene/resource paths, input behavior, save paths, and editor/runtime separation across Windows and Linux.
- Clear error handling for missing save data, missing level content, and invalid project configuration.
- A documented repeatable build/export procedure using the installed Godot .NET SDK and export templates, including any platform prerequisites.
- Minimal smoke tests or a manual test checklist covering menu, first level, checkpoint/retry, progression, level editing, save/load, and the second world.
- Avoiding machine-specific absolute paths, OS-only packages, or checked-in generated build output.

Acceptance criteria:
- Run the available .NET build and Godot validation checks.
- If export templates are installed, produce or test Windows and Linux exports; otherwise state exactly what is missing and give the commands/settings needed once installed.
- Report platform-specific results and remaining risks without implying that an untested build is verified.
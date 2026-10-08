---
description: "Add configurable wind, moving enemies, and expanding or contracting passages"
name: "06 - Add gameplay mechanics"
argument-hint: "Optional: choose or prioritize mechanics"
agent: "agent"
---

Extend the existing level/runtime systems with a small, coherent set of optional mechanics: wind zones, moving enemies or hazards, and passages that expand or contract over time.

Inspect player movement, collision, level data, and the level editor before changing them. Build on existing abstractions. If the editor or data-driven level format is missing, implement only the most useful mechanic in the current architecture and report the dependency rather than introducing an incompatible content system.

Implement each mechanic as a configurable level element:
- Wind zones apply a predictable directional force to the player or rotating bar, with visible indication of direction and strength.
- Moving enemies/hazards follow a simple authored path or patrol pattern with fair collision and readable warning cues.
- Expanding/contracting passages change clearance on a predictable cycle with a visual timing cue.
- Expose relevant parameters in the level editor and validate their ranges. Keep deterministic, testable timing where possible.

Ensure effects are legible, difficulty can be tuned per level, and mechanics do not make collision unfair or obscure the player's bar. Do not overload the first level; add a separate compact mechanic-test level instead.

Acceptance criteria:
- Each implemented element can be authored, saved, loaded, and playtested.
- Focused tests or deterministic checks cover its behavior and invalid settings.
- The original first-level route remains playable and the project builds.
- Clearly report any mechanic deferred and why.
# Session: Simple Mode Safety And Right Panel UX

## Date
2026-06-09 14:16

## Summary
This session stabilized Simple Mode after the native block-stack pivot. The work focused on practical robot simulation behavior, viewport quick controls, A/B marker cleanup, right panel redesign, template save/load/delete behavior, and selected-step simulation start.

## What changed
- Moved simulation execution into `Viewport3D` so playback can access robot refs, FK/IK, and collision state.
- Added per-frame Cartesian MoveL playback for Move A->B using TCP interpolation plus IK solving.
- Added collision guard before applying simulation frames; colliding candidate frames stop playback and keep the robot at the previous safe frame.
- Added unreachable-target validation using IK followed by FK error checks. Invalid A/B points are not saved, and playback stops with a message if a pose cannot be reached.
- Added user-facing viewport messages for unreachable points, IK failure, and collision-stopped simulation.
- Added a horizontal viewport toolbar for FK/IK, hitbox toggle, play/pause, stop, and camera movement hint.
- Removed the duplicate simulation controls from the right workflow panel.
- Simplified A/B markers to small dots and small A1/B1 labels, removing large labels, rings, and beacon columns.
- Updated hitbox debug colors: green clear, yellow near contact, red colliding.
- Redesigned Simple Mode right panel with compact command tiles, smaller empty state, Program section, and Quick Templates.
- Added selected-step playback: clicking a Simple Mode block selects its first step, and viewport toolbar play starts from that selected step.
- Replaced unreliable `window.prompt` save naming with an in-panel save modal.
- Persisted saved modules/workflows into Electron `block-library.json` through `readBlockLibrary` / `writeBlockLibrary`.
- Added saved template delete buttons that update `block-library.json`.

## Files touched
- `src/renderer/src/components/viewport/Viewport3D.tsx`
- `src/renderer/src/components/workflow/BlockWorkspace.tsx`
- `src/renderer/src/components/workflow/WorkflowPanel.tsx`
- `.project-memory/active-context.md`
- `.project-memory/sessions/2026-06-09-1416-simple-mode-safety-right-panel.md`

## Decisions made
- Viewport toolbar is the single place for simulation controls; the right panel should not duplicate play/stop.
- Simulation should stop before collision or unreachable target frames, because the user expects simulator behavior to respect real robot safety constraints.
- Right panel saved templates should be persisted in the existing Electron block library rather than only in transient Zustand state.
- A/B visual feedback should be small and non-obstructive: just small 3D dots plus compact labels.
- Simple Mode remains native UI, not raw Blockly.

## Bugs / risks / unresolved issues
- Collision detection remains approximate and frame-sampled; it is safer than before but not a complete swept-volume planner.
- Manual Electron verification is still needed for all UI behaviors because browser automation was not available in this session.
- Saved template UI currently displays the latest few items; a fuller template manager may be needed later.
- Real Fairino FR5 LUA verification remains open.
- Blockly dependency/helper may be unused after the native UI pivot.

## Commands / checks run

```txt
npm run typecheck
Passed after each major change.

npm run build
Passed after each major change.

git status --short --branch
feature/studio-ui-parity with code changes in Viewport3D, BlockWorkspace, and WorkflowPanel before memory save.
```

## Next recommended steps
- Launch the Electron app and manually test the saved module/workflow modal, persistence, loading, and delete behavior.
- Test selected-step playback from both Move A->B and Delay/Set DO blocks.
- Test collision and unreachable-target stop messages against deliberately invalid paths.
- Verify project save/open still preserves project-specific template arrays.
- Verify LUA export on sample Simple Mode workflows and later on a real Fairino FR5 robot pad.

## Notes for future agents
- Current repository files are source of truth; this memory is only a handoff.
- The user wants simulator behavior to avoid unsafe real-world robot states: collision or unreachable poses should not be silently simulated as if valid.
- Preserve the native compact Simple Mode direction and viewport-based A/B placement.

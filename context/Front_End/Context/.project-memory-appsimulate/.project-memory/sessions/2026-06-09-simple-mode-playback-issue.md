# Session: Simple Mode Blockly Pivot And Playback Issue

## Date
2026-06-09 12:38

## Summary
This session attempted to implement a Blockly-style Simple Mode, then pivoted toward a more product-friendly native block stack because the raw Blockly UI was visually poor, cramped, and confusing for non-technical robot users. Simple blocks, project persistence fields, Electron startup/cache fixes, viewport right-click A/B point setting, and A/B marker rendering were added or adjusted. The session ended with a critical unresolved simulation bug: the robot arm is not moving correctly from A -> B according to the user's expectation.

## What changed
- Added or retained Simple Mode data fields for Blockly/workflow persistence: `simpleBlocklyWorkspace`, `projectModules`, and `projectWorkflowTemplates`.
- Added simple block metadata to workflow steps so a Move A->B block can be represented as paired `MoveL` steps.
- Reworked Simple Mode UI toward native block cards for Move A->B, Delay, Set DO, save module, save workflow, and built-in templates.
- Added project save/load compatibility for version `1.1` while still supporting older `1.0` data.
- Added Electron wrapper script to avoid `ELECTRON_RUN_AS_NODE` breaking Electron dev startup.
- Added Electron cache command-line switches and single-instance lock to reduce repeated Windows cache errors.
- Added viewport right-click context menu to set A/B points directly in 3D space.
- Added simple waypoint marker rendering and screen labels for A/B points in the viewport.

## Files touched
- `package.json`
- `package-lock.json`
- `scripts/electron-vite-clean-env.cjs`
- `src/main/index.ts`
- `src/preload/index.ts`
- `src/preload/index.d.ts`
- `src/renderer/src/assets/main.css`
- `src/renderer/src/components/layout/Header.tsx`
- `src/renderer/src/components/workflow/BlockWorkspace.tsx`
- `src/renderer/src/components/viewport/Viewport3D.tsx`
- `src/renderer/src/i18n/translations.ts`
- `src/renderer/src/services/electronService.ts`
- `src/renderer/src/store/robotStore.ts`
- `src/renderer/src/types/robot.types.ts`
- `src/renderer/src/engine/blockly/robotBlockly.ts`

## Decisions made
- Raw Blockly canvas is not acceptable as the final non-technical Simple Mode UI without heavy UX wrapping.
- A/B point setting should happen from the 3D viewport right-click menu, not from obscure buttons on the block.
- Simple Mode should keep using `WorkflowStep[]` as the compatibility layer for Advanced Mode, simulation, and LUA export.
- Move A->B should be understandable as A and B waypoints with visible markers and a path cue in the viewport.

## Bugs / risks / unresolved issues
- Critical: the robot arm currently does not move correctly from A -> B during simulation. Likely cause: playback uses joint-space interpolation or precomputed joint angles for `MoveL`, not a per-frame Cartesian TCP interpolation from A to B with IK solving.
- Need to inspect and likely change `WorkflowPanel.runSimulation()` / playback ownership. `Viewport3D` has the robot refs and IK/FK context needed for correct MoveL playback.
- Marker visibility and right-click set behavior need manual verification after the playback fix.
- Some Blockly code/dependency may now be unused after the pivot to native Simple Mode.

## Commands / checks run

```txt
npm run typecheck
Passed earlier in the session after the Simple Mode and viewport changes.

npm run build
Passed earlier in the session after the Simple Mode and viewport changes.

git status --short
Clean in C:\Users\Yukina\Downloads\fairobot-studio at session save time.
```

## Next recommended steps
- Fix simulation playback for `MoveL` / Move A->B so TCP follows a Cartesian path from current TCP to A and then A to B.
- Prefer implementing playback in or through `Viewport3D`, where `computeFK`, `computeIK`, robot refs, and current joint state are available.
- During playback, interpolate TCP pose per frame, solve IK each frame, update joint angles, and check `isPlaying` for cancellation.
- Keep Delay and DO behavior simple: Delay sleeps for the configured ms; DO steps should advance without trying to animate joints.
- Re-run `npm run typecheck` and `npm run build`, then manually test right-click A/B markers and play simulation.

## Notes for future agents
- The user's key complaint at session end: "cái tay đang không di chuyển đúng ý người dùng" for A -> B. Treat this as the first bug to fix next session.
- Do not reintroduce raw Blockly as the visible primary workspace unless explicitly requested; the user found it ugly and confusing.
- The viewport right-click A/B flow is the expected UX direction.
- Current repository files remain the source of truth; this memory is only a handoff.

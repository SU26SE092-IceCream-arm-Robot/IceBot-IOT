# Session: Visual Scratch, Unit Conversions, Native Menu, and Viewport Interaction Fix

## Date
2026-05-26 11:48

## Summary
In this session, we refactored the project architecture to follow Electron best practices and added advanced interactive features. We built a drag-and-drop Scratch-like Lego visual programming workspace, implemented English/Vietnamese localization, integrated a native window menu bar, added custom units conversion settings (metric/radian), and fixed a major bug where the 3D viewport robot arm locked/froze during IK and direct joint dragging.

## What changed
- **Robot Arm viewport interactions**: Modified `Viewport3D.tsx` to stop re-attaching or resetting dummy target coords in `useEffect` when `transformControls.dragging` is active. Removed `jointAngles` from TransformControls sync dependency array.
- **Scratch Visual blocks programming**: Created `BlockWorkspace.tsx` and updated `WorkflowPanel.tsx` to render interactive visual coding blocks (Rotate, Move TCP, DO, delays) which automatically compile to LUA instructions and pre-calculate workspace joint states.
- **Units Conversion Support**: Updated `robotStore.ts` to host `lengthUnit` and `angleUnit` global configs. Updated `RobotSidebar.tsx`, `BlockWorkspace.tsx`, and `WorkflowPanel.tsx` to display mm/m and deg/rad measurements with on-the-fly math formulas.
- **i18n translation system**: Created `translations.ts` dictionary and integrated Globe switcher in `Header.tsx` to translate all texts cleanly.
- **Electron Integration**: Created `menu.ts` for Native App menu options, updated `index.ts` to load it, and set up `electronService.ts` to isolate IPC processes from headless web dependencies.

## Files touched
- `src/main/index.ts`
- `src/main/menu.ts` (NEW)
- `src/preload/index.ts`
- `src/preload/index.d.ts`
- `src/renderer/src/services/electronService.ts` (NEW)
- `src/renderer/src/i18n/translations.ts` (NEW)
- `src/renderer/src/components/layout/Header.tsx`
- `src/renderer/src/components/robot/RobotSidebar.tsx`
- `src/renderer/src/components/viewport/Viewport3D.tsx`
- `src/renderer/src/components/workflow/WorkflowPanel.tsx`
- `src/renderer/src/components/workflow/BlockWorkspace.tsx` (NEW)
- `src/renderer/src/store/robotStore.ts`
- `src/renderer/src/types/robot.types.ts`
- `src/renderer/src/engine/codegen/luaCodegen.ts`

## Decisions made
- **Bi-directional unit edits**: Decided to allow users to input parameters in meters and radians inside visual Scratch blocks. The UI converts inputs into mm and degrees before modifying store state to ensure consistency across simulated IK loops and exported LUA files.
- **TransformControls Drag Protection**: Disabled state resets on the active manipulation object inside Three.js scene triggers while `transformControls.dragging` is active.

## Bugs / risks / unresolved issues
- Visual Scratch blocks require testing on multiple resolutions to ensure the SVG-like clip-path puzzle indentation displays correctly.

## Next recommended steps
- Follow the manual verification checklist: [manual_testing_checklist.md](file:///C:/Users/Yukina/.gemini/antigravity-ide/brain/f7f42bd5-e6be-4535-bc24-a87994d372c4/manual_testing_checklist.md).
- Continue building out advanced blocks and compile output for physical robot integrations.

# Session: Left Panel Scene Resources and Unit Controls

## Date
2026-05-26 16:36

## Summary
Refined the left sidebar into a Unity-like scene/resource manager with a persistent robot control area, added centimeter unit support, compacted resource/transform UI, and made joint value inputs directly editable with validation.

## What changed
- Refactored the left panel so `Scene` / `Tài nguyên` tabs live above a persistent `Robot Control` section.
- Added a scene hierarchy view for Environment, uploaded Models, and Robot, with visibility toggles for uploaded models only.
- Reworked the resource tab so upload is a small button beside `Danh sách thiết bị`, not a large dropzone.
- Fixed resource panel sizing: upper panel uses a fixed height and transform editor uses fixed non-scrolling compact layout.
- Added `cm` to length units and propagated conversion to Robot Control TCP display, Scratch Move TCP distance input, and viewport distance labels.
- Converted numeric-looking inputs that should not show browser spinner arrows to `type="text"` with `inputMode="decimal"`.
- Added direct joint value editing in Robot Control and clamps entered values to each joint's min/max bound.
- Added dark thin scrollbar styling for scrollable lists.

## Files touched
- `src/renderer/src/components/robot/RobotSidebar.tsx`
- `src/renderer/src/components/scene/ScenePanel.tsx`
- `src/renderer/src/components/workflow/BlockWorkspace.tsx`
- `src/renderer/src/components/viewport/Viewport3D.tsx`
- `src/renderer/src/store/robotStore.ts`
- `src/renderer/src/i18n/translations.ts`
- `src/renderer/src/assets/main.css`

## Decisions made
- Keep internal physical units as mm and degrees; only UI display/input converts to cm/m or rad.
- Use a fixed top-left panel height (`h-[390px]`) to stop Resource/Transform sections from resizing based on object count.
- Keep the transform editor fixed and non-scrolling in compact mode; the object list remains scrollable.
- Use text inputs with `inputMode="decimal"` instead of number inputs to avoid browser spinner arrows in the dark UI.
- For joint direct entry, clamp out-of-range values to `JOINT_BOUNDS` immediately.

## Bugs / risks / unresolved issues
- Fixed heights should be manually checked on smaller/larger app windows.
- Resource transform currently auto-falls back to the first uploaded object if `selectedObjectId` is missing.

## Commands / checks run

```txt
npm run typecheck
Passed.

npm run build
Passed.
```

## Next recommended steps
- Manually test Resource tab with 0, 1, 2, and many uploaded models.
- Verify all transform inputs remain visible without scroll in compact mode.
- Verify joint direct entry clamps correctly in both deg and rad display modes.

## Notes for future agents
- Do not restore number input spinner arrows; the user explicitly rejected them.
- If further tuning is needed, adjust fixed heights in `RobotSidebar.tsx` and `ScenePanel.tsx` rather than returning to flex-based auto-sizing.

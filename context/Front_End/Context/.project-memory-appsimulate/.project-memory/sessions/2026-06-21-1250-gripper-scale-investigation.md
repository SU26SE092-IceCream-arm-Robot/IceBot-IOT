# Session Log - Gripper Scale and Robot Dimension Mismatch Investigation
Date: 2026-06-21 12:50

## Summary of the Session
In this short session, we investigated the issue where the robot arm/gripper appears scaled down incorrectly in the viewport, while the collision hitbox (yellow bounding box) and TCP (red sphere) display at disproportionate dimensions (e.g. hitbox is extremely large compared to the actual visible robot/gripper meshes, or the robot is rendered too small).

We analyzed the generated `debug_scale.txt` log file and the robot's URDF definition (`fairino5_v6.urdf`) to inspect the scaling factors.

## Key Findings & Data Captured
From the user's running environment, `debug_scale.txt` output showed:
- `parent name: wrist3_link`
- `parentWorldScale: (1, 0.9999999999999998, 0.9999999999999998)` — The world scale of `wrist3_link` (the robot joint) is indeed `1.0` in the Three.js world, meaning the URDF robot meshes are loaded with a 1 unit = 1 meter convention.
- `scaleBuster: (1, 1, 1)` — The scale neutralizer correctly computed `1.0` because the parent scale is `1.0`.
- `threeObj local scale: (1, 1, 1)` — The tool wrapper group scale matches the store transform scale (`1.0`).
- `threeObj children: [Group scale: (0.001, 0.001, 0.001)]` — The inner 3D model (loaded via OBJLoader/GLTFLoader) was auto-scaled to `0.001` (millimeter to meter translation) during load.

### The Dimension Mismatch
- The tooltip in the viewport shows: **"Khuỷu tay ↔ Cổ tay 3: 66 mm"** (Elbow to Wrist 3: 66 mm).
- In reality, the physical length of the forearm (Elbow to Wrist 3) of a Fairino FR5 robot is approximately **250 - 300 mm**.
- Measuring it as `66 mm` indicates that either:
  1. The mesh files of the robot links loaded from the URDF are smaller than their physical dimensions (e.g., they were modeled in centimeters/millimeters but loaded as meters, making the robot appear 10 times or 1000 times smaller, or vice-versa).
  2. The joint positions/lengths computed by the `URDFLoader` or kinematics code are in different units than the visual meshes.
  3. The auto-scaling of the gripper tool (`scaleFactor = 0.001`) conflicts with how the robot links are scaled or measured.

## Open Questions & Next Steps
- Determine why the distance between Elbow and Wrist 3 is measured as `66 mm` instead of `~250-300 mm`. Check the mesh bounds of `forearm_link.STL` and the joint offsets in the URDF.
- Check the distance computation function in `Viewport3D.tsx` to verify if it calculates Euclidean distances in Three.js units (meters) and multiplies by 1000 to convert to mm, and whether the robot links are indeed at the correct scale.
- Adjust the auto-scaling logic so both the visual robot mesh, the collision hitboxes, and the imported gripper models align properly at a unified 1 unit = 1 meter (or 1 unit = 1 millimeter) scale.

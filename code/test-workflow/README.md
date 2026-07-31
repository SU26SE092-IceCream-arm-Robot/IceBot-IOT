# test-workflow/

Sample `.lua` file used only by **Test may > 1 (Test tay Robot)** in the console menu, to verify
the arm can actually move — separate from `workflow/`, which holds only `.lua` files downloaded
from BE per store (via provisioning) and is gitignored.

Drop the sample file here as:

```
robot_test.lua
```

If the file is missing, the connection check in "Test tay Robot" still runs; the sample-run step
is just skipped with a message. See `AppConfig.TestSampleScriptName` / `AppConfig.GetTestWorkflowDirectory()`
in `code/src/IceBot/Config/AppConfig.cs`.

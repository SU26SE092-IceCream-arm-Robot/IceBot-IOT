---
name: icebot-test-report
description: Run IceBot-IOT unit tests related to newly added or changed functionality, then generate the canonical Markdown test document from a complete passing run. Use whenever implementation, configuration, contracts, drivers, order processing, BE integration, or tests change, or when test documentation under testing/ must be refreshed.
---

# IceBot test document workflow

1. Inspect the diff and map changed behavior to test classes in `harness/IceBot.Harness.Tests`.
2. Add or update focused xUnit tests for every new behavior, regression, boundary, and rejection path. Do not weaken existing assertions.
3. Add every new test class to the catalog in `scripts/generate-test-document.ps1`.
4. Run `scripts/run-tests-and-document.ps1 -TestClasses ClassA,ClassB` from the repository root. Pass class names without namespaces. The script runs related tests first, then the complete suite.
5. Verify `testing/UNIT_TEST_REPORT.md` contains the same total, passed, and failed counts as `testing/results/IceBot.UnitTests.Fresh.trx`.
6. Keep generated TRX files local. Use the Markdown file as the only test document; do not create or update Excel reports.

If no reliable class mapping exists, omit `-TestClasses`; the script runs the complete suite. Never generate the canonical document from a failed or partial run.

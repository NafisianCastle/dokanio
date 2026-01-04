# OfflineFirstPOS .NET 10 Upgrade Tasks

## Overview

This document tracks the atomic upgrade of the OfflineFirstPOS solution to .NET 10. All projects will be upgraded simultaneously (TFMs and package versions), followed by a build-and-fix pass and automated test execution.

**Progress**: 0/3 tasks complete (0%) ![0%](https://progress-bar.xyz/0)

---

## Tasks

### [▶] TASK-001: Verify prerequisites
**References**: Plan §4 (Common prerequisites), Plan §1 (Executive Summary), Plan §2 (Migration Strategy)

- [✓] (1) Verify .NET 10 SDK is installed (use `dotnet --list-sdks`) per Plan §4
- [▶] (2) Runtime/SDK version meets minimum requirements (**Verify**)
- [ ] (3) Check `global.json` (if present) for SDK compatibility and update per Plan §4
- [ ] (4) `global.json` is compatible with required SDK (**Verify**)

### [ ] TASK-002: Atomic framework and package upgrade with compilation fixes
**References**: Plan §2 (Migration Strategy), Plan §3 (Detailed Dependency Analysis), Plan §4 (Project List), Plan §5 (Package Update Reference), Plan §6 (Breaking Changes Catalog), Plan §10 (Source Control Strategy)

- [ ] (1) Update `TargetFramework`/`TargetFrameworks` in all project files listed in Plan §4 to the proposed TFMs (e.g., `net10.0`, `net10.0-windows`)
- [ ] (2) All project files updated to target frameworks listed in Plan §4 (**Verify**)
- [ ] (3) Update NuGet package references across all projects per Plan §5 (apply package removals where framework provides functionality, e.g., crypto packages)
- [ ] (4) All package references updated per Plan §5 (**Verify**)
- [ ] (5) Update any `Directory.Build.*` or `Directory.Packages.props` files that define central package/version settings per Plan §2 and Plan §5
- [ ] (6) Restore dependencies (`dotnet restore`) for the solution
- [ ] (7) All dependencies restored successfully (**Verify**)
- [ ] (8) Build the full solution and fix all compilation errors caused by the framework and package upgrades, applying fixes guided by Plan §6 (prioritize `Shared.Core` fixes where applicable)
- [ ] (9) Solution builds with 0 errors (**Verify**)
- [ ] (10) Commit changes with message: "TASK-002: Atomic upgrade to net10.0 — update TFMs and package versions"

### [ ] TASK-003: Run automated tests and resolve failures
**References**: Plan §7 (Testing & Validation Strategy), Plan §6 (Breaking Changes Catalog), Plan §3 (Project Graph Summary)

- [ ] (1) Run all discovered test projects per Plan §7 (unit and integration tests)
- [ ] (2) Fix any test failures (reference Plan §6 for common breaking-change fixes; prioritize authentication and EF-related failures)
- [ ] (3) Re-run tests after fixes
- [ ] (4) All tests pass with 0 failures (**Verify**)
- [ ] (5) Commit test fixes with message: "TASK-003: Complete testing and validation"


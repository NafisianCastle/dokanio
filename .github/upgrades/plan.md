# .github/upgrades/plan.md

# OfflineFirstPOS — .NET 10 Upgrade Plan

Table of contents

- [1. Executive Summary](#executive-summary)
- [2. Migration Strategy](#migration-strategy)
- [3. Detailed Dependency Analysis](#detailed-dependency-analysis)
- [4. Project-by-Project Plans](#project-by-project-plans)
- [5. Package Update Reference](#package-update-reference)
- [6. Breaking Changes Catalog](#breaking-changes-catalog)
- [7. Testing & Validation Strategy](#testing--validation-strategy)
- [8. Risk Management](#risk-management)
- [9. Complexity & Effort Assessment](#complexity--effort-assessment)
- [10. Source Control Strategy](#source-control-strategy)
- [11. Success Criteria](#success-criteria)

---

## 1. Executive Summary

### Selected Strategy
**All-At-Once Strategy** - All projects upgraded simultaneously in a single coordinated operation.

**Rationale**:
- Solution contains 4 projects (small solution)
- 3 projects require framework update to `net10.0` (Shared.Core, Server, Desktop)
- Projects are SDK-style and exhibit a clear dependency structure (Shared.Core is a common library)
- Assessment shows package updates are available for all suggested packages and no incompatible packages were detected

### Key Metrics (from assessment)
- Total Projects: 4 (3 require upgrade)
- Total NuGet Packages: 33 (14 require upgrade)
- Total LOC: 22,966
- Total Issues: 80
- Estimated LOC to modify: 59+

### Critical Findings
- High concentration of API compatibility issues in `Server` and `Shared.Core` (source-incompatible and behavioral changes)
- `Mobile` already targets `net10.0` and requires no changes
- Several Microsoft ASP.NET Core and EF Core packages should be updated to `10.0.1` to match target framework

### Next steps
- Prepare environment (SDK and global.json validation)
- Perform atomic upgrade: update all project TargetFramework(s) and package references in a single operation
- Restore, build and fix compilation errors as a single bounded pass
- Run tests and resolve failures

## 2. Migration Strategy

### Chosen Approach
- Strategy: **All-At-Once Strategy** — upgrade all projects simultaneously in a single atomic operation.

### Justification
- Solution size is small (4 projects) and largely homogeneous (SDK-style projects).
- `Mobile` already targets `net10.0`; remaining projects share common dependencies and have clear package upgrade paths available.
- Team can accept short window of coordinated changes and concentrate fixes in one pass.

### Atomic Upgrade Outline
- Prerequisites: validate .NET 10 SDK installed and ensure `global.json` (if present) is compatible.
- Change scope (single coordinated batch):
  - Update TargetFramework/TargetFrameworks in all project files to the proposed TFMs
  - Update NuGet package references to suggested versions from assessment
  - Update any Directory.Build.* or central package files if they define references
  - Restore packages and build the full solution
  - Fix compilation errors discovered during build in the same operation
- Testing: after the atomic upgrade completes, run test projects and fix failures

### Dependency and Ordering Considerations
- All projects will be updated simultaneously; however, keep dependency relationships in mind for troubleshooting (Shared.Core is a dependency for Desktop, Server, Mobile).
- Tests and validations run after the atomic update across entire solution.

### Source control
- All changes should be committed on single feature branch `upgrade-to-NET10` as an atomic commit, with follow-up commits for fixes if needed.

## 3. Detailed Dependency Analysis

### Project Graph Summary
- Root library: `src\Shared.Core` (no project dependencies) — common code used by Desktop, Server, Mobile.
- Consumers: `src\Desktop`, `src\Server`, `src\Mobile` depend on `Shared.Core`.
- No circular dependencies detected.

### Migration Ordering Note
- Strategy is All-At-Once; migration will update all projects simultaneously. The dependency graph is included for troubleshooting and validation: if compilation errors reference Shared.Core APIs, fix Shared.Core sources first within the same upgrade pass.

### Critical Path
- `Shared.Core` is the critical path: changes here may affect three dependents. Prepare to prioritize fixes in Shared.Core as part of the atomic pass.

### Project List (summary)
- `src\Shared.Core\Shared.Core.csproj` — Proposed: `net10.0` (library)
- `src\Server\Server.csproj` — Proposed: `net10.0` (ASP.NET Core)
- `src\Desktop\Desktop.csproj` — Proposed: `net10.0-windows` (WinForms)
- `src\Mobile\Mobile.csproj` — Already `net10.0-*` multi-TFM (no change required)

## 4. Project-by-Project Plans

### Common prerequisites for all projects
- Ensure .NET 10 SDK is installed and `dotnet --list-sdks` shows the required SDK.
- Verify `global.json` (if present) uses a compatible SDK or update it.
- Backup or ensure commit of pending changes and switch to branch `upgrade-to-NET10`.

---

### Project: `src\Shared.Core\Shared.Core.csproj`
**Current**: `net8.0`  
**Target**: `net10.0`  
**Type**: Class Library (SDK-style)  
**Risk**: Medium (source-incompatible and behavioral changes found)  

Migration steps:
1. Update `TargetFramework` to `net10.0` in the project file (or add `net10.0` to `TargetFrameworks` if multitargeting).
2. Update package references per §5 Package Update Reference.
3. Inspect and fix source-incompatible API usages flagged in assessment (e.g., TimeSpan usages, Jwt APIs), prioritizing changes that affect public API.
4. Build solution and resolve compile errors (fix in Shared.Core first where feasible).
5. Run unit tests (if present) and address failures.

Validation:
- Project builds without errors
- No security-vulnerable package versions remain

---

### Project: `src\Server\Server.csproj`
**Current**: `net8.0`  
**Target**: `net10.0`  
**Type**: ASP.NET Core  
**Risk**: High (binary and source-incompatible APIs concentrated here, especially Identity/JWT handling)

Migration steps:
1. Update `TargetFramework` to `net10.0`.
2. Update ASP.NET Core and EF packages to `10.0.1` where recommended.
3. Address binary-incompatible JWT APIs and IdentityModel changes; migrate to `Microsoft.IdentityModel` or updated JwtBearer patterns as required.
4. Rebuild and fix compilation errors. If errors originate in Shared.Core, apply Shared.Core fixes in same pass.
5. Run integration and unit tests; verify authentication flows.

Validation:
- Solution builds with 0 errors
- Auth-related features exercise test coverage pass

---

### Project: `src\Desktop\Desktop.csproj`
**Current**: `net8.0-windows`  
**Target**: `net10.0-windows`  
**Type**: WinForms  
**Risk**: Low

Migration steps:
1. Update `TargetFramework` to `net10.0-windows`.
2. Update package references (Microsoft.Extensions.* packages to 10.0.1 where recommended).
3. Build and fix any minor behavioral changes.
4. Run smoke/manual UI checks (not automated by this plan).

Validation:
- Project builds without errors

---

### Project: `src\Mobile\Mobile.csproj`
**Current**: `net10.0-*` (already target)
**Target**: unchanged

Notes:
- No migration steps required. Verify that Shared.Core changes do not break Mobile builds after upgrade.

## 5. Package Update Reference

### Common package updates (affecting multiple projects)
- `Microsoft.AspNetCore.Authentication.JwtBearer`: 8.0.11 ? 10.0.1 (Server, Shared.Core) — security & compatibility
- `Microsoft.AspNetCore.DataProtection`: 8.0.11 ? 10.0.1 (Shared.Core)
- `Microsoft.AspNetCore.OpenApi`: 8.0.11 ? 10.0.1 (Server)
- `Microsoft.EntityFrameworkCore` family: 8.0.11 ? 10.0.1 (Shared.Core, Server)
- `Microsoft.Extensions.DependencyInjection` / `Abstractions`: 8.x ? 10.0.1 (Desktop, Shared.Core)
- `Microsoft.Extensions.*` logging/hosting/http packages: 8.x ? 10.0.1 where recommended

### Notes
- `System.Security.Cryptography.Algorithms` functionality is included in framework; remove explicit package reference where assessment indicates it's no longer required.
- Keep `Microsoft.Maui.*` packages unchanged in `Mobile` (already 10.0.1).

### Project-specific package notes
- `Desktop`: update `Microsoft.Extensions.*` packages to `10.0.1` where referenced.
- `Server`: upgrade `Microsoft.AspNetCore.*` and EF packages to `10.0.1`.
- `Shared.Core`: upgrade EF Core and ASP.NET related packages listed above.

## 6. Breaking Changes Catalog

This section highlights likely breaking changes and areas to review after framework and package upgrades. These are derived from analysis outputs and common .NET 8?10 upgrade patterns.

### High-impact areas
- Jwt / Identity APIs
  - `JwtSecurityTokenHandler` constructors and `ValidateToken` signatures may have changed; review usage and update to `Microsoft.IdentityModel` patterns where needed.
  - `JwtBearerEvents` members and options shape may differ; update event handlers and option property accesses.

- Entity Framework Core
  - EF Core 10 has behavioral and API changes; review migrations, DbContext usage, and package-specific tool versions.

- Microsoft.Extensions APIs
  - `AddConsole` behavior changed; logging configuration may need adjustments.
  - Dependency injection interfaces and registration patterns remain stable but watch for assembly binding changes.

- TimeSpan-related APIs
  - Some `TimeSpan.FromX` calls flagged as source-incompatible; review durations and conversions.

- System.Security.Cryptography
  - Some algorithm classes may be present in framework; remove package references and validate behavior.

### Validation guidance
- When encountering compile errors, map each error to the breaking change categories above and apply the smallest change that preserves behavior.
- Prefer modern replacement APIs and Microsoft.IdentityModel libraries for auth-related code.

## 7. Testing & Validation Strategy

### Pre-upgrade checks
- Validate .NET 10 SDK installation (`dotnet --list-sdks`) and `global.json` compatibility.
- Ensure branch `upgrade-to-NET10` is checked out and pending changes are committed.

### Post-upgrade automated checks
- `dotnet restore` for the solution
- `dotnet build` for the solution — objective: solution builds with 0 errors
- Run discovered test projects (use `upgrade_discover_test_projects` to list if needed); expected: tests run and pass

### Test scope
- Unit tests (project-level)
- Integration tests (Server)
- Authentication flows (Server) — exercise JWT-related code

### Validation checklist
For each project:
- [ ] Builds without errors
- [ ] Builds without warnings (where feasible)
- [ ] Unit tests pass
- [ ] No remaining security-vulnerable packages

## 8. Risk Management

### Identified Risks
- Authentication/Identity breakages (High) — Server project uses Jwt/Identity APIs flagged as binary-incompatible.
- EF Core behavioral changes (Medium) — database access may require code updates or migration changes.
- Central library changes ripple (Medium) — `Shared.Core` changes affect three consumers.

### Mitigations
- Prioritize fixes in `Shared.Core` during the atomic pass to reduce downstream errors.
- Address Jwt/Identity API changes by adopting Microsoft.IdentityModel patterns and reviewing token handling.
- Ensure tests cover authentication and data access; add minimal tests if coverage is missing for critical paths.
- Keep a backup branch and single atomic PR for easier rollback.

### Contingency
- If a blocking binary-incompatible change is discovered, document and isolate the change in a follow-up PR reverting the specific API usage and applying an alternate approach; keep the main atomic commit small and focused on framework/package updates.

## 9. Complexity & Effort Assessment

### Per-project complexity (relative)
- `src\Shared.Core` — Medium (17 source-incompatible + 12 behavioral change items; largest LOC)
- `src\Server` — High (15 binary-incompatible, 13 source-incompatible; authentication-heavy)
- `src\Desktop` — Low (minor behavioral change)
- `src\Mobile` — Low (already targeting net10.0)

### Overall assessment
- Solution classified as **Simple/Small** by project count, but with **targeted high-risk areas** (Server authentication and Shared.Core API changes). The All-At-Once strategy remains appropriate, with focused attention on Shared.Core and Server during the atomic fix pass.

## 10. Source Control Strategy

- Branch: create and work on `upgrade-to-NET10` (already created per earlier steps).
- Commit approach: prefer a single atomic commit that updates project TFMs and package versions. Subsequent fixes may be committed as follow-up commits but keep the PR focused and small.
- PR: open a single Pull Request targeting `develop` (or `main` if that is your policy) with the atomic upgrade commit and include links to this `plan.md` and `assessment.md`.
- Review: ensure PR reviewers focus on build and test verification. Use PR checks to run `dotnet build` and `dotnet test`.

## 11. Success Criteria

Migration is complete when all of the following are met:

1. All projects target their proposed frameworks (`net10.0` or `net10.0-windows`) per assessment.
2. All package updates from §5 Package Update Reference applied.
3. Solution builds with 0 errors.
4. All automated tests pass (unit and integration tests discovered in the solution).
5. No remaining packages with known security vulnerabilities as reported in assessment.
6. Build and CI pipeline passes on the upgrade branch.


---

Plan generation complete.

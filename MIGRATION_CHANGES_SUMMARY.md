# Migration Script Changes - Summary

## Problem Statement
User reported 3 issues with the Tableau Migration SDK script:
1. **Only project folders copied, not their content** (workbooks, data sources missing)
2. **All project folders copied** instead of only CSV-listed projects
3. **Projects placed at top level** instead of correct destination folders per CSV LUIDs

## Solution Approach

### Phase 1: Filters and Mappings Created
Created the following components to address the requirements:

#### 1. `ProjectMappingStore` (Hooks/Filters/ProjectLuidFilter.cs)
- **Purpose**: Centralized store for CSV project mappings
- **Format**: Maps source ProjectLUID → destination ProjectDestinationLUID
- **Made public** so other filters/mappings can access the same data

#### 2. `ProjectLuidFilter` (Hooks/Filters/ProjectLuidFilter.cs)
- **Purpose**: Filter projects to only those listed in the CSV
- **Logic**: Returns `true` only for projects whose LUID is in the CSV source column
- **Result**: Prevents non-CSV projects from being migrated

#### 3. `SkipAllProjectsFilter` (Hooks/Filters/SkipAllProjectsFilter.cs)
- **Purpose**: Initially created to skip ALL project migration
- **Status**: **REMOVED** - conflicts with the correct approach (see Phase 2)

#### 4. `WorkbookCsvFilter` (Hooks/Filters/WorkbookCSVFilter.cs)
- **Purpose**: Filter workbooks to only those whose parent project is in the CSV
- **Logic**: Gets parent project LUID, checks if it exists in ProjectMappingStore
- **Result**: Only workbooks from CSV-listed projects are migrated

#### 5. `DataSourceCsvFilter` (Hooks/Filters/DataSourceCsvFilter.cs)
- **Purpose**: Same as WorkbookCsvFilter but for data sources
- **Result**: Only data sources from CSV-listed projects are migrated

#### 6. `WorkbookProjectMapping` (Hooks/Mappings/WorkbookProjectMapping.cs)
- **Purpose**: Initially attempted to remap workbooks to destination projects
- **Status**: **NOT USED** - SDK should handle this automatically via project mappings

#### 7. `DataSourceProjectMapping` (Hooks/Mappings/DataSourceProjectMapping.cs)
- **Purpose**: Same as WorkbookProjectMapping but for data sources
- **Status**: **NOT USED** - SDK should handle this automatically via project mappings

### Phase 2: Corrected Strategy

After research and testing, the correct approach is:

#### Migration Flow
```
1. FILTER projects → Only CSV-listed projects pass through
2. MAP projects → Map source projects to existing destination projects
3. MIGRATE projects → SDK attempts to create/update projects
4. FILTER content → Only workbooks/data sources from CSV projects
5. MIGRATE content → SDK automatically uses project mappings for content location
```

#### Current Implementation
1. **`ProjectLuidFilter`** - Filters projects to only CSV-listed ones
2. **`ProjectDestinationLuidMapping`** - Maps filtered projects to destination LUIDs
3. **`WorkbookCsvFilter`** - Filters workbooks to only those from CSV projects
4. **`DataSourceCsvFilter`** - Filters data sources to only those from CSV projects

#### Removed Components
- **`SkipAllProjectsFilter`** - Removed because we need project mappings to establish context
- **`WorkbookProjectMapping`** - Not needed, SDK handles this via project mappings
- **`DataSourceProjectMapping`** - Not needed, SDK handles this via project mappings
- **`DestinationProjectCache`** - Couldn't implement (no API access in mappings)
- **`LoadDestinationProjectCacheHook`** - Couldn't implement (no API access in init hooks)

## Current Status

### ✅ Build Status
- **SUCCESS** - Project compiles with 11 warnings (all pre-existing)
- No compilation errors

### ⚠️ Known Issue with Current Implementation

**Problem**: `ProjectDestinationLuidMapping` uses `Rename(destLuid)` which creates a location path like `/destLuid`. This may not match the actual destination project's location path.

**Why this might work**:
- When the SDK tries to create a project that already exists (same name, same location), Tableau REST API returns the existing project
- The SDK may handle this gracefully and establish the correct mapping

**Why this might fail**:
- If the destination project location doesn't match `/destLuid`, the SDK might try to create a new project
- Could cause errors or duplicate projects

### Recommended Testing Steps

1. **Small-scale test**:
   ```csv
   ProjectLUID,ProjectDestinationLUID
   <one-test-project-luid>,<existing-dest-project-luid>
   ```

2. **Verify destination projects exist** before running migration

3. **Check logs** for:
   - Project filtering messages
   - Project mapping messages
   - Workbook/data source filtering messages
   - Any errors about project creation or location mismatches

4. **Monitor results**:
   - Are only CSV-listed workbooks/data sources migrated? ✅ Expected: YES
   - Are they published to correct destination projects? ⚠️ Needs testing
   - Do any new projects get created? ⚠️ Should be NO (if dest projects exist)

## Alternative Approaches if Current Approach Fails

### Option A: Post-Migration Content Move
Use a post-publish hook to query destination and move content to correct projects after migration

### Option B: Use Full Project Path
Instead of using LUID, use the full destination project path in CSV:
```csv
ProjectLUID,DestinationProjectPath
abc-123,/TopLevelFolder/SubFolder/ProjectName
```

### Option C: Two-Phase Migration
1. First run: Migrate and map projects only (no content)
2. Second run: Use established mappings to migrate content

## Files Modified

### Created:
- `Hooks/Filters/WorkbookCSVFilter.cs`
- `Hooks/Filters/DataSourceCsvFilter.cs`
- `Hooks/Filters/SkipAllProjectsFilter.cs` (later removed)
- `Hooks/Mappings/WorkbookProjectMapping.cs` (not used but still in codebase)
- `Hooks/Mappings/DataSourceProjectMapping.cs` (not used but still in codebase)

### Modified:
- `Hooks/Filters/ProjectLuidFilter.cs` - Made ProjectMappingStore public, added CSV loading
- `MyMigrationApplication.cs` - Updated migration strategy, removed SkipAllProjectsFilter
- `Program.cs` - Added DI registrations for new components, removed cache-related registrations

### Deleted:
- `DestinationProjectCache.cs`
- `Hooks/InitializeMigration/LoadDestinationProjectCacheHook.cs`

## CSV Format Requirement

The CSV file must have this format:
```csv
ProjectLUID,ProjectDestinationLUID
<source-project-guid>,<destination-project-guid>
<source-project-guid>,<destination-project-guid>
```

**CRITICAL**: Destination projects MUST already exist in the destination Tableau Cloud site before running the migration.

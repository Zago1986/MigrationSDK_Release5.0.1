# Migration SDK Fix Summary

## Problems Fixed

### Issue 1: Only Project Folders Were Copied (No Content)
**Root Cause**: The previous implementation had filters and transformers but wasn't properly configured to migrate workbooks and data sources.

**Fix**: 
- Added proper filters (`WorkbookCsvFilter`, `DataSourceCsvFilter`) to include workbooks and data sources from CSV-listed projects
- Added content mappings (`WorkbookProjectMapping`, `DataSourceProjectMapping`) to remap content to destination projects
- Ensured transformers and hooks are properly registered for both workbooks and data sources

### Issue 2: All Project Folders Were Copied (Not Just CSV-Listed Ones)
**Root Cause**: Project filters were not properly configured or not being used.

**Fix**:
- Projects are now **NOT migrated at all** (per requirements - destination projects must already exist)
- Only content (workbooks, data sources) from projects listed in CSV is migrated
- Content is filtered by `WorkbookCsvFilter` and `DataSourceCsvFilter` which check if the parent project LUID is in the CSV

### Issue 3: Content Was Placed at Top Level Instead of Destination Folders
**Root Cause**: Content mappings were not properly remapping the destination project location.

**Fix**:
- Created `WorkbookProjectMapping` and `DataSourceProjectMapping` that:
  - Read the source project LUID from the content's location
  - Look up the destination project LUID from the CSV mapping
  - Remap the content location to the destination project path

## Key Components

### 1. ProjectMappingStore
- **Location**: `MyMigrationApplication.cs`
- **Purpose**: Loads and stores the CSV mapping (ProjectLUID → ProjectDestinationLUID)
- **Key Method**: `TryGetDestination(sourceProjectLuid, out destProjectLuid)`

### 2. WorkbookCsvFilter
- **Location**: `MyMigrationApplication.cs`
- **Purpose**: Filters workbooks to only include those from source projects in the CSV
- **Logic**: Checks if the workbook's parent project LUID exists in the CSV mapping

### 3. DataSourceCsvFilter
- **Location**: `MyMigrationApplication.cs`
- **Purpose**: Filters data sources to only include those from source projects in the CSV
- **Logic**: Checks if the data source's parent project LUID exists in the CSV mapping

### 4. WorkbookProjectMapping
- **Location**: `Hooks/Mappings/WorkbookProjectMapping.cs`
- **Purpose**: Remaps workbook locations to destination project paths
- **Logic**:
  1. Extracts source project LUID from workbook's location
  2. Looks up destination project LUID from CSV mapping
  3. Constructs new location with destination project LUID
  4. Returns mapped context

### 5. DataSourceProjectMapping
- **Location**: `Hooks/Mappings/DataSourceProjectMapping.cs`
- **Purpose**: Remaps data source locations to destination project paths
- **Logic**: Same as WorkbookProjectMapping but for data sources

### 6. ProjectLuidFilter (Updated)
- **Location**: `Hooks/Filters/ProjectLuidFilter.cs`
- **Purpose**: Updated to use ProjectMappingStore instead of reading CSV directly
- **Note**: Currently not used since projects are not migrated

### 7. ProjectDestinationLuidMapping (Updated)
- **Location**: `Hooks/Mappings/ProjectDestinationLuidMapping.cs`
- **Purpose**: Updated to use ProjectMappingStore
- **Note**: Currently not used since projects are not migrated

## Migration Flow

1. **Startup**:
   - Load CSV file into ProjectMappingStore
   - Display preflight report (rows processed, ready, skipped)
   - If dry-run mode, exit after preflight

2. **Build Migration Plan**:
   - Configure source (Tableau Server) and destination (Tableau Cloud)
   - Add filters for workbooks and data sources
   - Add mappings for workbooks and data sources
   - Add transformers (tags, encryption)
   - Add hooks (permissions, logging)

3. **Execute Migration**:
   - Users are migrated (per default ServerToCloud pipeline)
   - Groups are migrated (per default pipeline)
   - **Projects are skipped** (not in content types list)
   - Workbooks are filtered → mapped → migrated
   - Data sources are filtered → mapped → migrated

4. **Content Migration Details**:
   - Filter checks: Is parent project in CSV? → Yes: include, No: skip
   - Mapping: Remap from source project to destination project (by LUID)
   - Publish: Content is published into existing destination project

5. **Results**:
   - Display migration results
   - Show post-migration CSV report
   - Save manifest for potential resume/retry

## Important Prerequisites

1. **Destination projects must already exist** in Tableau Cloud with the exact LUIDs specified in the CSV file
2. **Source server** must be accessible with provided credentials
3. **Destination cloud** must be accessible with provided credentials
4. **CSV file** must exist at the specified path with correct format

## CSV File Format

```csv
ProjectLUID,ProjectDestinationLUID
<source-luid-1>,<dest-luid-1>
<source-luid-2>,<dest-luid-2>
```

Example:
```csv
ProjectLUID,ProjectDestinationLUID
67890,12345
1121-3141-5161,1234-5678-9101
0313-2333-4353,4252-6272-8293
```

## Testing Recommendations

1. **Dry Run First**:
   - Set `csv.dryRun = true` in appsettings.json
   - Run the application to verify CSV is loaded correctly
   - Check preflight report shows expected row counts

2. **Test with Single Project**:
   - Create a CSV with just one project mapping
   - Ensure destination project exists
   - Run migration and verify:
     - Only content from that source project is migrated
     - Content appears in the correct destination project
     - No top-level folders are created

3. **Test with Multiple Projects**:
   - Add more rows to CSV
   - Verify content from each source project goes to its mapped destination
   - Confirm projects not in CSV are not migrated

4. **Error Scenarios**:
   - Test with non-existent destination LUID (should fail gracefully)
   - Test with missing CSV columns (should be caught during loading)
   - Test with empty/invalid CSV rows (should be skipped and reported)

## Logging and Diagnostics

The application logs:
- CSV preflight information (rows processed, ready, skipped)
- Migration strategy explanation
- Individual content filtering decisions
- Content remapping actions
- Migration results by content type
- Post-migration CSV report

Watch for log messages like:
- `Workbook '<name>' from source project <id> remapped to destination project <id>`
- `No destination project mapping found for ...` (indicates missing CSV entry)
- `Could not determine source project ID...` (indicates malformed content location)

## Common Issues and Solutions

### Issue: Content not migrating
- **Check**: Is the source project LUID in the CSV file?
- **Check**: Are there any filter log messages indicating content was skipped?

### Issue: Content goes to wrong destination
- **Check**: Is the destination project LUID correct in the CSV?
- **Check**: Does the destination project actually exist with that LUID?

### Issue: CSV rows skipped
- **Check**: CSV rows for missing/empty ProjectLUID or ProjectDestinationLUID
- **Check**: CSV rows for duplicate ProjectLUID entries
- **Review**: Preflight report shows which rows were skipped and why

### Issue: Migration fails for all content
- **Check**: Do destination projects exist?
- **Check**: Are destination LUIDs correct?
- **Check**: Do you have permissions to publish to destination projects?

## Architecture Notes

This solution follows the Tableau Migration SDK patterns:
- **Filters**: Control what content enters the migration pipeline
- **Mappings**: Transform content locations before publishing
- **Transformers**: Modify content properties
- **Hooks**: React to migration events (batch completion, publish completion)

The key insight for this fix was understanding that:
1. Projects should not be migrated when destinations already exist
2. Content needs both filtering (by source project) AND mapping (to destination project)
3. The CSV provides a LUID-to-LUID mapping, which we use to construct destination paths

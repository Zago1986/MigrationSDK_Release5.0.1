# Destination Project Mapping Fix - Summary

## Problem
Content (workbooks and data sources) was being migrated to the top-level "Explore" folder in Tableau Cloud instead of being placed in the correct destination project folders specified in the CSV file.

## Root Cause
The SDK wasn't properly mapping content to destination projects because:
1. Projects were being processed but not explicitly mapped to destination locations
2. Without explicit destination location mappings, content defaulted to top-level folders

## Solution Implemented

### Strategy Overview
1. **Users**: Skip all user migration (users already exist at destination)
2. **Projects**: Filter to CSV-listed projects and process them to establish container references
3. **Content Location Mapping**: Explicitly map workbooks and data sources to destination project locations using project LUIDs from CSV
4. **Content Filtering**: Only migrate content from CSV-listed source projects

### Key Changes

#### 1. Project Processing (Unchanged - Required)
- **File**: `Hooks/Filters/ProjectLuidFilter.cs`
- **Purpose**: Filters projects to only those listed in CSV
- Projects are processed so SDK has container references for child content
- Uses `item.SourceItem.Id.ToString()` to correctly match CSV project LUIDs

#### 2. Project Mapping (Updated - Non-Invasive)
- **File**: `Hooks/Mappings/ProjectDestinationLuidMapping.cs`
- **Purpose**: Allows projects to be processed without modifying their locations
- Returns original context unchanged - just for logging and container reference establishment
- Actual destination routing is handled by content mappings

#### 3. **NEW: Workbook Location Mapping**
- **File**: `Hooks/Mappings/WorkbookProjectMapping.cs`
- **Key Change**: Explicitly builds destination location as `/{destProjectLuid}/{workbookName}`
- Gets source project ID from `Container.Id` property (not Location.Name)
- Looks up destination project LUID from CSV
- Creates new `ContentLocation` with destination project LUID in path
- This ensures SDK publishes workbook to the correct destination project

#### 4. **NEW: Data Source Location Mapping**
- **File**: `Hooks/Mappings/DataSourceProjectMapping.cs`
- **Key Change**: Same approach as workbook mapping
- Explicitly builds destination location as `/{destProjectLuid}/{dataSourceName}`
- Ensures data sources go to correct destination projects

#### 5. Registration in Migration Plan
- **File**: `MyMigrationApplication.cs`
- Added `WorkbookProjectMapping` and `DataSourceProjectMapping` to plan builder
- Order of operations:
  1. Filter users (skip all)
  2. Filter projects (CSV only)
  3. Map projects (establish container refs)
  4. **Map workbooks** (set destination location)
  5. **Map data sources** (set destination location)
  6. Filter workbooks (CSV projects only)
  7. Filter data sources (CSV projects only)

### How It Works

When a workbook or data source is migrated:

1. **Source Project Identification**: 
   ```csharp
   var mappableContent = ctx.ContentItem as IMappableContainerContent;
   var sourceProjectId = mappableContent?.Container?.Id.ToString();
   ```
   Gets the source project LUID from the content's container reference

2. **Destination Project Lookup**:
   ```csharp
   if (!_mappingStore.TryGetDestination(sourceProjectId, out var destProjectLuid))
   ```
   Looks up the destination project LUID from the CSV mapping

3. **Destination Location Creation**:
   ```csharp
   var destLocation = new ContentLocation($"/{destGuid}/{ctx.ContentItem.Name}");
   var mappedCtx = ctx.MapTo(destLocation);
   ```
   Creates a new location path that includes the destination project LUID

4. **SDK Publishing**:
   The SDK uses this mapped location to publish content to the correct project folder

### Requirements
- Destination projects **must already exist** in Tableau Cloud
- Destination projects must have the exact LUIDs specified in the CSV file
- CSV format: `ProjectLUID,ProjectDestinationLUID` (source LUID → destination LUID)

### Testing
Build succeeded with 11 warnings (all pre-existing).

**Next step**: Run the migration and verify:
1. Content appears in correct destination project folders (not "Explore")
2. No ArgumentNullException errors
3. Check logs for messages like:
   - "Workbook '{name}' (source project {sourceId}) mapped to destination project {destLuid}"
   - "Data source '{name}' (source project {sourceId}) mapped to destination project {destLuid}"

### Expected Behavior
- ✅ Only CSV-listed projects' content will be migrated
- ✅ Content will be placed in correct destination project folders
- ✅ Users will be matched by display name/email (not migrated)
- ✅ No new project folders should be created
- ✅ Content ownership should be preserved (matched to existing users)

### Potential Issues to Monitor
1. **If content still goes to wrong location**: Check that destination project LUIDs in CSV are correct
2. **If authentication errors occur**: Verify destination projects exist and are accessible
3. **If mapping errors occur**: Check logs to see which source/destination project mappings failed

## Files Modified in This Fix
1. `MyMigrationApplication.cs` - Added content location mappings to plan
2. `Hooks/Mappings/ProjectDestinationLuidMapping.cs` - Updated to return original context
3. `Hooks/Mappings/WorkbookProjectMapping.cs` - Simplified to build explicit destination paths
4. `Hooks/Mappings/DataSourceProjectMapping.cs` - Simplified to build explicit destination paths

## Files Removed
1. `Hooks/Transformers/ContentDestinationProjectTransformer.cs` - Unused experimental approach
2. `Hooks/InitializeMigration/PrePopulateProjectManifestHook.cs` - Unused experimental approach

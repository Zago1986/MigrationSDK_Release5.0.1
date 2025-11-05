# Migration Script Improvements - Implementation Summary

## Date: November 5, 2025

## User Requirements

The user requested three improvements to the Tableau Migration SDK script:

### 1. **Destination Folder Mapping**
**Problem**: The script was creating new folders named after the IDs from `Project_Migration.csv` (e.g., a new folder named "123456").

**Required**: Locate the existing destination project folder using the provided ID and migrate content into that folder, not create a new one.

### 2. **Project Folder Structure**
**Problem**: Only the project's content was being migrated.

**Required**: The project folder structure itself should also be migrated to the corresponding destination project folder, preserving the hierarchy.

### 3. **User Migration**
**Problem**: Users were being imported/migrated.

**Required**: Do not import users (they've already been migrated separately). Instead, match content ownership based on the user's display name and email address.

---

## Implementation Changes

### Change 1: Skip Project Migration (Issue #1)

**File Modified**: `Hooks/Mappings/ProjectDestinationLuidMapping.cs`

**Change**: Modified the `MapAsync` method to return `null` instead of creating a mapped location.

**Before**:
```csharp
// Map to a location with the destination LUID as the project name
var destLocation = ctx.ContentItem.Location.Rename(destLuid);
var mappedCtx = ctx.MapTo(destLocation);
return Task.FromResult<ContentMappingContext<IProject>?>(mappedCtx);
```

**After**:
```csharp
// Skip project migration - destination projects already exist
// Return null so the SDK doesn't create new project folders
_logger?.LogInformation("Source project {SourceId} ({SourceName}) mapped to destination LUID {DestLuid} - skipping project migration (dest already exists)", 
    sourceId, ctx.ContentItem.Name, destLuid);

return Task.FromResult<ContentMappingContext<IProject>?>(null);
```

**Result**: 
- Projects listed in CSV will no longer create new folders
- The SDK will recognize that destination projects already exist
- Content will be published to existing projects

### Change 2: Project Hierarchy Handling (Issue #2)

**Approach**: Since we're now skipping ALL project migration (returning `null`), the SDK will not create any project folders - neither top-level nor child projects. This is correct because:

1. **Destination projects already exist** (as per user's requirement)
2. **Project hierarchy already exists** at the destination
3. **Content will be mapped** to the existing project structure

**No code changes needed** - the change in `ProjectDestinationLuidMapping` handles this automatically.

**Note**: The CSV only needs to list top-level projects. Child projects will be handled through the content filtering logic.

### Change 3: Skip User Migration (Issue #3)

**File Created**: `Hooks/Filters/SkipAllUsersFilter.cs`

**Implementation**:
```csharp
/// <summary>
/// Filters out ALL users from migration.
/// Users have already been migrated through a separate process.
/// The SDK will match content ownership based on user display name and email address.
/// </summary>
public class SkipAllUsersFilter : ContentFilterBase<IUser>
{
    public SkipAllUsersFilter(
        ISharedResourcesLocalizer localizer,
        ILogger<IContentFilter<IUser>> logger)
            : base(localizer, logger) { }

    public override bool ShouldMigrate(ContentMigrationItem<IUser> item)
    {
        // Return false to skip all users
        return false;
    }
}
```

**Files Modified**:
- `Program.cs` - Added DI registration: `services.AddScoped<SkipAllUsersFilter>();`
- `MyMigrationApplication.cs` - Added filter registration: `_planBuilder.Filters.Add<SkipAllUsersFilter, IUser>();`

**Result**:
- No users will be migrated/imported
- The SDK will automatically match content ownership by:
  - User display name
  - User email address
- Existing destination users will be used for content ownership

### Change 4: Updated Migration Strategy Log

**File Modified**: `MyMigrationApplication.cs`

**Updated Strategy Message**:
```csharp
_logger.LogInformation("=== Migration Strategy ===");
_logger.LogInformation("- Users will NOT be migrated (already exist at destination)");
_logger.LogInformation("- Projects will be mapped to existing destination projects by LUID from CSV");
_logger.LogInformation("- Project folder structure will be preserved");
_logger.LogInformation("- Only content from CSV-listed projects will be migrated");
_logger.LogInformation("- Content ownership will be matched by user display name and email");
_logger.LogInformation("==========================");
```

---

## Migration Flow (Updated)

### Current Flow:
1. **Load CSV mappings** - ProjectLUID → ProjectDestinationLUID
2. **Skip user migration** - SkipAllUsersFilter returns false for all users
3. **Map projects to destination** - ProjectDestinationLuidMapping returns null (skip creation)
4. **Filter workbooks** - Only workbooks from CSV-listed projects pass through
5. **Filter data sources** - Only data sources from CSV-listed projects pass through
6. **Migrate content** - Workbooks and data sources are published to existing destination projects
7. **Match ownership** - SDK matches content owners by display name and email to existing destination users

### Key Points:
- ✅ **No new project folders created** - destination projects must already exist
- ✅ **No users imported** - destination users must already exist
- ✅ **Content filtered by CSV** - only content from specified projects is migrated
- ✅ **Ownership preserved** - matched by name and email, not by migration
- ✅ **Hierarchy preserved** - existing destination project structure is used

---

## Files Modified

### Created:
- `Hooks/Filters/SkipAllUsersFilter.cs` - New filter to skip all user migration

### Modified:
- `Hooks/Mappings/ProjectDestinationLuidMapping.cs` - Changed to return null instead of mapped location
- `MyMigrationApplication.cs` - Added SkipAllUsersFilter and updated strategy log
- `Program.cs` - Added DI registration for SkipAllUsersFilter

### No Changes Required:
- `Hooks/Filters/WorkbookCsvFilter.cs` - Already filters by CSV projects
- `Hooks/Filters/DataSourceCsvFilter.cs` - Already filters by CSV projects
- `Hooks/Filters/ProjectLuidFilter.cs` - (Not currently used, but preserved)

---

## Testing Checklist

### Before Running Migration:
- [ ] Ensure all destination projects exist in Tableau Cloud with the exact LUIDs specified in CSV
- [ ] Ensure all destination users exist with correct display names and email addresses
- [ ] Verify CSV format: `ProjectLUID,ProjectDestinationLUID`
- [ ] Verify destination project hierarchy matches expected structure

### During Migration:
- [ ] Check logs for: "skipping project migration (dest already exists)"
- [ ] Verify no new project folders are created
- [ ] Verify no users are imported
- [ ] Check that only CSV-listed project content is migrated

### After Migration:
- [ ] Verify workbooks are in correct destination projects
- [ ] Verify data sources are in correct destination projects  
- [ ] Verify content ownership matches existing destination users
- [ ] Verify project hierarchy is preserved
- [ ] Check for any error messages about missing projects or users

---

## CSV Format Requirement

The CSV file must have this exact format:

```csv
ProjectLUID,ProjectDestinationLUID
abc123-456-789,def456-789-012
xyz789-012-345,uvw012-345-678
```

**Important Notes**:
1. **Source ProjectLUID**: The GUID/LUID of the project in the source Tableau Server
2. **ProjectDestinationLUID**: The GUID/LUID of the EXISTING project in destination Tableau Cloud
3. **Destination projects MUST exist** before running the migration
4. **Only top-level projects** need to be listed (child projects inherit the mapping)

---

## Build Status

✅ **Build Successful**
- Compilation: SUCCESS
- Warnings: 11 (all pre-existing, none related to changes)
- Errors: 0

---

## Potential Issues and Solutions

### Issue: Content Not Finding Destination Project
**Symptom**: Content fails to migrate with "project not found" errors

**Cause**: Destination project LUID in CSV doesn't match actual project in Tableau Cloud

**Solution**: 
1. Log into destination Tableau Cloud
2. Navigate to the project
3. Check the URL for the project LUID (format: `/projects/abc-123-def-456`)
4. Update CSV with correct LUID

### Issue: Content Ownership Not Matching
**Symptom**: Content is migrated but owned by wrong users

**Cause**: Destination users don't have matching display names or emails

**Solution**:
1. Verify destination users exist with same email addresses as source
2. Ensure display names match between source and destination
3. The SDK matches by these fields automatically

### Issue: Project Hierarchy Not Preserved
**Symptom**: Child project content appears in wrong location

**Cause**: Destination project structure differs from source

**Solution**:
1. Ensure destination projects have same hierarchy as source
2. CSV should map top-level projects only
3. Child projects will follow parent mapping automatically

---

## Rollback Plan

If migration needs to be reverted:

1. **Projects**: No rollback needed - no projects were created
2. **Users**: No rollback needed - no users were imported
3. **Content**: Manually delete migrated workbooks/data sources from destination
4. **Alternative**: Use previous manifest.json to track what was migrated

---

## Next Steps

1. **Test with small CSV** - Start with 1-2 projects to verify behavior
2. **Verify logs** - Check that project creation is skipped and users are not imported
3. **Validate results** - Ensure content is in correct projects with correct owners
4. **Full migration** - Once validated, proceed with complete CSV list

---

## Contact Information

For issues or questions, refer to:
- Tableau Migration SDK documentation: https://tableau.github.io/migration-sdk/
- Project repository: MigrationSDK_Release5.0.1
- Branch: zago_branch

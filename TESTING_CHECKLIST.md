# Migration Testing Checklist

## Pre-Migration Setup

- [ ] Destination projects exist in Tableau Cloud with correct LUIDs
- [ ] CSV file is created with correct format (ProjectLUID,ProjectDestinationLUID)
- [ ] CSV file path is correctly set in appsettings.json (`csv.projectMapping`)
- [ ] Source Tableau Server credentials are configured in appsettings.json
- [ ] Destination Tableau Cloud credentials are configured in appsettings.json

## Dry Run Test

- [ ] Set `csv.dryRun = true` in appsettings.json
- [ ] Run: `dotnet run --project .\Csharp.ExampleApplication.csproj`
- [ ] Verify CSV Preflight report shows:
  - Correct number of rows processed
  - All expected rows are "ready"
  - No unexpected rows are "skipped"
- [ ] Review any skipped rows and fix CSV if needed

## Actual Migration Test

- [ ] Set `csv.dryRun = false` in appsettings.json
- [ ] Run: `dotnet run --project .\Csharp.ExampleApplication.csproj`
- [ ] Monitor console output for:
  - Migration strategy message (projects NOT migrated, only content)
  - Filter messages (workbooks/data sources being included)
  - Mapping messages (content remapped to destination projects)
  - Any error or warning messages

## Post-Migration Verification

### In Tableau Cloud (Destination)

- [ ] Verify no new project folders were created at top level
- [ ] Navigate to each destination project (from CSV)
- [ ] Verify workbooks from source projects appear in correct destination projects
- [ ] Verify data sources from source projects appear in correct destination projects
- [ ] Check that workbook/data source names are correct
- [ ] Test opening a few workbooks to ensure they work

### In Application Logs

- [ ] Review migration result summary
  - Should show "Succeeded" status
  - Check counts of migrated workbooks and data sources
  - Review any failures (should be 0 for successful migration)
- [ ] Review CSV Post-migration report
  - Successes count should match expected content count
  - Failures should be 0
  - Skipped rows should match preflight (if any)

## Test Scenarios

### Scenario 1: Single Project Migration
- [ ] CSV has 1 row (1 source project → 1 destination project)
- [ ] Source project has X workbooks and Y data sources
- [ ] After migration, destination project has X workbooks and Y data sources
- [ ] No content from other source projects was migrated

### Scenario 2: Multiple Projects to Different Destinations
- [ ] CSV has 3 rows (3 different source → 3 different destinations)
- [ ] Each destination project receives only content from its mapped source
- [ ] No cross-contamination between destinations

### Scenario 3: Multiple Sources to Single Destination
- [ ] CSV has 2 rows with different source projects but same destination
- [ ] Destination project contains content from both source projects
- [ ] Content from both sources coexists in destination

### Scenario 4: Partial Project Set
- [ ] Source has 10 projects, CSV only lists 3
- [ ] Only content from those 3 projects is migrated
- [ ] Content from other 7 projects is not migrated

## Error Scenario Tests

### Invalid Destination LUID
- [ ] CSV includes a destination LUID that doesn't exist
- [ ] Migration should report errors for that content
- [ ] Other valid mappings should still succeed

### Missing CSV Columns
- [ ] Remove or misspell a required column header
- [ ] Application should fail with clear error message during CSV load

### Empty CSV Values
- [ ] CSV row with empty ProjectLUID or ProjectDestinationLUID
- [ ] Row should be skipped and reported in preflight

### Duplicate Source LUIDs
- [ ] CSV has two rows with same ProjectLUID
- [ ] Second row should be skipped and reported in preflight

## Performance Checks

- [ ] Note migration start and end times
- [ ] Check batch sizes in appsettings.json are appropriate
- [ ] Monitor network/API calls if needed
- [ ] Review log for any timeouts or retries

## Rollback Plan

If migration fails or has issues:
- [ ] Use manifest.json file to understand what was migrated
- [ ] Manually delete incorrectly placed content from destination
- [ ] Fix issues (CSV, permissions, credentials)
- [ ] Re-run migration (SDK can resume from manifest)

## Sign-off

- [ ] All tests passed
- [ ] All expected content migrated correctly
- [ ] No unexpected content was migrated
- [ ] Content is in correct destination projects
- [ ] No errors in migration log
- [ ] Stakeholders notified of successful migration

## Notes

Use this section to document any issues, workarounds, or observations during testing:

```
Date: 
Tester: 
Source Projects: 
Destination Projects: 
Issues: 
Resolutions: 
```

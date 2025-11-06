# Server to Cloud Migration SDK - Test Application
This is a console application used to test the Migration SDK .Net Features.

## Overview
This application migrates Tableau content (workbooks and data sources) from Tableau Server to Tableau Cloud, with the ability to remap content to existing destination projects based on a CSV mapping file.

## Migration Strategy

### CSV-Based Project Mapping
- Reads CSV file (ProjectLUID,ProjectDestinationLUID,DestinationProjectPath) from the path specified in `csv.projectMapping` in appsettings.json.
- **Source projects ARE migrated** - they are created as subfolders inside the destination parent projects.
- The destination **parent projects** specified in the CSV must already exist in Tableau Cloud.
- Only content (workbooks, data sources) from source projects listed in the CSV file will be migrated.
- **Project Structure Preserved**: Source projects are created inside destination projects, maintaining the hierarchy:
  ```
  Destination Project (existing in Cloud)
  └── Source Project (migrated from Server)
      └── Workbooks and Data Sources
  ```

**Example**:
- CSV specifies: `DestinationProjectPath = MigrationSDK`
- Source project: `DATABRICKS_TEST`
- Result: Creates `MigrationSDK/DATABRICKS_TEST` in Tableau Cloud
- All content placed in `MigrationSDK/DATABRICKS_TEST`

### User Matching
- **Users are NOT migrated** - they are assumed to already exist at the destination (e.g., via Azure AD import).
- Content ownership is automatically matched by **email address** via the `DestinationUserMapping`.
- **Usernames can differ** between source and destination - the SDK matches users by their email address.
  - Example: Source user `MOS7CA` with email `silvio.carvalho@company.com` → Destination user `mos7ca@company.com` with same email
- The SDK will find the correct destination user based on email and assign content ownership appropriately.
- **Important**: Email addresses must be the same on both platforms for matching to work.

### Migration Behavior
1. **Filters projects**: Only projects listed in the CSV will be migrated from the source.
2. **Creates project hierarchy**: Source projects are created as subfolders inside the destination parent projects.
3. **Filters content**: Only workbooks and data sources whose parent projects are listed in the CSV will be included.
4. **Places content**: Content is published into the newly created source project folder (which is inside the destination parent project).
5. **Prerequisite**: Destination **parent** projects must already exist with the names specified in the CSV.
6. **Failure handling**: If a destination parent project doesn't exist, the migration will fail for those items.

## Configuration

### appsettings.json
Configure the following settings:
- **source**: Tableau Server connection details (URL, site, token name, token)
- **destination**: Tableau Cloud connection details (URL, site, token name, token)
- **csv.projectMapping**: Path to the CSV file (relative or absolute)
- **csv.dryRun**: Set to `true` to preview the migration without actually executing it

### CSV File Format
The CSV file must have the following columns:
```
ProjectLUID,ProjectDestinationLUID,DestinationProjectPath
<source-project-luid>,<destination-project-luid>,<destination-project-name>
```

Example:
```
ProjectLUID,ProjectDestinationLUID,DestinationProjectPath
67890,12345,MigrationSDK
1121-3141-5161,1234-5678-9101,DATABRICKS_TEST
0313-2333-4353,4252-6272-8293,Oracle_TEST
```

Where:
- **ProjectLUID**: The LUID of the source project on Tableau Server
- **ProjectDestinationLUID**: The LUID of the existing destination project on Tableau Cloud (for reference/validation)
- **DestinationProjectPath**: The **exact name** of the destination project in Tableau Cloud

**Important Notes:**
- The Tableau Migration SDK's location system uses project **names/paths**, not LUIDs directly
- The `DestinationProjectPath` must **exactly match** the project name in Tableau Cloud
- For nested projects, use the full path (e.g., `Parent/Child`)
- The SDK maps by path/name and then automatically uses the correct project LUID once matched
- Destination projects must already exist in Tableau Cloud

## How to Run

1. **Set up destination projects**: Ensure all destination projects exist in Tableau Cloud with the LUIDs specified in your CSV file.

2. **Configure appsettings.json**:
   - Set source/destination server URLs and access tokens
   - Set `csv.projectMapping` to the path of your CSV file
   - Optional: Set `csv.dryRun = true` to test without executing the migration

3. **Execute the migration**:
   ```
   dotnet run --project .\Csharp.ExampleApplication.csproj
   ```

4. **Review results**: The application will display:
   - CSV Preflight report (rows processed, ready, skipped)
   - Migration result (Success/Failed)
   - Detailed logs for each content type
   - CSV Post-migration report (successes, failures, skipped rows)

## Sample Output

```
CSV Preflight: rows processed=3, ready=3, skipped=0
=== Migration Strategy ===
- Projects will NOT be migrated (destination projects must already exist)
- Only content (workbooks, data sources) from projects listed in CSV will be migrated
- Content will be published into existing destination projects specified in CSV
==========================
Result: Succeeded
## Workbook ##
Workbook '/SourceProject1/MyWorkbook' migrated to '/DestinationProject/MyWorkbook'
...
CSV Report: rows processed=3, migration successes (items)=12, failures (items)=0, csv-skipped-rows=0
```

## Key Features

- **Selective migration**: Only migrates content from projects specified in the CSV
- **Content consolidation**: Can consolidate multiple source projects into single destination projects
- **Dry-run mode**: Test the migration without making changes
- **Comprehensive logging**: Detailed reports of what was migrated and any failures
- **Error handling**: Clear error messages if destination projects don't exist

## Technical Details

### Filters
- `WorkbookCsvFilter`: Filters workbooks to only include those from source projects in the CSV
- `DataSourceCsvFilter`: Filters data sources to only include those from source projects in the CSV

### Mappings
- `WorkbookProjectMapping`: Remaps workbook locations to destination project paths
- `DataSourceProjectMapping`: Remaps data source locations to destination project paths

### Transformers
- `MigratedTagTransformer`: Adds migration tags to content
- `EncryptExtractsTransformer`: Configures extract encryption

### Hooks
- `UpdatePermissionsHook`: Updates permissions after publishing
- `BulkLoggingHook`: Logs migration details
- `LogMigrationBatchesHook`: Logs batch completion
- `LogMigrationActionsHook`: Logs individual migration actions

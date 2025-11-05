# Server to Cloud Migration SDK - Test Application
This is a console application used to test the Migration SDK .Net Features.

LUID-to-existing-project mapping
- Reads CSV (ProjectLUID,ProjectDestinationLUID) from csv.projectMapping in appsettings.json.
- Projects are not migrated/created. Content is filtered to rows listed in the CSV and remapped to the destination project by LUID.
- If the destination LUID does not exist, no new folder is created; publish fails for those items and is reported.

Run
- Configure source/destination tokens and csv.projectMapping path in appsettings.json.
- Optional dry-run (no migration): set csv.dryRun = true.
- Execute: dotnet run --project .\Csharp.ExampleApplication.csproj

Sample report (truncated)
- CSV Preflight: rows processed=3, ready=3, skipped=0
- Result: Succeeded
- ## Workbook ##
  - ... content detail logs ...
- CSV Report: rows processed=3, migration successes (items)=12, failures (items)=2, csv-skipped-rows=0

PR description
- Fix LUID-folder creation by removing project migration and mapping content to existing destination projects by LUID from CSV. Adds filters and transformers to ensure only mapped content migrates and is published into existing projects. Includes dry-run and clear reporting.

One-liner
- Remap content to existing projects via CSV (ProjectLUID → ProjectDestinationLUID), never create LUID-named folders, with dry-run and reporting.
